using Infrastructure.Dto.UserManagementDtos;

namespace Infrastructure.CQRS.Command.EventManagement;
public record CreateUserAttachmentCommand(UserAttachmentDto UserAttachment) : IRequest;