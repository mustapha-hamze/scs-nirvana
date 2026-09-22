namespace Domains.Entities.General
{
    public class ApplicationSetting : BaseEntity
    {
        public int ApplicationId { get; set; }
        public int SettingId { get; set; }

        public string Title { get; set; }

        public string Value { get; set; }

        // relation
        public virtual Application Application { get; set; }
    }
}