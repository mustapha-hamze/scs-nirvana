using System.Collections.Generic;
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

        public string Title { get; set; }

        public string LogoFileName { get; set; }

        public int TypeId { get; set; }


        // relation
        public virtual ICollection<SchemaDetails> Details { get; set; }

        public virtual Application Application { get; set; }
    }
}
