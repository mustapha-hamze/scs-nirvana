using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Contracts.UserManagement;
using Application.CQRS.Queries.UserManagement;
using Application.UserManagementRepository;
using AutoMapper;
using MediatR;

namespace Application.CQRS.Handlers.UserManagement;

public class GetUserAttachmentsHandler : IRequestHandler<GetUserAttachmentsQuery, List<UserAttachmentDto>>
{
    private readonly IUserAttachmentRepository _repository;
    private readonly IMapper _mapper;

    public GetUserAttachmentsHandler(IUserAttachmentRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<List<UserAttachmentDto>> Handle(GetUserAttachmentsQuery request, CancellationToken cancellationToken)
    {
        var attachments = await _repository.List(request.UserId);
        return _mapper.Map<List<UserAttachmentDto>>(attachments);
    }
}
