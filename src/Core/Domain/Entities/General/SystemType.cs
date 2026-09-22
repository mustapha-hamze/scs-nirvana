namespace Domains.Entities.General
{
    public class SystemType : BaseEntity
    {
        public int ApplicationId { get; set; }
        public int TypeGroupId { get; set; }

        public string Title { get; set; }

        public bool IsRTL { get; set; }

        // relation
        public virtual Application Application { get; set; }
    }
}