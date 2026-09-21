using AutoMapper;
using Application.Contracts.AccessManagement;
using Application.Contracts.CMS;
using Application.Contracts.General;
using Application.Contracts.UserManagement;
using Domains.Entities.AccessManagement;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Domains.Entities.User;

namespace Application.Mapper;

// Domain <-> Application.Contracts maps only. Identity and persistence/SP-model maps stay in
// Infrastructure.Mapper.MapperProfile, since Application must not reference Infrastructure types.
//
// AssertConfigurationIsValid() (Core.Tests.Architecture.MappingConfigurationTests) enforces every
// map here. Every ignore below is either an EF navigation/collection with no DTO counterpart, or
// a field this profile must never write from a DTO - never a blanket suppression.
public class ApplicationMapperProfile : Profile
{
    public ApplicationMapperProfile()
    {
        // ContentManagement
        CreateMap<Category, CategoryDto>();
        CreateMap<CategoryDto, Category>()
            .ForMember(dest => dest.Application, opt => opt.Ignore());

        CreateMap<Content, ContentDto>();
        // Categories/Tags/Cultures are legacy pipe-delimited compatibility strings whose
        // canonical source of truth is the ContentInCategory/Tag/Culture join tables. A
        // normal content create/update must never set them directly — only the dedicated
        // CreateContentCategories/Tags/Cultures commands (which replace both the join rows
        // and the compatibility string together, in one transaction) may.
        CreateMap<ContentDto, Content>()
            .ForMember(dest => dest.Categories, opt => opt.Ignore())
            .ForMember(dest => dest.Tags, opt => opt.Ignore())
            .ForMember(dest => dest.Cultures, opt => opt.Ignore())
            // FarsiContent isn't on ContentDto by design: ContentServices.Update maps onto the
            // already-loaded tracked entity so this map must not clear its existing value.
            .ForMember(dest => dest.FarsiContent, opt => opt.Ignore())
            .ForMember(dest => dest.Sections, opt => opt.Ignore())
            .ForMember(dest => dest.Images, opt => opt.Ignore())
            .ForMember(dest => dest.Metadata, opt => opt.Ignore())
            .ForMember(dest => dest.Application, opt => opt.Ignore());

        // SectionDto.SectionElements is populated manually (ContentServices.GetSections joins
        // sections with their elements outside AutoMapper), not from ContentSection.Elements -
        // the names don't even match, so this was never going to auto-map.
        CreateMap<ContentSection, SectionDto>()
            .ForMember(dest => dest.SectionElements, opt => opt.Ignore());
        CreateMap<SectionDto, ContentSection>()
            .ForMember(dest => dest.Elements, opt => opt.Ignore())
            .ForMember(dest => dest.Content, opt => opt.Ignore());

        CreateMap<SectionElement, SectionElementDto>();
        CreateMap<SectionElementDto, SectionElement>()
            .ForMember(dest => dest.Section, opt => opt.Ignore());

        CreateMap<ContentMetadata, ContentMetadataDto>();
        CreateMap<ContentMetadataDto, ContentMetadata>()
            .ForMember(dest => dest.Content, opt => opt.Ignore());

        CreateMap<ContentImage, ContentImageDto>();
        CreateMap<ContentImageDto, ContentImage>()
            .ForMember(dest => dest.Content, opt => opt.Ignore());

        CreateMap<Schema, SchemaDto>();
        CreateMap<SchemaDto, Schema>()
            .ForMember(dest => dest.Details, opt => opt.Ignore())
            .ForMember(dest => dest.Application, opt => opt.Ignore());

        CreateMap<SchemaDetails, SchemaDetailsDto>();
        CreateMap<SchemaDetailsDto, SchemaDetails>()
            .ForMember(dest => dest.Schema, opt => opt.Ignore());

        // AccessManagement
        CreateMap<EntityAccess, EntityAccessDto>();
        CreateMap<EntityAccessDto, EntityAccess>()
            .ForMember(dest => dest.SectorEntity, opt => opt.Ignore());

        CreateMap<Sector, SectorDto>();
        CreateMap<SectorDto, Sector>()
            .ForMember(dest => dest.Application, opt => opt.Ignore())
            .ForMember(dest => dest.SectorEntities, opt => opt.Ignore());

        CreateMap<SectorEntity, SectorEntityDto>();
        CreateMap<SectorEntityDto, SectorEntity>()
            .ForMember(dest => dest.Sector, opt => opt.Ignore())
            .ForMember(dest => dest.EntityAccesses, opt => opt.Ignore());

        // General
        CreateMap<Domains.Entities.General.Application, ApplicationDto>();
        CreateMap<ApplicationDto, Domains.Entities.General.Application>()
            .ForMember(dest => dest.Contents, opt => opt.Ignore())
            .ForMember(dest => dest.Tags, opt => opt.Ignore())
            .ForMember(dest => dest.Cultures, opt => opt.Ignore())
            .ForMember(dest => dest.SystemLogs, opt => opt.Ignore())
            .ForMember(dest => dest.Schemas, opt => opt.Ignore())
            .ForMember(dest => dest.Sliders, opt => opt.Ignore())
            .ForMember(dest => dest.ApplicationSettings, opt => opt.Ignore())
            .ForMember(dest => dest.SystemTypes, opt => opt.Ignore())
            .ForMember(dest => dest.Sectors, opt => opt.Ignore());

        CreateMap<ApplicationSetting, ApplicationSettingDto>();
        CreateMap<ApplicationSettingDto, ApplicationSetting>()
            .ForMember(dest => dest.Application, opt => opt.Ignore());

        CreateMap<UserInApplication, UserInApplicationDto>();

        CreateMap<Culture, CultureDto>();
        // Culture is a global lookup today, not scoped to an application anywhere (see
        // ICultureRepository) - ApplicationId is never set through this map, only ever left at
        // its default, so a caller can't accidentally pin a Culture to an application via the DTO.
        CreateMap<CultureDto, Culture>()
            .ForMember(dest => dest.ApplicationId, opt => opt.Ignore())
            .ForMember(dest => dest.Application, opt => opt.Ignore());

        CreateMap<SystemType, SystemTypeDto>();
        CreateMap<SystemTypeDto, SystemType>()
            .ForMember(dest => dest.Application, opt => opt.Ignore());

        CreateMap<Domains.Entities.General.Tag, TagDto>();
        CreateMap<TagDto, Domains.Entities.General.Tag>()
            .ForMember(dest => dest.Application, opt => opt.Ignore());

        // User management
        CreateMap<UserAttachment, UserAttachmentDto>();
        CreateMap<UserAttachmentDto, UserAttachment>();
    }
}
