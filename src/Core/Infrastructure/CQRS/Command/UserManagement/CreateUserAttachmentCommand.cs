using Infrastructure.Dto.UserManagementDtos;

namespace Infrastructure.CQRS.Command.UserManagement;
public record CreateUserAttachmentCommand(UserAttachmentDto UserAttachment) : IRequest;