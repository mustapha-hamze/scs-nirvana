namespace Domains.Entities.General
{
    public class Tag : BaseEntity
    {
        // property
        public int ApplicationId { get; set; }

        public string Title { get; set; }

        public int TypeId { get; set; }

        // relation
        public virtual Application Application { get; set; }
    }
}
