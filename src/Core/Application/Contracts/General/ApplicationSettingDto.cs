using System.ComponentModel.DataAnnotations;
using Domains.Entities;

namespace Application.Contracts.General
{
    public class ApplicationSettingDto : BaseEntity
    {
        public int ApplicationId { get; set; }
        public int SettingId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        [StringLength(512)]
        public string Value { get; set; }
    }
}
