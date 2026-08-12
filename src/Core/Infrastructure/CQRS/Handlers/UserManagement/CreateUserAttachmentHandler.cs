using AutoMapper;
using Domains.Entities.User;
using Infrastructure.CQRS.Command.UserManagement;
using Infrastructure.Repository;

namespace Infrastructure.CQRS.Handlers.UserManagement;

public class CreateUserAttachmentHandler : IRequestHandler<CreateUserAttachmentCommand, Unit>
{
    private readonly IRepository<UserAttachment> _repository;
    private readonly IMapper _mapper;

    public CreateUserAttachmentHandler(IRepository<UserAttachment> repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Unit> Handle(CreateUserAttachmentCommand request, CancellationToken cancellationToken)
    {
        await _repository.Create(_mapper.Map<UserAttachment>(request.UserAttachment));
        return Unit.Value;
    }
}
