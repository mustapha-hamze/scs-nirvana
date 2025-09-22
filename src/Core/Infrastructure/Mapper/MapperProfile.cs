using AutoMapper;
using Domains.Entities.User;
using Infrastructure.Dto.UserManagementDtos;

namespace Infrastructure.Mapper
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<ContentAttachment, ContentAttachmentDto>();
            CreateMap<ContentAttachmentDto, ContentAttachment>();

            CreateMap<Category, CategoryDto>();
        }
    }
}