using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Repository;
using Domains.Entities.User;

namespace Application.UserManagementRepository
{
    public interface IUserAttachmentRepository : IRepository<UserAttachment>
    {
        Task<List<UserAttachment>> List(string userId, CancellationToken cancellationToken = default);
    }
}
