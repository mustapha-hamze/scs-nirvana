using System.Threading.Tasks;
using Application.CQRS.Command.UserManagement;
using Application.Repository;
using Application.UnitOfWork;
using AutoMapper;
using Domains.Entities.User;
using MediatR;

namespace Application.CQRS.Handlers.UserManagement;

public class CreateUserAttachmentHandler : IRequestHandler<CreateUserAttachmentCommand, Unit>
{
    private readonly IRepository<UserAttachment> _repository;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserAttachmentHandler(IRepository<UserAttachment> repository, IMapper mapper, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(CreateUserAttachmentCommand request, CancellationToken cancellationToken)
    {
        await _repository.Create(_mapper.Map<UserAttachment>(request.UserAttachment));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
