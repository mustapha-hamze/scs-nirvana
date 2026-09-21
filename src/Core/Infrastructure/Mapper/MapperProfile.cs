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
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<ContentAttachment, ContentAttachmentDto>();
            CreateMap<ContentAttachmentDto, ContentAttachment>();

            CreateMap<ApplicationUser, UserDto>();
        }
    }
}
