using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Dto.EventManagement
{
    public class TicketOrderDto : BaseEntity
    {
        public int ApplicationId { get; set; }

        [StringLength(450)]
        public string UserId { get; set; } // 100: normal, 200: business
        public int UserType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        [StringLength(450)]
        public string FileId { get; set; }


        public virtual ICollection<TicketOrderItemDto> TicketsOrderItems { get; set; }
    }
}