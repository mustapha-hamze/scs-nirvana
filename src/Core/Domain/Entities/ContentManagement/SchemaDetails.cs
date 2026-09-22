namespace Domains.Entities.ContentManagement
{
    public class SchemaDetails : BaseEntity
    {
        public SchemaDetails()
        {
        }

        // property
        public int SchemaId { get; set; }

        public string Title { get; set; }

        public int TypeId { get; set; }

        public int Size { get; set; }


        // foreign key
        public virtual Schema Schema { get; set; }
    }
}
