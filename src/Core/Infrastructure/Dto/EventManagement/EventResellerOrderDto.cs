using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Dto.EventManagement
{
    public class EventResellerOrderDto
    {
        [Required]
        public string ResellerId { get; set; }
        [Required]
        public int EventId { get; set; }
        [Required]
        public int EventTicketTypeId { get; set; }
        [Required]
        public int Count { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }
    }
}