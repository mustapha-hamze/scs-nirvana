using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Repository;
using Domains.Entities.User;

namespace Application.UserManagementRepository
{
    // Deliberately excluded from the tenant-owned-port cleanup: UserAttachment is owned by a
    // userId, not an applicationId - it isn't a tenant model, so the generic IRepository<T>
    // surface (with its bare, unscoped GetById/Delete) is intentionally kept here rather than
    // requiring an application scope that doesn't apply to this entity.
    public interface IUserAttachmentRepository : IRepository<UserAttachment>
    {
        Task<List<UserAttachment>> List(string userId, CancellationToken cancellationToken = default);

        // Owner-scoped read: missing, deleted, and cross-user attachment ids all throw the same
        // KeyNotFoundException, so callers can't distinguish "doesn't exist" from "exists but isn't yours".
        Task<UserAttachment> GetByIdForUser(int attachmentId, string userId, CancellationToken cancellationToken = default);
    }
}
