using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.EventManagement
{
    public class EventDto : BaseEntity
    {
        [Required]
        public int ApplicationId { get; set; }

        [StringLength(64)]
        [Required]
        public string Title { get; set; }

        [StringLength(512)]
        [Required]
        public string Artists { get; set; }

        [StringLength(2048)]
        [Required]
        public string Description { get; set; }

        [StringLength(64)]
        public string Fa_Title { get; set; }
        [StringLength(2048)]
        public string Fa_Description { get; set; }

        [Required]
        public DateTime StartDate { get; set; }
        [Required]
        public DateTime EndDate { get; set; }

        [StringLength(128)]
        [Required]
        public string StrLocation { get; set; }

        [StringLength(128)]
        [Required]
        public string MapLocation { get; set; }

        [StringLength(450)]
        public string FileId { get; set; }

        public virtual ICollection<EventTicketsTypeDto> EventTicketsTypes { get; set; }
    }
}