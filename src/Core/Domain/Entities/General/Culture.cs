namespace Domains.Entities.General
{
    public class Culture : BaseEntity
    {
        public int ApplicationId { get; set; }

        public string Title { get; set; }

        public string Key { get; set; }

        public virtual Application Application { get; set; }
    }
}
