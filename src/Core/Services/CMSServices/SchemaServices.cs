using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Infrastructure.Dto.CMSDtos;
using Domains.Entities.ContentManagement;
using Application.CMSRepository;
using Application.Repository;

namespace Services.CMSServices
{
    public class SchemaServices : ISchemaServices
    {
        // fields
        private readonly ISchemaRepository _schemaRepository;
        private readonly IRepository<SchemaDetails> _schemaDetailsRepository;
        private readonly IMapper _mapper;

        // constructor
        public SchemaServices(ISchemaRepository schemaRepository, IRepository<SchemaDetails> schemaDetailsRepository, IMapper mapper)
        {
            _schemaRepository = schemaRepository;
            _schemaDetailsRepository = schemaDetailsRepository;
            _mapper = mapper;
        }

        // methods
        public async Task<SchemaDto> Create(SchemaDto schema)
        {
            schema.IsActive = true;
            return _mapper.Map<SchemaDto>(await _schemaRepository.Create(_mapper.Map<Schema>(schema)));
        }

        public async Task<SchemaDto> Update(SchemaDto schema)
        {
            return _mapper.Map<SchemaDto>(await _schemaRepository.Update(_mapper.Map<Schema>(schema)));
        }

        public async Task<SchemaDto> GetById(int id)
        {
            return _mapper.Map<SchemaDto>(await _schemaRepository.GetById(id));
        }

        public async Task Delete(int id)
        {
            await _schemaRepository.Delete(id);
        }

        public List<SchemaDto> List(int applicationId, int typeId)
        {
            return _mapper.Map<List<SchemaDto>>(_schemaRepository.List(applicationId, typeId));
        }

        public List<SchemaDto> List(int applicationId)
        {
            return _mapper.Map<List<SchemaDto>>(_schemaRepository.List(applicationId));
        }

        public async Task<SchemaDetailsDto> CreateDetails(SchemaDetailsDto schemaDetails)
        {
            schemaDetails.IsActive = true;
            return _mapper.Map<SchemaDetailsDto>(await _schemaDetailsRepository.Create(_mapper.Map<SchemaDetails>(schemaDetails)));
        }

        public async Task DeleteDetails(int id)
        {
            await _schemaDetailsRepository.Delete(id);
        }

        public List<SchemaDetailsDto> DetailsList(int schemaId)
        {
            return _mapper.Map<List<SchemaDetailsDto>>(_schemaRepository.DetailsList(schemaId));
        }
    }
}
