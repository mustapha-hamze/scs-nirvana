using System.Collections.Generic;
using System.Threading.Tasks;
using Application.CQRS.Command.UserManagement;
using Application.UnitOfWork;
using Application.UserManagementRepository;
using AutoMapper;
using Domains.Entities.User;
using MediatR;

namespace Application.CQRS.Handlers.UserManagement;

public class CreateUserAttachmentHandler : IRequestHandler<CreateUserAttachmentCommand, Unit>
{
    private readonly IUserAttachmentRepository _repository;
    private readonly IUserManagementRepository _userManagementRepository;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserAttachmentHandler(IUserAttachmentRepository repository, IUserManagementRepository userManagementRepository,
        IMapper mapper, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _userManagementRepository = userManagementRepository;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(CreateUserAttachmentCommand request, CancellationToken cancellationToken)
    {
        if (!await _userManagementRepository.UserExists(request.UserAttachment.UserId, cancellationToken))
            throw new KeyNotFoundException();

        await _repository.Create(_mapper.Map<UserAttachment>(request.UserAttachment));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
