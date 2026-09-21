using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.ContentManagement;
using Application.Repository;

namespace Application.CMSRepository
{
    public interface ISchemaRepository : IRepository<Schema>
    {
        List<Schema> List(int applicationId, int typeId);
        List<Schema> List(int applicationId);

        // Application-scoped: filters on the schema's own applicationId, so a cross-application
        // schemaId simply yields no rows - the same shape as a schemaId with no details.
        List<SchemaDetails> DetailsList(int schemaId, int applicationId);

        // Application-scoped lookup: throws (SingleAsync) rather than returning null when the id
        // doesn't exist, is soft-deleted, or belongs to a different application, so all three
        // cases are indistinguishable to the caller.
        Task<Schema> GetByIdForApplication(int id, int applicationId);

        // Ownership-chain resolution for schema-detail mutations: resolves detail -> schema ->
        // application, rejecting a missing/cross-application/soft-deleted resource at any hop.
        Task<SchemaDetails> GetDetailForApplication(int detailId, int applicationId);

        // Staging-only, purpose-specific CRUD for SchemaDetails (replaces a generic
        // IRepository<SchemaDetails> injection). Callers must resolve/verify ownership via
        // GetDetailForApplication (or the parent's GetByIdForApplication, for create) first.
        Task<SchemaDetails> CreateDetail(SchemaDetails detail);
        Task DeleteDetail(int id);
    }
}