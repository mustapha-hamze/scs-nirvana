using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domains.Entities.General;

namespace Domains.Entities.ContentManagement
{
    public class Schema : BaseEntity
    {
        public Schema()
        {
        }

        // property
        public int ApplicationId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        [StringLength(128)]
        public string LogoFileName { get; set; }

        public int TypeId { get; set; }


        // relation
        public virtual ICollection<SchemaDetails> Details { get; set; }

        public virtual Application Application { get; set; }
    }
}
