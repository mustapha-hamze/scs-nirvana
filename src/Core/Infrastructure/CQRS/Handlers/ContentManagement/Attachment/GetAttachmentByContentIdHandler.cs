using AutoMapper;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.CQRS.Handlers.ContentManagement.Attachment;
public class GetAttachmentByContentIdHandler : IRequestHandler<GetAttachmentByContentIdQuery, List<ContentAttachmentDto>>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetAttachmentByContentIdHandler(ApplicationDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }
    public async Task<List<ContentAttachmentDto>> Handle(GetAttachmentByContentIdQuery request, CancellationToken cancellationToken)
    {
        var attachments = _mapper.Map<List<ContentAttachmentDto>>(await _dbContext.ContentAttachments.Where(ca => ca.ContentId == request.ContentId).ToListAsync());
        return attachments;
    }
}