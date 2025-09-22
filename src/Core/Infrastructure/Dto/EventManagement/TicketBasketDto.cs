using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Dto.EventManagement
{
    public class TicketBasketDto : BaseEntity
    {
        public int ApplicationId { get; set; }

        [StringLength(450)]
        public string UserId { get; set; }

        public int EventId { get; set; }

        public int TicketTypeId { get; set; }

        [StringLength(128)]
        public string EventTitle { get; set; }

        [StringLength(128)]
        public string TicketTypeTitle { get; set; }

        public int Count { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }
    }
}