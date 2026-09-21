using AutoMapper;
using Domains.Entities.ContentManagement;
using Domains.Entities.User;
using Infrastructure.Identity;
using Infrastructure.Dto.CMSDtos;

namespace Infrastructure.Mapper
{
    // Infrastructure-only maps: Identity types and Infrastructure-namespaced DTOs that Application
    // cannot reference. Domain <-> Application.Contracts maps live in
    // Application.Mapper.ApplicationMapperProfile instead.
    //
    // AssertConfigurationIsValid() (Core.Tests.Architecture.MappingConfigurationTests) enforces
    // every map here.
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<ContentAttachment, ContentAttachmentDto>();
            CreateMap<ContentAttachmentDto, ContentAttachment>()
                .ForMember(dest => dest.Content, opt => opt.Ignore());

            // UserDto.Accesses is resolved separately per application via
            // IUserManagementServices.GetUserAccesses - ApplicationUser has no single Accesses
            // value to map (per-application access strings live in UserAccess rows instead).
            CreateMap<ApplicationUser, UserDto>()
                .ForMember(dest => dest.Accesses, opt => opt.Ignore());
        }
    }
}
