using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.CMSRepository;
using Domains.Entities.ContentManagement;
using Infrastructure.Data;
using Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.CMSRepository
{
    public class SchemaRepository : Repository<Schema>, ISchemaRepository
    {
        // fields
        private readonly ApplicationDbContext _dbContext;

        // constructor
        public SchemaRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        // methods
        public List<Schema> List(int applicationId, int typeId)
        {
            return _dbContext.Schemas
                .Where(s => s.ApplicationId == applicationId && s.TypeId == typeId && !s.IsDeleted)
                .Order().ToList();
        }

        public List<Schema> List(int applicationId)
        {
            return _dbContext.Schemas
                .Where(s => s.ApplicationId == applicationId && !s.IsDeleted)
                .Order().ToList();
        }

        public List<SchemaDetails> DetailsList(int schemaId, int applicationId)
        {
            return _dbContext.SchemaDetails
                .Where(d => !d.IsDeleted && d.SchemaId == schemaId
                    && d.Schema.ApplicationId == applicationId && !d.Schema.IsDeleted)
                .Order().ToList();
        }

        public async Task<Schema> GetByIdForApplication(int id, int applicationId)
        {
            return await _dbContext.Schemas.AsNoTracking()
                .SingleAsync(s => s.Id == id && s.ApplicationId == applicationId && !s.IsDeleted);
        }

        public async Task<SchemaDetails> GetDetailForApplication(int detailId, int applicationId)
        {
            return await _dbContext.SchemaDetails.AsNoTracking()
                .SingleAsync(d => d.Id == detailId && !d.IsDeleted
                    && d.Schema.ApplicationId == applicationId && !d.Schema.IsDeleted);
        }
    }
}