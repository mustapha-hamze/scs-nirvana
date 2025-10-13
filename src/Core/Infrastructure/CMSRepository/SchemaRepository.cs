using System.Collections.Generic;
using System.Linq;
using Domains.Entities.ContentManagement;
using Infrastructure.Data;
using Infrastructure.Repository;

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
        public List<Schema> List(int applicationId)
        {
            return _dbContext.Schemas
                .Where(s => s.ApplicationId == applicationId && !s.IsDeleted)
                .Order().ToList();
        }

        public List<SchemaDetails> DetailsList(int schemaId)
        {
            return _dbContext.SchemaDetails
                .Where(s => !s.IsDeleted && s.SchemaId == schemaId)
                .Order().ToList();
        }
    }
}