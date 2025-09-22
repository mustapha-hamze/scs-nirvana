namespace Web.Areas.Api.Models;
public class CheckTicketValidityViewModel
{
    [Required]
    [RegularExpression("^[A-Za-z0-9]{32}$")]
    public string Token { get; set; }
    [Required]
    [RegularExpression(@"^[A-Za-z0-9\-]{18}$")]
    public string PublicId { get; set; }
}