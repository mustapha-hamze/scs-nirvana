using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Application.Contracts.CMS;
using Domains.Entities.ContentManagement;
using Application.CMSRepository;
using Application.UnitOfWork;

namespace Application.UseCases.CMSServices
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
        public async Task<SchemaDto> Create(SchemaDto schema, int applicationId, CancellationToken cancellationToken = default)
        {
            schema.IsActive = true;
            // Never trust ApplicationId from the DTO - server-pin it.
            schema.ApplicationId = applicationId;
            var created = await _schemaRepository.Create(_mapper.Map<Schema>(schema));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<SchemaDto>(created);
        }

        public async Task<SchemaDto> Update(SchemaDto schema, int applicationId, CancellationToken cancellationToken = default)
        {
            // Resolves the existing row through the application-scoped lookup rather than
            // trusting schema.Id/ApplicationId from the DTO, and merges onto the loaded entity so
            // this call can't be used to move a schema into a different application by re-pinning
            // ApplicationId or by hijacking another application's schema via its Id.
            var existing = await _schemaRepository.GetByIdForApplication(schema.Id, applicationId, cancellationToken);
            schema.ApplicationId = applicationId;
            _mapper.Map(schema, existing);
            var updated = await _schemaRepository.Update(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<SchemaDto>(updated);
        }

        public async Task<SchemaDto> GetById(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<SchemaDto>(await _schemaRepository.GetByIdForApplication(id, applicationId, cancellationToken));
        }

        public async Task Delete(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            await _schemaRepository.GetByIdForApplication(id, applicationId, cancellationToken);
            await _schemaRepository.Delete(id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<SchemaDto>> List(int applicationId, int typeId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<SchemaDto>>(await _schemaRepository.List(applicationId, typeId, cancellationToken));
        }

        public async Task<List<SchemaDto>> List(int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<SchemaDto>>(await _schemaRepository.List(applicationId, cancellationToken));
        }

        public async Task<SchemaDetailsDto> CreateDetails(SchemaDetailsDto schemaDetails, int applicationId, CancellationToken cancellationToken = default)
        {
            // Never trust SchemaId from the DTO - verify the parent schema belongs to this
            // application (and isn't soft-deleted) first.
            await _schemaRepository.GetByIdForApplication(schemaDetails.SchemaId, applicationId, cancellationToken);

            schemaDetails.IsActive = true;
            var created = await _schemaRepository.CreateDetail(_mapper.Map<SchemaDetails>(schemaDetails));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<SchemaDetailsDto>(created);
        }

        public async Task DeleteDetails(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            // Resolves ownership through the actual chain (detail -> schema -> application)
            // rather than a bare-ID delete.
            await _schemaRepository.GetDetailForApplication(id, applicationId, cancellationToken);
            await _schemaRepository.DeleteDetail(id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<SchemaDetailsDto>> DetailsList(int schemaId, int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<SchemaDetailsDto>>(await _schemaRepository.DetailsList(schemaId, applicationId, cancellationToken));
        }
    }
}