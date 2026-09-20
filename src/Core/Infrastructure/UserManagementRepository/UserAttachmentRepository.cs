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

        public async Task<List<UserAttachment>> List(string userId)
        {
            return await _dbContext.UserAttachments
                .Where(a => a.UserId == userId && !a.IsDeleted)
                .ToListAsync();
        }
    }
}
