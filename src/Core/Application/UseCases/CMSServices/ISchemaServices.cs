using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.CMS;

namespace Application.UseCases.CMSServices
{
    public interface ISchemaServices
    {
        Task<SchemaDto> Create(SchemaDto schema, int applicationId, CancellationToken cancellationToken = default);
        Task<SchemaDto> Update(SchemaDto schema, int applicationId, CancellationToken cancellationToken = default);
        Task<SchemaDto> GetById(int id, int applicationId, CancellationToken cancellationToken = default);
        Task Delete(int id, int applicationId, CancellationToken cancellationToken = default);
        Task<List<SchemaDto>> List(int applicationId, int typeId, CancellationToken cancellationToken = default);
        Task<List<SchemaDto>> List(int applicationId, CancellationToken cancellationToken = default);


        Task<SchemaDetailsDto> CreateDetails(SchemaDetailsDto schemaDetails, int applicationId, CancellationToken cancellationToken = default);
        Task DeleteDetails(int id, int applicationId, CancellationToken cancellationToken = default);
        Task<List<SchemaDetailsDto>> DetailsList(int schemaId, int applicationId, CancellationToken cancellationToken = default);
    }
}