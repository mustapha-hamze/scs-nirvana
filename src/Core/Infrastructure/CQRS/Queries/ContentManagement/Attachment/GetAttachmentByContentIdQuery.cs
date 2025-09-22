namespace Infrastructure.CQRS.Queries.ContentManagement.Attachment;
public record GetAttachmentByContentIdQuery(int ContentId) : IRequest<List<ContentAttachmentDto>>;