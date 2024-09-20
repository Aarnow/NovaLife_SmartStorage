using SQLite;

namespace SmartStorage.Entities
{
    public class SmartStorage_Logs : ModKit.ORM.ModEntity<SmartStorage_Logs>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public int BizId { get; set; }
        public int CharacterId { get; set; }
        public string CharacterFullName { get; set; }
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public bool IsDeposit { get; set; }
        public int CreatedAt { get; set; }

        public SmartStorage_Logs()
        {
        }
    }
}
