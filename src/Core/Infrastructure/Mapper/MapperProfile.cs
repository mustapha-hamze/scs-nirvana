using AutoMapper;
using Domains.Entities.AccessManagement;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Domains.Entities.User;
using Infrastructure.Identity;
using Infrastructure.Dto.AccessManagerDtos;
using Infrastructure.Dto.CMSDtos;
using Infrastructure.Dto.GeneralDtos;

namespace Infrastructure.Mapper
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            // ContentManagement
            CreateMap<ContentAttachment, ContentAttachmentDto>();
            CreateMap<ContentAttachmentDto, ContentAttachment>();

            CreateMap<Category, CategoryDto>();
            CreateMap<CategoryDto, Category>();

            CreateMap<Content, ContentDto>();
            // Categories/Tags/Cultures are legacy pipe-delimited compatibility strings whose
            // canonical source of truth is the ContentInCategory/Tag/Culture join tables. A
            // normal content create/update must never set them directly — only the dedicated
            // CreateContentCategories/Tags/Cultures commands (which replace both the join rows
            // and the compatibility string together, in one transaction) may.
            CreateMap<ContentDto, Content>()
                .ForMember(dest => dest.Categories, opt => opt.Ignore())
                .ForMember(dest => dest.Tags, opt => opt.Ignore())
                .ForMember(dest => dest.Cultures, opt => opt.Ignore());

            CreateMap<ContentSection, SectionDto>();
            CreateMap<SectionDto, ContentSection>();

            CreateMap<SectionElement, SectionElementDto>();
            CreateMap<SectionElementDto, SectionElement>();

            CreateMap<ContentMetadata, ContentMetadataDto>();
            CreateMap<ContentMetadataDto, ContentMetadata>();

            CreateMap<ContentImage, ContentImageDto>();
            CreateMap<ContentImageDto, ContentImage>();

            CreateMap<Schema, SchemaDto>();
            CreateMap<SchemaDto, Schema>();

            CreateMap<SchemaDetails, SchemaDetailsDto>();
            CreateMap<SchemaDetailsDto, SchemaDetails>();

            // AccessManagement
            CreateMap<EntityAccess, EntityAccessDto>();
            CreateMap<EntityAccessDto, EntityAccess>();

            CreateMap<Sector, SectorDto>();
            CreateMap<SectorDto, Sector>();

            CreateMap<SectorEntity, SectorEntityDto>();
            CreateMap<SectorEntityDto, SectorEntity>();

            // General
            CreateMap<Domains.Entities.General.Application, ApplicationDto>();
            CreateMap<ApplicationDto, Domains.Entities.General.Application>();

            CreateMap<ApplicationSetting, ApplicationSettingDto>();
            CreateMap<ApplicationSettingDto, ApplicationSetting>();

            CreateMap<UserInApplication, UserInApplicationDto>();

            CreateMap<Culture, CultureDto>();
            CreateMap<CultureDto, Culture>();

            CreateMap<SystemType, SystemTypeDto>();
            CreateMap<SystemTypeDto, SystemType>();

            CreateMap<Domains.Entities.General.Tag, TagDto>();
            CreateMap<TagDto, Domains.Entities.General.Tag>();

            // User management
            CreateMap<ApplicationUser, UserDto>();
            CreateMap<UserAttachment, UserAttachmentDto>();
            CreateMap<UserAttachmentDto, UserAttachment>();
        }
    }
}
