using AutoMapper;
using Infrastructure.CQRS.Queries.UserManagement;
using Infrastructure.Data;
using Infrastructure.Dto.UserManagementDtos;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.CQRS.Handlers.UserManagement;

public class GetUserAttachmentsHandler : IRequestHandler<GetUserAttachmentsQuery, List<UserAttachmentDto>>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetUserAttachmentsHandler(ApplicationDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<List<UserAttachmentDto>> Handle(GetUserAttachmentsQuery request, CancellationToken cancellationToken)
    {
        var attachments = await _dbContext.UserAttachments
            .Where(a => a.UserId == request.UserId && !a.IsDeleted)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<UserAttachmentDto>>(attachments);
    }
}
