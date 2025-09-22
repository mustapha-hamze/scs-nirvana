using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Infrastructure.Dto.CMSDtos;
using Domains.Entities.ContentManagement;
using Infrastructure;
using Infrastructure.CMSRepository;
using Infrastructure.Repository;

namespace Services.CMSServices
{
    public class SchemaServices : ISchemaServices
    {
        // fields
        private readonly ISchemaRepository _schemaRepository;
        private readonly IRepository<SchemaDetails> _schemaDetailsRepository;

        // constructor
        public SchemaServices(ISchemaRepository schemaRepository, IRepository<SchemaDetails> schemaDetailsRepository)
        {
            _schemaRepository = schemaRepository;
            _schemaDetailsRepository = schemaDetailsRepository;
        }

        // methods
        public async Task<SchemaDto> Create(SchemaDto schema)
        {
            schema.IsActive = true;
            return Mapper(await _schemaRepository.Create(Mapper(schema)));
        }

        public async Task<SchemaDto> Update(SchemaDto schema)
        {
            return Mapper(await _schemaRepository.Update(Mapper(schema)));
        }

        public async Task<SchemaDto> GetById(int id)
        {
            return Mapper(await _schemaRepository.GetById(id));
        }

        public async Task Delete(int id)
        {
            await _schemaRepository.Delete(id);
        }

        public List<SchemaDto> List(int applicationId)
        {
            return Mapper(_schemaRepository.List(applicationId));
        }

        public async Task<SchemaDetailsDto> CreateDetails(SchemaDetailsDto schemaDetails)
        {
            schemaDetails.IsActive = true;
            return MapperDetails(await _schemaDetailsRepository.Create(MapperDetails(schemaDetails)));
        }

        public async Task DeleteDetails(int id)
        {
            await _schemaDetailsRepository.Delete(id);
        }

        public List<SchemaDetailsDto> DetailsList(int schemaId)
        {
            return MapperDetails(_schemaRepository.DetailsList(schemaId));
        }



        // mapper
        private Schema Mapper(SchemaDto schema)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SchemaDto, Schema>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<SchemaDto, Schema>(schema);
        }
        private SchemaDto Mapper(Schema schema)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Schema, SchemaDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<Schema, SchemaDto>(schema);
        }
        private List<SchemaDto> Mapper(List<Schema> schema)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Schema, SchemaDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<Schema>, List<SchemaDto>>(schema);
        }

        private SchemaDetails MapperDetails(SchemaDetailsDto schemaDetails)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SchemaDetailsDto, SchemaDetails>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<SchemaDetailsDto, SchemaDetails>(schemaDetails);
        }
        private SchemaDetailsDto MapperDetails(SchemaDetails schemaDetails)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SchemaDetails, SchemaDetailsDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<SchemaDetails, SchemaDetailsDto>(schemaDetails);
        }
        private List<SchemaDetailsDto> MapperDetails(List<SchemaDetails> schemaDetails)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SchemaDetails, SchemaDetailsDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<SchemaDetails>, List<SchemaDetailsDto>>(schemaDetails);
        }
    }
}