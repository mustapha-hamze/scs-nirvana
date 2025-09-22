using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Dto.EventManagement
{
    public class SP_TicketOrderItemDto
    {
        public int TicketTypeId { get; set; }
        public int EventId { get; set; }
        public string EventTitle { get; set; }
        public string TicketType { get; set; }
        public int Count { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }
        public int Id { get; set; }
        public int OrderId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string StrLocation { get; set; }
        public string MapLocation { get; set; }
        public string FileId { get; set; }
        public string Artists { get; set; }
        public string Description { get; set; }
    }
}