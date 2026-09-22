namespace Domains.Entities.General
{
    public class UserInApplication : BaseEntity
    {
        public string UserId { get; set; }
        public int ApplicationId { get; set; }

        // Un-deletes and reactivates together: a membership that was removed (IsDeleted) is
        // never meaningfully "restored" with only one of the two flags flipped.
        public void Restore()
        {
            IsDeleted = false;
            IsActive = true;
        }
    }
}