using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.General
{
    [Table("GNR_ApplicationSettings")]
    public class ApplicationSetting : BaseEntity
    {
        public int ApplicationId { get; set; }
        public int SettingId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        [StringLength(512)]
        public string Value { get; set; }

        // relation
        [ForeignKey("ApplicationId")]
        public virtual Application Application { get; set; }
    }
}