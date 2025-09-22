using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.ContentManagement
{
    [Table("CMS_SchemaDetails")]
    public class SchemaDetails : BaseEntity
    {
        public SchemaDetails()
        {
        }

        // property
        public int SchemaId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        public int TypeId { get; set; }

        public int Size { get; set; }


        // foreign key
        [ForeignKey("SchemaId")]
        public virtual Schema Schema { get; set; }
    }
}
