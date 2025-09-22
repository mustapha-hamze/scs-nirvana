namespace Infrastructure.Dto.EventManagement
{
    public class EventTicketDto : BaseEntity
    {
        public int OrderItemId { get; set; }

        public string EventTitle { get; set; }

        public string TicketTypeTitle { get; set; }

        public string PublicId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public decimal Price { get; set; }

        public string StrLocation { get; set; }

        public string FileId { get; set; }

        // status 100 = valid || status 200 = invalid
    }
}