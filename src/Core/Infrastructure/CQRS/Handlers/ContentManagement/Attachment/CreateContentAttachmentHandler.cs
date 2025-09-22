using AutoMapper;
using Domains.Entities.ContentManagement;
using Infrastructure.CQRS.Command.ContentManagement.Attachment;
using Infrastructure.Repository;

namespace Infrastructure.CQRS.Handlers.ContentManagement.Attachment;
public class CreateContentAttachmentHandler : IRequestHandler<CreateContentAttachmentCommand, Unit>
{
    private readonly IRepository<ContentAttachment> _repository;
    private readonly IMapper _mapper;
    public CreateContentAttachmentHandler(IRepository<ContentAttachment> repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }
    public async Task<Unit> Handle(CreateContentAttachmentCommand request, CancellationToken cancellationToken)
    {
        await _repository.Create(_mapper.Map<ContentAttachment>(request.AttachmentDto));
        return Unit.Value;
    }
}