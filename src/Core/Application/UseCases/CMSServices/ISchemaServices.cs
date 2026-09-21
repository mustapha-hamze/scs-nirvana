using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.CMS;

namespace Services.CMSServices
{
    public interface ISchemaServices
    {
        Task<SchemaDto> Create(SchemaDto schema, int applicationId);
        Task<SchemaDto> Update(SchemaDto schema, int applicationId);
        Task<SchemaDto> GetById(int id, int applicationId);
        Task Delete(int id, int applicationId);
        List<SchemaDto> List(int applicationId, int typeId);
        List<SchemaDto> List(int applicationId);


        Task<SchemaDetailsDto> CreateDetails(SchemaDetailsDto schemaDetails, int applicationId);
        Task DeleteDetails(int id, int applicationId);
        List<SchemaDetailsDto> DetailsList(int schemaId, int applicationId);
    }
}