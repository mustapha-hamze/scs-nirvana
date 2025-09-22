using Infrastructure.Dto.UserManagementDtos;

namespace Infrastructure.CQRS.Queries.UserManagement;

public record GetUserAttachmentByIdQuery(int Id) : IRequest<UserAttachmentDto>;