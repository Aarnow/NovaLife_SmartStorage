using Life;
using Life.DB;
using Life.InventorySystem;
using Life.Network;
using Life.UI;
using ModKit.Helper;
using ModKit.Interfaces;
using ModKit.Utils;
using SmartStorage.Entities;
using System.Collections.Generic;
using System.Linq;
using _menu = AAMenu.Menu;
using mk = ModKit.Helper.TextFormattingHelper;

namespace SmartStorage
{
    public class SmartStorage : ModKit.ModKit
    {
        public const int OutlawMaskId = 13;
        public const int CrowbarId = 1580;
        public const int CrowbarRequired = 3;

        public SmartStorage(IGameAPI api) : base(api)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.1", "Aarnow");
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();

            Orm.RegisterTable<SmartStorage_Category>();
            Orm.RegisterTable<SmartStorage_Item>();
            Orm.RegisterTable<SmartStorage_Logs>();

            InsertMenu();

            ModKit.Internal.Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");
        }

        public void InsertMenu()
        {
            _menu.AddProximityTabLine(PluginInformations, 1237, "Grand casier", async (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);

                Bizs currentBiz = await LifeDB.db.Table<Bizs>().Where(b => b.TerrainId == player.setup.areaId).FirstOrDefaultAsync();

                if (currentBiz != null && currentBiz != default && currentBiz.TerrainId != default)
                {
                    if ((player.HasBiz() && player.biz.Id == currentBiz.Id) ||
                    (DateUtils.IsCurrentTimeBetween(IllegalStart, IllegalEnd) && player.setup.characterSkinData.Accessory == OutlawMaskId) ||
                    (player.IsAdmin && player.serviceAdmin)) OpenLargeLocker(player, currentBiz);
                    else player.Notify("SmartStorage", "Grand casier inaccessible", NotificationManager.Type.Info);
                }
                else player.Notify("SmartStorage", "Ce grand casier n'est pas intéressant", NotificationManager.Type.Info);
            }, 100);
        }

        #region LOCKER
        public async void OpenLargeLocker(Player player, Bizs currentBiz)
        {
            List<SmartStorage_Category> categories = await SmartStorage_Category.Query(c => currentBiz.Id == c.BizId);

            //Déclaration
            Panel panel = PanelHelper.Create("Grand casier", UIPanel.PanelType.TabPrice, player, () => OpenLargeLocker(player, currentBiz));

            //Corps
            if (categories.Count > 0)
            {
                foreach (var category in categories)
                {
                    panel.AddTabLine($"{category.CategoryName}", $"{(category.IsBroken ? $"{mk.Color("forcé", mk.Colors.Error)}" : category.Password.Length > 0 ? $"{mk.Color("tiroir verrouillé", mk.Colors.Warning)}" : "")}", category.CategoryIcon, _ =>
                    {
                        if (category.Password.Length > 0 && !category.IsBroken) LargeLockerEnterCategoryPassword(player, currentBiz, category);
                        else LargeLockerCategoryContent(player, category);
                    });
                }
            }
            else panel.AddTabLine("Aucune catégorie", _ => { });

            //Boutons
            
            if (player.IsAdmin && player.serviceAdmin)
            {
                panel.NextButton("Historique", () => LargeLockerLogs(player, currentBiz));
            } else
            {
                panel.NextButton("Sélectionner", () => panel.SelectTab());
                panel.NextButton($"{mk.Size("Modifier une catégorie", 14)}", () => LargeLockerManagement(player, currentBiz, categories[panel.selectedTab]));
                panel.NextButton($"{mk.Size("Ajouter une catégorie", 14)}", () => LargeLockerManagement(player, currentBiz));
            }
            panel.CloseButton();

            //Affichage
            panel.Display();
        }

        public async void LargeLockerLogs(Player player, Bizs currentBiz)
        {
            List<SmartStorage_Logs> logs = await SmartStorage_Logs.Query(l => l.BizId == currentBiz.Id);

            Panel panel = PanelHelper.Create($"Grand casier - Historique", UIPanel.PanelType.TabPrice, player, () => LargeLockerLogs(player, currentBiz));

            foreach (var log in logs)
            {
                var currentItem = ItemUtils.GetItemById(log.ItemId);
                panel.AddTabLine($"{mk.Color($"{(log.IsDeposit ? "DÉPÔT" : "RETRAIT")}", (log.IsDeposit ? mk.Colors.Success : mk.Colors.Orange))} par {mk.Color($"{log.CharacterFullName} [{log.CharacterId}]", mk.Colors.Info)}<br>" +
                    $"{mk.Size($"{currentItem.itemName} x {mk.Color($"{log.Quantity}", mk.Colors.Warning)}", 14)}",
                    $"{DateUtils.ConvertNumericalDateToString(log.CreatedAt)}",
                    ItemUtils.GetIconIdByItemId(currentItem.id), _ => { });
            }

            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        public void LargeLockerEnterCategoryPassword(Player player, Bizs currentBiz, SmartStorage_Category category)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Mot de passe", UIPanel.PanelType.Input, player, () => LargeLockerEnterCategoryPassword(player, currentBiz, category));

            //Corps
            panel.TextLines.Add($"{category.CategoryName}");
            panel.TextLines.Add("Entrer le mot de passe");

            //Boutons
            panel.NextButton("Confirmer", () =>
            {
                if (panel.inputText.Length >= 3)
                {
                    if (category.Password == panel.inputText)
                    {
                        LargeLockerCategoryContent(player, category);
                        return;
                    }
                    else player.Notify("SmartStorage", "Mot de passe incorrect", NotificationManager.Type.Error);                      
                }

                panel.Refresh();
            });
            panel.NextButton("Forcer", async () =>
            {
                if (DateUtils.IsCurrentTimeBetween(IllegalStart, IllegalEnd) && player.setup.characterSkinData.Accessory == OutlawMaskId)
                {
                    if (InventoryUtils.CheckInventoryContainsItem(player, CrowbarId, CrowbarRequired))
                    {
                        category.IsBroken = true;
                        if (await category.Save())
                        {
                            InventoryUtils.RemoveFromInventory(player, CrowbarId, CrowbarRequired);
                            LargeLockerCategoryContent(player, category);
                            return;
                        }
                        else player.Notify("SmartStorage", "Nous n'avons pas pu forcer ce grand casier", NotificationManager.Type.Warning);
                    }
                    else player.Notify("SmartStorage", $"Vous avez besoin de {CrowbarRequired} pied{(CrowbarRequired > 1 ? "s" : "")} de biche", NotificationManager.Type.Warning);
                }
                else player.Notify("SmartStorage", $"Vous devez porter un masque et agir entre les heures d'hostilités ({IllegalStart}H - {IllegalEnd}H)", NotificationManager.Type.Warning);

                panel.Refresh();
            });
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
        public void LargeLockerManagement(Player player, Bizs currentBiz, SmartStorage_Category category = null)
        {
            if(category == null)
            {
                category = new SmartStorage_Category();
                category.CategoryName = "Nouvelle catégorie";
                category.CategoryIcon = IconUtils.Others.None.Id;
                category.BizId = currentBiz.Id;
                category.IsBroken = false;
                category.Password = "";
            }

            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Gestion catégorie", UIPanel.PanelType.TabPrice, player, () => LargeLockerManagement(player, currentBiz, category));

            //Corps
            panel.AddTabLine($"{mk.Color("Nom:", mk.Colors.Info)} {category.CategoryName}", _ => LargeLockerSetCategoryName(player, category));
            panel.AddTabLine($"{mk.Color("Icône:", mk.Colors.Info)} {category.CategoryIcon}", "", category.CategoryIcon, _ => LargeLockerSetCategoryIcon(player, category));
            panel.AddTabLine($"{mk.Color("Mot de passe:", mk.Colors.Info)} {(category.Password.Length > 0 ? $"{category.Password}": $"{mk.Color("aucun",mk.Colors.Orange)}")}", _ => LargeLockerSetCategoryPassword(player, category));

            //Boutons
            panel.NextButton("Sélectionner", () => panel.SelectTab());
            if (category.Id != default)
            {
                panel.PreviousButtonWithAction("Supprimer", async () =>
                {
                    var query = await SmartStorage_Item.Query(i => i.CategoryId == category.Id);
                    if (query.Count > 0)
                    {
                        player.Notify("SmartStorage", "Vous ne pouvez pas supprimer une catégorie comportant des objets", NotificationManager.Type.Error);
                        return false;
                    }
                    else
                    {
                        if(await category.Delete())
                        {
                            player.Notify("SmartStorage", "Catégorie supprimée", NotificationManager.Type.Success);
                            return true;
                        }
                        else
                        {
                            player.Notify("SmartStorage", "Nous n'avons pas pu supprimer cette catégorie", NotificationManager.Type.Error);
                            return false;
                        }
                    }
                });
            }
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }

        #region SETTERS
        public void LargeLockerSetCategoryName(Player player, SmartStorage_Category category)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Nom de la catégorie", UIPanel.PanelType.Input, player, () => LargeLockerSetCategoryName(player, category));

            //Corps
            panel.TextLines.Add("Définir le nom de la catégorie");
            panel.inputPlaceholder = "3 caractères minimum";

            //Boutons
            panel.PreviousButtonWithAction("Sauvegarder", async () =>
            {
                if(panel.inputText.Length >= 3)
                {
                    category.CategoryName = panel.inputText;

                    if (await category.Save())
                    {
                        player.Notify("SmartStorage", "Catégorie enregistrée", NotificationManager.Type.Success);
                        return true;
                    }
                    else
                    {
                        player.Notify("SmartStorage", "Nous n'avons pas pu enregistrer votre catégorie", NotificationManager.Type.Error);
                        return false;
                    }
                }
                else
                {
                    player.Notify("SmartStorage", "Format incorrect", NotificationManager.Type.Warning);
                    return false;
                }
            });
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
        public void LargeLockerSetCategoryIcon(Player player, SmartStorage_Category category)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Icône de la catégorie", UIPanel.PanelType.Input, player, () => LargeLockerSetCategoryIcon(player, category));

            //Corps
            panel.TextLines.Add("Définir l'icône de la catégorie");
            panel.TextLines.Add("Renseigner l'ID d'un objet (cf. wiki)");
            panel.inputPlaceholder = "exemple icône de nugget: 2";

            //Boutons
            panel.PreviousButtonWithAction("Sauvegarder", async () =>
            {
                if (int.TryParse(panel.inputText, out int itemId))
                {
                    category.CategoryIcon = ItemUtils.GetIconIdByItemId(itemId);

                    if (await category.Save())
                    {
                        player.Notify("SmartStorage", "Catégorie enregistrée", NotificationManager.Type.Success);
                        return true;
                    }
                    else
                    {
                        player.Notify("SmartStorage", "Nous n'avons pas pu enregistrer votre catégorie", NotificationManager.Type.Error);
                        return false;
                    }
                }
                else
                {
                    player.Notify("SmartStorage", "Format incorrect", NotificationManager.Type.Warning);
                    return false;
                }
            });
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
        public void LargeLockerSetCategoryPassword(Player player, SmartStorage_Category category)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Mot de passe de la catégorie", UIPanel.PanelType.Input, player, () => LargeLockerSetCategoryPassword(player, category));

            //Corps
            panel.TextLines.Add("Définir un mot de passe pour accéder à la catégorie");
            panel.inputPlaceholder = "3 caractères minimum";

            //Boutons
            panel.PreviousButtonWithAction("Sauvegarder", async () =>
            {
                if (panel.inputText.Length >= 3)
                {
                    category.Password = panel.inputText;
                    category.IsBroken = false;

                    if (await category.Save())
                    {
                        player.Notify("SmartStorage", "Catégorie enregistrée", NotificationManager.Type.Success);
                        return true;
                    }
                    else
                    {
                        player.Notify("SmartStorage", "Nous n'avons pas pu enregistrer votre catégorie", NotificationManager.Type.Error);
                        return false;
                    }
                }
                else
                {
                    player.Notify("SmartStorage", "Format incorrect", NotificationManager.Type.Warning);
                    return false;
                }
            });
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
        #endregion

        public async void LargeLockerCategoryContent(Player player, SmartStorage_Category category)
        {
            List<SmartStorage_Item> items = await SmartStorage_Item.Query(i => i.BizId == category.BizId && i.CategoryId == category.Id);

            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - {category.CategoryName}", UIPanel.PanelType.TabPrice, player, () => LargeLockerCategoryContent(player, category));

            //Corps
            foreach (var item in items)
            {
                var currentItem = ItemUtils.GetItemById(item.ItemId);
                panel.AddTabLine($"{currentItem.itemName}", $"{item.Quantity}", ItemUtils.GetIconIdByItemId(item.ItemId), _ => LargeLockerWithdraw(player, item, currentItem));
            }

            //Boutons
            panel.NextButton("Déposer", () => LargeLockerDeposit(player, category));
            panel.NextButton("Retirer", () => panel.SelectTab());
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
        public void LargeLockerDeposit(Player player, SmartStorage_Category category)
        {
            //inventaire du joueur
            Dictionary<int, int> playerInventory = InventoryUtils.ReturnPlayerInventory(player);

            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Déposer dans \"{category.CategoryName}\"", UIPanel.PanelType.TabPrice, player, () => LargeLockerDeposit(player, category));

            //Corps
            foreach ((var item, int index) in playerInventory.Select((item, index) => (item, index)))
            {
                Item currentItem = ItemUtils.GetItemById(item.Key);

                if(currentItem.id == 3 || currentItem.id == 4 || currentItem.id == 5) //pièces mecaniques
                {
                    continue;
                }

                if (currentItem.id == 6 || currentItem.id == 1622 || currentItem.id == 1629) //flingues
                {
                    continue;
                }

                if (currentItem.id == 41) //carte kisa
                {
                    continue;
                }

                panel.AddTabLine($"{currentItem.itemName}", $"Quantité: {item.Value}", ItemUtils.GetIconIdByItemId(currentItem.id), _ =>
                {
                    LargeLockerDepositQuantity(player, category, currentItem, item.Value);
                });
            }

            //Boutons
            panel.NextButton("Sélectionner", () => panel.SelectTab());
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
        public void LargeLockerDepositQuantity(Player player, SmartStorage_Category category, Item item, int quantity)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Déposer {item.itemName} dans \"{category.CategoryName}\"", UIPanel.PanelType.Input, player, () => LargeLockerDepositQuantity(player, category, item, quantity));

            //Corps
            panel.TextLines.Add("Définir la quantité que vous souhaitez déposer");
            panel.TextLines.Add($"Vous avez {quantity} dans votre inventaire");
            panel.inputText = quantity.ToString();

            //Boutons
            panel.PreviousButtonWithAction("Confirmer", async () =>
            {
                if (int.TryParse(panel.inputText, out int qty))
                {
                    if(qty > 0)
                    {
                        if (InventoryUtils.CheckInventoryContainsItem(player, item.id, qty))
                        {
                            SmartStorage_Item smartStorage_Item = new SmartStorage_Item();
                            var query = await SmartStorage_Item.Query(i => i.BizId == player.biz.Id && i.CategoryId == category.Id && i.ItemId == item.id);
                            if (query != null && query.Count > 0)
                            {
                                smartStorage_Item = query.FirstOrDefault();
                                smartStorage_Item.Quantity += qty;
                            }
                            else
                            {
                                smartStorage_Item.ItemId = item.id;
                                smartStorage_Item.Quantity = qty;
                                smartStorage_Item.BizId = player.biz.Id;
                                smartStorage_Item.CategoryId = category.Id;
                            }

                            InventoryUtils.RemoveFromInventory(player, smartStorage_Item.ItemId, qty);

                            if (await smartStorage_Item.Save())
                            {
                                //LOGS
                                SmartStorage_Logs newLog = new SmartStorage_Logs();
                                newLog.BizId = smartStorage_Item.BizId;
                                newLog.CharacterId = player.character.Id;
                                newLog.CharacterFullName = player.GetFullName();
                                newLog.ItemId = smartStorage_Item.ItemId;
                                newLog.Quantity = smartStorage_Item.Quantity;
                                newLog.IsDeposit = true;
                                newLog.CreatedAt = DateUtils.GetNumericalDateOfTheDay();
                                await newLog.Save();

                                player.Notify("SmartStorage", $"Vous venez de déposer {qty} {item.itemName}", NotificationManager.Type.Success);
                                return true;
                            }
                            else
                            {
                                InventoryUtils.AddItem(player, smartStorage_Item.ItemId, qty);
                                player.Notify("SmartStorage", $"Nous n'avons pas pu enregistrer votre dépôt", NotificationManager.Type.Error);
                                return false;
                            }
                        }
                        else
                        {
                            player.Notify("SmartStorage", $"Vous ne possédez pas suffisament de {item.itemName}", NotificationManager.Type.Warning);
                            return false;
                        }
                    }
                    else
                    {
                        player.Notify("SmartStorage", $"Vous devez renseigner une valeur positive", NotificationManager.Type.Warning);
                        return false;
                    }
                }
                else
                {
                    player.Notify("SmartStorage", "Format incorrect", NotificationManager.Type.Warning);
                    return false;
                }
            });
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
        public void LargeLockerWithdraw(Player player, SmartStorage_Item smartStorage_Item, Item item)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Retirer des {item.itemName}", UIPanel.PanelType.Input, player, () => LargeLockerWithdraw(player, smartStorage_Item,item));

            //Corps
            panel.TextLines.Add("Définir la quantité que vous souhaitez retirer");
            panel.TextLines.Add($"Il y a {smartStorage_Item.Quantity} {item.itemName} dans ce grand casier");

            //Boutons
            panel.PreviousButtonWithAction("Confirmer", async () =>
            {
                if (int.TryParse(panel.inputText, out int value))
                {
                    if (value > 0)
                    {
                        int quantity = value > smartStorage_Item.Quantity ? smartStorage_Item.Quantity : value;
                        if (InventoryUtils.AddItem(player, smartStorage_Item.ItemId, quantity))
                        {
                            smartStorage_Item.Quantity -= quantity;

                            if (smartStorage_Item.Quantity > 0 ? await smartStorage_Item.Save() : await smartStorage_Item.Delete())
                            {
                                //LOGS
                                SmartStorage_Logs newLog = new SmartStorage_Logs();
                                newLog.BizId = smartStorage_Item.BizId;
                                newLog.CharacterId = player.character.Id;
                                newLog.CharacterFullName = player.GetFullName();
                                newLog.ItemId = smartStorage_Item.ItemId;
                                newLog.Quantity = quantity;
                                newLog.IsDeposit = false;
                                newLog.CreatedAt = DateUtils.GetNumericalDateOfTheDay();
                                await newLog.Save();

                                player.Notify("SmartStorage", $"Vous venez de retirer {quantity} {item.itemName}", NotificationManager.Type.Success);
                                return true;
                            }
                            else
                            {
                                InventoryUtils.RemoveFromInventory(player, smartStorage_Item.ItemId, quantity);
                                player.Notify("SmartStorage", $"Nous n'avons pas pu enregistrer votre dépôt", NotificationManager.Type.Error);
                                return false;
                            }
                        }
                        else
                        {
                            player.Notify("SmartStorage", $"Vous n'avez pas suffisament d'espace dans votre inventaire", NotificationManager.Type.Warning);
                            return false;
                        }
                    }
                    else
                    {
                        player.Notify("SmartStorage", $"Vous devez renseigner une valeur positive", NotificationManager.Type.Warning);
                        return false;
                    }
                }
                else
                {
                    player.Notify("SmartStorage", "Format incorrect", NotificationManager.Type.Warning);
                    return false;
                }
            });
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
        #endregion
    }
}
