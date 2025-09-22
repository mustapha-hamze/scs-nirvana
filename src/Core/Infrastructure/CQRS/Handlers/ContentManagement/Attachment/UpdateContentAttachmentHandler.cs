namespace Infrastructure.CQRS.Handlers.ContentManagement.Attachment;
public class UpdateContentAttachmentHandler : IRequestHandler<UpdateContentAttachmentCommand, Unit>
{
    private readonly IRepository<ContentAttachment> _repository;
    public UpdateContentAttachmentHandler(IRepository<ContentAttachment> repository)
    {
        _repository = repository;
    }

    public Task<Unit> Handle(UpdateContentAttachmentCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}