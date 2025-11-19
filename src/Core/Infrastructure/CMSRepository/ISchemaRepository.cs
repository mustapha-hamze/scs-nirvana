using System.Collections.Generic;
using Domains.Entities.ContentManagement;
using Infrastructure.Repository;

namespace Infrastructure.CMSRepository
{
    public interface ISchemaRepository : IRepository<Schema>
    {
        List<Schema> List(int applicationId, int typeId);
        List<Schema> List(int applicationId);
        List<SchemaDetails> DetailsList(int schemaId);
    }
}