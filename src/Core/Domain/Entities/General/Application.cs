using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domains.Entities.AccessManagement;
using Domains.Entities.ContentManagement;
using Domains.Entities.CustomModule;

namespace Domains.Entities.General
{
    public class Application : BaseEntity
    {
        // property
        [StringLength(64)]
        public string Title { get; set; }

        [StringLength(512)]
        public string Description { get; set; }

        [StringLength(450)]
        public string LogoFileName { get; set; }

        [StringLength(128)]
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
