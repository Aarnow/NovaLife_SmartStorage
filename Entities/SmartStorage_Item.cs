using SQLite;

namespace SmartStorage.Entities
{
    public class SmartStorage_Item : ModKit.ORM.ModEntity<SmartStorage_Item>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public int CategoryId { get; set; }
        public int BizId { get; set; }

        public SmartStorage_Item()
        {
        }
    }
}
