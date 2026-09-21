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
        private readonly Repository<SchemaDetails> _detailsRepository;

        // constructor
        public SchemaRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
            _detailsRepository = new Repository<SchemaDetails>(dbContext);
        }

        // methods
        public Task<List<Schema>> List(int applicationId, int typeId, CancellationToken cancellationToken = default)
        {
            return _dbContext.Schemas
                .Where(s => s.ApplicationId == applicationId && s.TypeId == typeId && !s.IsDeleted)
                .Order().ToListAsync(cancellationToken);
        }

        public Task<List<Schema>> List(int applicationId, CancellationToken cancellationToken = default)
        {
            return _dbContext.Schemas
                .Where(s => s.ApplicationId == applicationId && !s.IsDeleted)
                .Order().ToListAsync(cancellationToken);
        }

        public Task<List<SchemaDetails>> DetailsList(int schemaId, int applicationId, CancellationToken cancellationToken = default)
        {
            return _dbContext.SchemaDetails
                .Where(d => !d.IsDeleted && d.SchemaId == schemaId
                    && d.Schema.ApplicationId == applicationId && !d.Schema.IsDeleted)
                .Order().ToListAsync(cancellationToken);
        }

        public async Task<Schema> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Schemas.AsNoTracking()
                .SingleAsync(s => s.Id == id && s.ApplicationId == applicationId && !s.IsDeleted, cancellationToken);
        }

        public async Task<SchemaDetails> GetDetailForApplication(int detailId, int applicationId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.SchemaDetails.AsNoTracking()
                .SingleAsync(d => d.Id == detailId && !d.IsDeleted
                    && d.Schema.ApplicationId == applicationId && !d.Schema.IsDeleted, cancellationToken);
        }

        public Task<SchemaDetails> CreateDetail(SchemaDetails detail) => _detailsRepository.Create(detail);

        public Task DeleteDetail(int id, CancellationToken cancellationToken = default) => _detailsRepository.Delete(id, cancellationToken);
    }
}