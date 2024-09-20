using Life;
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
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.0", "Aarnow");
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
            _menu.AddProximityTabLine(PluginInformations, 1237, "", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                if (player.HasBiz() && player.setup.areaId == player.biz.TerrainId) OpenLargeLocker(player);
                else player.Notify("SmartStorage", "Vous devez êtes membre de la société possèdant ce grand casier", NotificationManager.Type.Info);
            });
        }

        #region LOCKER
        public async void OpenLargeLocker(Player player)
        {
            List<SmartStorage_Category> categories = await SmartStorage_Category.Query(c => player.biz.Id == c.BizId);

            //Déclaration
            Panel panel = PanelHelper.Create("Grand casier", UIPanel.PanelType.TabPrice, player, () => OpenLargeLocker(player));

            //Corps
            if (categories.Count > 0)
            {
                foreach (var category in categories)
                {
                    panel.AddTabLine($"{category.CategoryName}", $"{(category.IsBroken ? $"{mk.Color("forcé", mk.Colors.Error)}" : category.Password.Length > 0 ? $"{mk.Color("tiroir verrouillé", mk.Colors.Warning)}" : "")}", category.CategoryIcon, _ =>
                    {
                        if (category.Password.Length > 0 && !category.IsBroken) OpenLargeLockerEnterCategoryPassword(player, category);
                        else OpenLargeLockerCategoryContent(player, category);
                    });
                }
            }
            else panel.AddTabLine("Aucune catégorie", _ => { });

            //Boutons
            panel.NextButton("Sélectionner", () => panel.SelectTab());
            panel.NextButton($"{mk.Size("Modifier une catégorie", 14)}", () => OpenLargeLockerManagement(player, categories[panel.selectedTab])); //modifier
            panel.NextButton($"{mk.Size("Ajouter une catégorie", 14)}", () => OpenLargeLockerManagement(player)); //ajouter
            //panel.NextButton("Historique", () => panel.SelectTab()); //consulter les logs
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
        public void OpenLargeLockerEnterCategoryPassword(Player player, SmartStorage_Category category)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Mot de passe", UIPanel.PanelType.Input, player, () => OpenLargeLockerEnterCategoryPassword(player, category));

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
                        OpenLargeLockerCategoryContent(player, category);
                        return;
                    }
                    else player.Notify("SmartStorage", "Mot de passe incorrect", NotificationManager.Type.Error);                      
                }

                panel.Refresh();
            });
            if (player.setup.characterSkinData.Accessory == OutlawMaskId)
            {
                panel.NextButton("Forcer", async () =>
                {
                    if(InventoryUtils.CheckInventoryContainsItem(player, CrowbarId, CrowbarRequired))
                    {
                        category.IsBroken = true;
                        if (await category.Save())
                        {
                            InventoryUtils.RemoveFromInventory(player, CrowbarId, CrowbarRequired);
                            OpenLargeLockerCategoryContent(player, category);
                            return;
                        }
                        else player.Notify("SmartStorage", "Nous n'avons pas pu forcer ce grand casier", NotificationManager.Type.Error);
                    }
                    else player.Notify("SmartStorage", $"Vous avez besoin de {CrowbarRequired} pied{(CrowbarRequired>1 ?"s":"")} de biche", NotificationManager.Type.Error);
                    panel.Refresh();
                });
            }
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }

        public void OpenLargeLockerManagement(Player player, SmartStorage_Category category = null)
        {
            if(category == null)
            {
                category = new SmartStorage_Category();
                category.CategoryName = "Nouvelle catégorie";
                category.CategoryIcon = IconUtils.Others.None.Id;
                category.BizId = player.biz.Id;
                category.IsBroken = false;
                category.Password = "";
            }

            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Gestion catégorie", UIPanel.PanelType.TabPrice, player, () => OpenLargeLockerManagement(player, category));

            //Corps
            panel.AddTabLine($"{mk.Color("Nom:", mk.Colors.Info)} {category.CategoryName}", _ => OpenLargeLockerSetCategoryName(player, category));
            panel.AddTabLine($"{mk.Color("Icône:", mk.Colors.Info)} {category.CategoryIcon}", "", category.CategoryIcon, _ => OpenLargeLockerSetCategoryIcon(player, category));
            panel.AddTabLine($"{mk.Color("Mot de passe:", mk.Colors.Info)} {(category.Password.Length > 0 ? $"{category.Password}": $"{mk.Color("aucun",mk.Colors.Orange)}")}", _ => OpenLargeLockerSetCategoryPassword(player, category));

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
        public void OpenLargeLockerSetCategoryName(Player player, SmartStorage_Category category)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Nom de la catégorie", UIPanel.PanelType.Input, player, () => OpenLargeLockerSetCategoryName(player, category));

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
        public void OpenLargeLockerSetCategoryIcon(Player player, SmartStorage_Category category)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Icône de la catégorie", UIPanel.PanelType.Input, player, () => OpenLargeLockerSetCategoryIcon(player, category));

            //Corps
            panel.TextLines.Add("Définir l'icône de la catégorie");
            panel.TextLines.Add("Renseigner l'ID d'un objet (cf. wiki)");
            panel.inputPlaceholder = "exemple icône de nugget: 2";

            //Boutons
            panel.PreviousButtonWithAction("Sauvegarder", async () =>
            {
                if (int.TryParse(panel.inputText, out int iconId))
                {
                    category.CategoryIcon = ItemUtils.GetIconIdByItemId(iconId);

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
        public void OpenLargeLockerSetCategoryPassword(Player player, SmartStorage_Category category)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Mot de passe de la catégorie", UIPanel.PanelType.Input, player, () => OpenLargeLockerSetCategoryPassword(player, category));

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

        public async void OpenLargeLockerCategoryContent(Player player, SmartStorage_Category category)
        {
            List<SmartStorage_Item> items = await SmartStorage_Item.Query(i => i.BizId == category.BizId);

            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - {category.CategoryName}", UIPanel.PanelType.TabPrice, player, () => OpenLargeLockerCategoryContent(player, category));

            //Corps
            foreach (var item in items)
            {
                var currentItem = ItemUtils.GetItemById(item.ItemId);
                panel.AddTabLine($"{currentItem.itemName}", $"{item.Quantity}", ItemUtils.GetIconIdByItemId(item.ItemId), _ => OpenLargeLockerWithdraw(player, item, currentItem));
            }

            //Boutons
            panel.NextButton("Déposer", () => OpenLargeLockerDeposit(player, category));
            panel.NextButton("Retirer", () => panel.SelectTab());
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }

        public void OpenLargeLockerDeposit(Player player, SmartStorage_Category category)
        {
            //inventaire du joueur
            Dictionary<int, int> playerInventory = new Dictionary<int, int>();

            for (int i = 0; i < 12; i++)
            {
                if (playerInventory.ContainsKey(player.setup.inventory.items[i].itemId))
                {
                    playerInventory[player.setup.inventory.items[i].itemId] += player.setup.inventory.items[i].number;
                }
                else if (player.setup.inventory.items[i].itemId != 0) playerInventory.Add(player.setup.inventory.items[i].itemId, player.setup.inventory.items[i].number);
            }

            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Déposer dans \"{category.CategoryName}\"", UIPanel.PanelType.TabPrice, player, () => OpenLargeLockerDeposit(player, category));

            //Corps
            foreach ((var item, int index) in playerInventory.Select((item, index) => (item, index)))
            {
                Item currentItem = ItemUtils.GetItemById(item.Key);
                panel.AddTabLine($"{currentItem.itemName}", $"Quantité: {item.Value}", ItemUtils.GetIconIdByItemId(currentItem.id), _ =>
                {
                    OpenLargeLockerDepositQuantity(player, category, currentItem, item.Value);
                });
            }

            //Boutons
            panel.NextButton("Sélectionner", () => panel.SelectTab());
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
        public void OpenLargeLockerDepositQuantity(Player player, SmartStorage_Category category, Item item, int quantity)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Déposer {item.itemName} dans \"{category.CategoryName}\"", UIPanel.PanelType.Input, player, () => OpenLargeLockerDepositQuantity(player, category, item, quantity));

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

        public void OpenLargeLockerWithdraw(Player player, SmartStorage_Item smartStorage_Item, Item item)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Grand casier - Retirer des {item.itemName}", UIPanel.PanelType.Input, player, () => OpenLargeLockerWithdraw(player, smartStorage_Item,item));

            //Corps
            panel.TextLines.Add("Définir la quantité que vous souhaitez retirer");
            panel.TextLines.Add($"Il y a {smartStorage_Item.Quantity} {item.itemName} dans ce grand casier");

            //Boutons
            panel.PreviousButtonWithAction("Confirmer", async () =>
            {
                if (int.TryParse(panel.inputText, out int qty))
                {
                    if (qty > 0)
                    {
                        if (InventoryUtils.AddItem(player, smartStorage_Item.ItemId, smartStorage_Item.Quantity))
                        {
                            smartStorage_Item.Quantity -= qty;

                            if (smartStorage_Item.Quantity > 0 ? await smartStorage_Item.Save() : await smartStorage_Item.Delete())
                            {
                                player.Notify("SmartStorage", $"Vous venez de retirer {(smartStorage_Item.Quantity > 0 ? qty : smartStorage_Item.Quantity + qty)} {item.itemName}", NotificationManager.Type.Success);
                                return true;
                            }
                            else
                            {
                                InventoryUtils.RemoveFromInventory(player, smartStorage_Item.ItemId, qty);
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
        #endregion
    }
}
