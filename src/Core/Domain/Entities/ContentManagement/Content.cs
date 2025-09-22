using System;
using System.Collections.Generic;
using Domains.Entities.General;

namespace Domains.Entities.ContentManagement
{
    [Table("CMS_Contents")]
    public class Content : BaseEntity
    {
        // property
        public int ApplicationId { get; set; }

        public int TypeId { get; set; }

        [StringLength(256)]
        public string Title { get; set; }

        [StringLength(256)]
        public string Title_FA { get; set; }

        [StringLength(2048)]
        public string HeadLine { get; set; }

        [StringLength(2048)]
        public string HeadLine_FA { get; set; }

        [StringLength(2048)]
        public string Abstract { get; set; }

        [StringLength(2048)]
        public string Abstract_FA { get; set; }

        public string Description { get; set; }

        public string Description_FA { get; set; }

        [StringLength(1024)]
        public string Categories { get; set; }
        [StringLength(1024)]
        public string Tags { get; set; }
        [StringLength(1024)]
        public string Cultures { get; set; }
        public DateTime PublishDt { get; set; }

        // relation
        public virtual ICollection<ContentSection> Sections { get; set; }

        public virtual ICollection<ContentImage> Images { get; set; }

        public virtual ContentMetadata Metadata { get; set; }

        [ForeignKey("ApplicationId")]
        public virtual Application Application { get; set; }
    }
}
