using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Application.Contracts.CMS;
using Domains.Entities.ContentManagement;
using Application.CMSRepository;
using Application.UnitOfWork;

namespace Services.CMSServices
{
    public class SchemaServices : ISchemaServices
    {
        // fields
        private readonly ISchemaRepository _schemaRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        // constructor
        public SchemaServices(ISchemaRepository schemaRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _schemaRepository = schemaRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        // methods
        public async Task<SchemaDto> Create(SchemaDto schema, int applicationId)
        {
            schema.IsActive = true;
            // Never trust ApplicationId from the DTO - server-pin it.
            schema.ApplicationId = applicationId;
            var created = await _schemaRepository.Create(_mapper.Map<Schema>(schema));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<SchemaDto>(created);
        }

        public async Task<SchemaDto> Update(SchemaDto schema, int applicationId)
        {
            // Resolves the existing row through the application-scoped lookup rather than
            // trusting schema.Id/ApplicationId from the DTO, and merges onto the loaded entity so
            // this call can't be used to move a schema into a different application by re-pinning
            // ApplicationId or by hijacking another application's schema via its Id.
            var existing = await _schemaRepository.GetByIdForApplication(schema.Id, applicationId);
            schema.ApplicationId = applicationId;
            _mapper.Map(schema, existing);
            var updated = await _schemaRepository.Update(existing);
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<SchemaDto>(updated);
        }

        public async Task<SchemaDto> GetById(int id, int applicationId)
        {
            return _mapper.Map<SchemaDto>(await _schemaRepository.GetByIdForApplication(id, applicationId));
        }

        public async Task Delete(int id, int applicationId)
        {
            await _schemaRepository.GetByIdForApplication(id, applicationId);
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

        public async Task<SchemaDetailsDto> CreateDetails(SchemaDetailsDto schemaDetails, int applicationId)
        {
            // Never trust SchemaId from the DTO - verify the parent schema belongs to this
            // application (and isn't soft-deleted) first.
            await _schemaRepository.GetByIdForApplication(schemaDetails.SchemaId, applicationId);

            schemaDetails.IsActive = true;
            var created = await _schemaRepository.CreateDetail(_mapper.Map<SchemaDetails>(schemaDetails));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<SchemaDetailsDto>(created);
        }

        public async Task DeleteDetails(int id, int applicationId)
        {
            // Resolves ownership through the actual chain (detail -> schema -> application)
            // rather than a bare-ID delete.
            await _schemaRepository.GetDetailForApplication(id, applicationId);
            await _schemaRepository.DeleteDetail(id);
            await _unitOfWork.SaveChangesAsync();
        }

        public List<SchemaDetailsDto> DetailsList(int schemaId, int applicationId)
        {
            return _mapper.Map<List<SchemaDetailsDto>>(_schemaRepository.DetailsList(schemaId, applicationId));
        }
    }
}
