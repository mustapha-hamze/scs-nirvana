namespace Application.Contracts.Tenancy
{
    // Delivery-agnostic port for the caller's selected tenant. The Web adapter backs this with
    // the authenticated browser session, so a selection made in one session never affects
    // another session for the same account. Null means no application is selected.
    public interface ICurrentApplicationContext
    {
        int? CurrentApplicationId { get; set; }
    }
}
