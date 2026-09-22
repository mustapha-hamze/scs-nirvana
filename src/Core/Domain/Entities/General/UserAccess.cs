namespace Domains.Entities.General
{
    public class UserAccess : BaseEntity
    {
        public string UserId { get; set; }

        public int ApplicationId { get; set; }

        public string Access { get; set; }
    }
}