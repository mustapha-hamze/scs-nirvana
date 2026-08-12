using System.Collections.Generic;
using System.Linq;
using Domains.Entities.General;
using Infrastructure.Data;
using Infrastructure.Repository;

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

        // methods
        public List<Domains.Entities.General.Tag> List(int applicationId)
        {
            return _dbContext.Tags
                .Where(t => t.ApplicationId == applicationId && !t.IsDeleted)
                .OrderByDescending(t => t.CreatedDT)
                .ToList();
        }

        public List<Domains.Entities.General.Tag> FindTagsByTypeId(int applicationId, int typeId)
        {
            return _dbContext.Tags
                .Where(t => t.ApplicationId == applicationId && !t.IsDeleted && t.TypeId == typeId)
                .OrderByDescending(t => t.CreatedDT)
                .ToList();
        }
    }
}