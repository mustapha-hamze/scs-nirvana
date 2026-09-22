using Application.Contracts.UserManagement;
using MediatR;

namespace Application.CQRS.Command.UserManagement;
public record CreateUserAttachmentCommand(UserAttachmentDto UserAttachment) : IRequest;
