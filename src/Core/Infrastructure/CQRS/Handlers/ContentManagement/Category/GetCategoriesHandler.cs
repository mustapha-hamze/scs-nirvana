using AutoMapper;
using Infrastructure.CQRS.Queries.ContentManagement.Category;
using Infrastructure.Data;
using Infrastructure.Dto.CMSDtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.CQRS.Handlers.ContentManagement.Category;

public class GetCategoriesHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMapper _mapper;
    public GetCategoriesHandler(ApplicationDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }
    public async Task<List<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories.Where(c => c.ParentId == request.ParentId && c.ApplicationId == request.ApplicationId && !c.IsDeleted).ToListAsync(cancellationToken);

        return _mapper.Map<List<CategoryDto>>(categories);
    }
}
