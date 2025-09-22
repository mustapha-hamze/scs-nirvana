using Infrastructure.Dto.UserManagementDtos;

namespace Infrastructure.CQRS.Queries.UserManagement;

public record GetUserAttachmentsQuery(string UserId) : IRequest<List<UserAttachmentDto>>;