using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Dto.EventManagement
{
    public class EventTicketsTypeDto : BaseEntity
    {
        public int EventId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }
        [StringLength(1024)]
        public string Description { get; set; }

        [StringLength(64)]
        public string Fa_Title { get; set; }
        [StringLength(1024)]
        public string Fa_Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentPrice { get; set; }

        public int Count { get; set; }

        public virtual EventDto Event { get; set; }
    }
}