using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Dto.EventManagement
{
    public class TicketOrderItemDto : BaseEntity
    {
        public int OrderId { get; set; }
        public int EventId { get; set; }
        public int TicketTypeId { get; set; } // 100 = Customer, 200 = Business
        public int Count { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        [ForeignKey("EventId")]
        public virtual EventDto Event { get; set; }

        [ForeignKey("TicketTypeId")]
        public virtual EventTicketsTypeDto TicketsType { get; set; }

        [ForeignKey("OrderId")]
        public virtual TicketOrderDto TicketOrders { get; set; }
    }
}