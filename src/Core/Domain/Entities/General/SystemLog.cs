namespace Domains.Entities.General
{
    public class SystemLog : BaseEntity
    {
        // property
        public int ApplicationId { get; set; }

        public int OperationCode { get; set; }

        public int EntityId { get; set; }

        public string OperationOwner { get; set; }

        // relation
        public virtual Application Application { get; set; }
    }
}
