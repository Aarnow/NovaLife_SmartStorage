using SQLite;

namespace SmartStorage.Entities
{
    public class SmartStorage_Category : ModKit.ORM.ModEntity<SmartStorage_Category>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public string CategoryName { get; set; }
        public int CategoryIcon { get; set; }
        public int BizId { get; set; }
        public string Password { get; set; }
        public bool IsBroken { get; set; }

        public SmartStorage_Category()
        {
        }
    }
}
