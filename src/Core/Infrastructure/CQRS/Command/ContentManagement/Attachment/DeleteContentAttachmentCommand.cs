namespace Infrastructure.CQRS.Command.ContentManagement.Attachment;
public record DeleteContentAttachmentCommand(int AttachmentId) : IRequest;