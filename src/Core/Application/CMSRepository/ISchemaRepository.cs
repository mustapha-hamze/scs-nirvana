using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.ContentManagement;

namespace Application.CMSRepository
{
    public interface ISchemaRepository
    {
        Task<Schema> Create(Schema schema);
        Task<Schema> Update(Schema schema);

        // Requires applicationId so a schema id can't be deleted from any application but its own.
        Task Delete(int id, int applicationId, CancellationToken cancellationToken = default);

        Task<List<Schema>> List(int applicationId, int typeId, CancellationToken cancellationToken = default);
        Task<List<Schema>> List(int applicationId, CancellationToken cancellationToken = default);

        // Application-scoped: filters on the schema's own applicationId, so a cross-application
        // schemaId simply yields no rows - the same shape as a schemaId with no details.
        Task<List<SchemaDetails>> DetailsList(int schemaId, int applicationId, CancellationToken cancellationToken = default);

        // Application-scoped lookup: throws (SingleAsync) rather than returning null when the id
        // doesn't exist, is soft-deleted, or belongs to a different application, so all three
        // cases are indistinguishable to the caller.
        Task<Schema> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default);

        // Ownership-chain resolution for schema-detail mutations: resolves detail -> schema ->
        // application, rejecting a missing/cross-application/soft-deleted resource at any hop.
        Task<SchemaDetails> GetDetailForApplication(int detailId, int applicationId, CancellationToken cancellationToken = default);

        // Staging-only, purpose-specific CRUD for SchemaDetails (replaces a generic
        // IRepository<SchemaDetails> injection). Callers must resolve/verify ownership via
        // GetDetailForApplication (or the parent's GetByIdForApplication, for create) first.
        Task<SchemaDetails> CreateDetail(SchemaDetails detail);
        Task DeleteDetail(int id, CancellationToken cancellationToken = default);
    }
}