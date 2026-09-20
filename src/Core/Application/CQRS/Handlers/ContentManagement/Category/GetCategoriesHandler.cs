using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.CMSRepository;
using Application.Contracts.CMS;
using Application.CQRS.Queries.ContentManagement.Category;
using AutoMapper;
using MediatR;

namespace Application.CQRS.Handlers.ContentManagement.Category;

public class GetCategoriesHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IMapper _mapper;

    public GetCategoriesHandler(ICategoryRepository categoryRepository, IMapper mapper)
    {
        _categoryRepository = categoryRepository;
        _mapper = mapper;
    }

    public Task<List<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = _categoryRepository.List(request.ApplicationId)
            .Where(c => c.ParentId == request.ParentId)
            .ToList();

        return Task.FromResult(_mapper.Map<List<CategoryDto>>(categories));
    }
}
