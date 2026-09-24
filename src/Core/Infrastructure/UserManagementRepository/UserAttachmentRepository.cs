using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.UserManagementRepository;
using Domains.Entities.User;
using Infrastructure.Data;
using Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.UserManagementRepository
{
    public class UserAttachmentRepository : Repository<UserAttachment>, IUserAttachmentRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public UserAttachmentRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<UserAttachment>> List(string userId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.UserAttachments
                .Where(a => a.UserId == userId && !a.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<UserAttachment> GetByIdForUser(int attachmentId, string userId, CancellationToken cancellationToken = default)
        {
            var attachment = await _dbContext.UserAttachments.AsNoTracking()
                .SingleOrDefaultAsync(a => a.Id == attachmentId && a.UserId == userId && !a.IsDeleted, cancellationToken);

            if (attachment is null)
                throw new KeyNotFoundException();

            return attachment;
        }
    }
}
