using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Application.Contracts.CMS;
using Domains.Entities.ContentManagement;
using Application.CMSRepository;
using Application.Repository;
using Application.UnitOfWork;

namespace Services.CMSServices
{
    public class SchemaServices : ISchemaServices
    {
        // fields
        private readonly ISchemaRepository _schemaRepository;
        private readonly IRepository<SchemaDetails> _schemaDetailsRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        // constructor
        public SchemaServices(ISchemaRepository schemaRepository, IRepository<SchemaDetails> schemaDetailsRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _schemaRepository = schemaRepository;
            _schemaDetailsRepository = schemaDetailsRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        // methods
        public async Task<SchemaDto> Create(SchemaDto schema)
        {
            schema.IsActive = true;
            var created = await _schemaRepository.Create(_mapper.Map<Schema>(schema));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<SchemaDto>(created);
        }

        public async Task<SchemaDto> Update(SchemaDto schema)
        {
            var updated = await _schemaRepository.Update(_mapper.Map<Schema>(schema));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<SchemaDto>(updated);
        }

        public async Task<SchemaDto> GetById(int id)
        {
            return _mapper.Map<SchemaDto>(await _schemaRepository.GetById(id));
        }

        public async Task Delete(int id)
        {
            await _schemaRepository.Delete(id);
            await _unitOfWork.SaveChangesAsync();
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
            var created = await _schemaDetailsRepository.Create(_mapper.Map<SchemaDetails>(schemaDetails));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<SchemaDetailsDto>(created);
        }

        public async Task DeleteDetails(int id)
        {
            await _schemaDetailsRepository.Delete(id);
            await _unitOfWork.SaveChangesAsync();
        }

        public List<SchemaDetailsDto> DetailsList(int schemaId)
        {
            return _mapper.Map<List<SchemaDetailsDto>>(_schemaRepository.DetailsList(schemaId));
        }
    }
}
