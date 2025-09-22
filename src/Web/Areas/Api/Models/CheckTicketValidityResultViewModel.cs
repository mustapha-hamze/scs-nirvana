namespace Web.Areas.Api.Models;
public class CheckTicketValidityResultViewModel
{
    public bool Result { get; set; }
    public string Event { get; set; }
    public string TicketType { get; set; }
    public string PublicId { get; set; }
}