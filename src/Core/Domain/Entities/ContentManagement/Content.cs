using System;
using System.Collections.Generic;
using Domains.Entities.General;

namespace Domains.Entities.ContentManagement
{
    public class Content : BaseEntity
    {
        // property
        public int ApplicationId { get; set; }

        public int TypeId { get; set; }

        public string Title { get; set; }

        public string HeadLine { get; set; }

        public string Abstract { get; set; }

        public string Description { get; set; }

        public string FarsiContent { get; set; }

        public string Categories { get; set; }
        public string Tags { get; set; }
        public string Cultures { get; set; }
        public DateTime PublishDt { get; set; }

        // relation
        public virtual ICollection<ContentSection> Sections { get; set; }

        public virtual ICollection<ContentImage> Images { get; set; }

        public virtual ContentMetadata Metadata { get; set; }

        public virtual Application Application { get; set; }
    }
}
