using System.Threading.Tasks;
using Application.Contracts.UserManagement;
using Application.CQRS.Queries.UserManagement;
using Application.UserManagementRepository;
using AutoMapper;
using MediatR;

namespace Application.CQRS.Handlers.UserManagement;

public class GetUserAttachmentByIdHandler : IRequestHandler<GetUserAttachmentByIdQuery, UserAttachmentDto>
{
    private readonly IUserAttachmentRepository _repository;
    private readonly IMapper _mapper;

    public GetUserAttachmentByIdHandler(IUserAttachmentRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<UserAttachmentDto> Handle(GetUserAttachmentByIdQuery request, CancellationToken cancellationToken)
    {
        var attachment = await _repository.GetByIdForUser(request.Id, request.UserId, cancellationToken);
        return _mapper.Map<UserAttachmentDto>(attachment);
    }
}
