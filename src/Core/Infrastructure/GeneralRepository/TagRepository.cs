using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domains.Entities.General;
using Infrastructure.Data;
using Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.GeneralRepository
{
    public class TagRepository : Repository<Domains.Entities.General.Tag>, global::Application.GeneralRepository.ITagRepository
    {
        // fields
        private readonly ApplicationDbContext _dbContext;


        // constructor
        public TagRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Domains.Entities.General.Tag> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Tags.AsNoTracking()
                .SingleAsync(t => t.Id == id && t.ApplicationId == applicationId && !t.IsDeleted, cancellationToken);
        }

        // methods
        public Task<List<Domains.Entities.General.Tag>> List(int applicationId, CancellationToken cancellationToken = default)
        {
            return _dbContext.Tags
                .Where(t => t.ApplicationId == applicationId && !t.IsDeleted)
                .OrderByDescending(t => t.CreatedDT)
                .ToListAsync(cancellationToken);
        }

        public Task<List<Domains.Entities.General.Tag>> FindTagsByTypeId(int applicationId, int typeId, CancellationToken cancellationToken = default)
        {
            return _dbContext.Tags
                .Where(t => t.ApplicationId == applicationId && !t.IsDeleted && t.TypeId == typeId)
                .OrderByDescending(t => t.CreatedDT)
                .ToListAsync(cancellationToken);
        }
    }
}