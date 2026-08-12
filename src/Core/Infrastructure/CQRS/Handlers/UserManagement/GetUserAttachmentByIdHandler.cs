using AutoMapper;
using Domains.Entities.User;
using Infrastructure.CQRS.Queries.UserManagement;
using Infrastructure.Dto.UserManagementDtos;
using Infrastructure.Repository;

namespace Infrastructure.CQRS.Handlers.UserManagement;

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
