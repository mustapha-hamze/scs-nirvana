using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Infrastructure.Dto.GeneralDtos;

namespace Infrastructure.Dto.PropTechDtos
{
    public class PropertyDto : BaseEntity
    {
        public int ApplicationId { get; set; }

        // [ForeignKey("ApplicationId")]
        // public virtual ApplicationDto Application { get; set; }

        public int Type { get; set; }
        public int BedRoom { get; set; }

        [StringLength(128)]
        public string BedRoomDescription { get; set; }

        public int BathRoom { get; set; }
        public int MinArea { get; set; }
        public int MaxArea { get; set; }
        public int MinGrossArea { get; set; }
        public int MaxGrossArea { get; set; }
        public int ReferenceArea { get; set; }
        public int GrossArea { get; set; }
        public int NetArea { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        public string Description { get; set; }
        // extera
        public int ReferenceId { get; set; }
        public int ReferenceId2 { get; set; }
        //realtion
        public int DevelopmentId { get; set; }
        public string DevelopmentTitle { get; set; }

        // [ForeignKey("DevelopmentId")]
        //public virtual DevelopmentDto Development { get; set; }

        // public virtual ICollection<PropertyPriceDto> Prices { get; set; }
        // public virtual ICollection<PropertyAttachmentDto> Attachments { get; set; }
    }
}