using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Dto.CMSDtos;

namespace Services.CMSServices
{
    public interface ISchemaServices
    {
        Task<SchemaDto> Create(SchemaDto schema);
        Task<SchemaDto> Update(SchemaDto schema);
        Task<SchemaDto> GetById(int id);
        Task Delete(int id);
        List<SchemaDto> List(int applicationId, int typeId);
        List<SchemaDto> List(int applicationId);


        Task<SchemaDetailsDto> CreateDetails(SchemaDetailsDto schemaDetails);
        Task DeleteDetails(int id);
        List<SchemaDetailsDto> DetailsList(int schemaId);
    }
}