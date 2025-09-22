namespace Infrastructure.Dto.EventManagement
{
    public class TicketOrderBackOfficeDto
    {
        public int Id { get; set; }
        public int Status { get; set; }
        public DateTime UpdatedDT { get; set; }
        public DateTime CreatedDT { get; set; }
        public string UserId { get; set; }
        public int UserType { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal PaidAmount { get; set; }
        public int ApplicationId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
    }
}