using System.Collections.Generic;
using Domains.Entities.AccessManagement;
using Domains.Entities.ContentManagement;
using Domains.Entities.CustomModule;

namespace Domains.Entities.General
{
    public class Application : BaseEntity
    {
        // property
        public string Title { get; set; }

        public string Description { get; set; }

        public string LogoFileName { get; set; }

        public string ApplicationKey { get; set; }


        // relation
        public virtual ICollection<Content> Contents { get; set; }
        public virtual ICollection<Tag> Tags { get; set; }
        public virtual ICollection<Culture> Cultures { get; set; }
        public virtual ICollection<SystemLog> SystemLogs { get; set; }
        public virtual ICollection<Schema> Schemas { get; set; }
        public virtual ICollection<Slider> Sliders { get; set; }
        public virtual ICollection<ApplicationSetting> ApplicationSettings { get; set; }


        public virtual ICollection<SystemType> SystemTypes { get; set; }
        public virtual ICollection<Sector> Sectors { get; set; }
    }
}
