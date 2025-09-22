using Microsoft.AspNetCore.Mvc.Filters;
namespace Web.Areas.BackOffice.ContextAccessor;
public class CheckUserApproval : ActionFilterAttribute
{
    public CheckUserApproval()
    {
    }
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {

    }
}