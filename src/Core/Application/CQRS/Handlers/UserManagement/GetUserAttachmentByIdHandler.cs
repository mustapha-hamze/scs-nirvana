using System.Threading;
using System.Threading.Tasks;
using Application.Contracts.UserManagement;
using Application.CQRS.Queries.UserManagement;
using Application.Repository;
using AutoMapper;
using Domains.Entities.User;
using MediatR;

namespace Application.CQRS.Handlers.UserManagement;

public class GetUserAttachmentByIdHandler : IRequestHandler<GetUserAttachmentByIdQuery, UserAttachmentDto>
{
    private readonly IRepository<UserAttachment> _repository;
    private readonly IMapper _mapper;

    public GetUserAttachmentByIdHandler(IRepository<UserAttachment> repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<UserAttachmentDto> Handle(GetUserAttachmentByIdQuery request, CancellationToken cancellationToken)
    {
        return _mapper.Map<UserAttachmentDto>(await _repository.GetById(request.Id));
    }
}
