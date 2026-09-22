namespace Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
[ServiceFilter(typeof(RequireTenantContextFilter))]
public class BaseController : Controller
{
}
