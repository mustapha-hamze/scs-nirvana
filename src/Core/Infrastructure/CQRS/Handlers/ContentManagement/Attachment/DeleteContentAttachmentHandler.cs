namespace Infrastructure.CQRS.Handlers.ContentManagement.Attachment;
public class DeleteContentAttachmentHandler : IRequestHandler<DeleteContentAttachmentCommand, Unit>
{
    private readonly IRepository<ContentAttachment> _repository;
    public DeleteContentAttachmentHandler(IRepository<ContentAttachment> repository)
    {
        _repository = repository;
    }

    public Task<Unit> Handle(DeleteContentAttachmentCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}