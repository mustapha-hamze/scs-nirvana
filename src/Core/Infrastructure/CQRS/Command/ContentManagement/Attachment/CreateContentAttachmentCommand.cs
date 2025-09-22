using Infrastructure.Dto.CMSDtos;

namespace Infrastructure.CQRS.Command.ContentManagement.Attachment;
public record CreateContentAttachmentCommand(ContentAttachmentDto AttachmentDto) : IRequest;