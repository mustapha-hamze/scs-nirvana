using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Contracts.CMS;
using Application.CQRS.Queries.ContentManagement.Category;
using Application.UseCases.CMSServices;
using MediatR;

namespace Application.CQRS.Handlers.ContentManagement.Category;

// Thin adapter over ICategoryServices so the repository/mapping logic for category reads has
// one implementation; BackOffice controllers call ICategoryServices directly (no IMediator
// there), while the Api endpoint goes through MediatR - both now resolve to the same source.
public class GetCategoriesHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
{
    private readonly ICategoryServices _categoryServices;

    public GetCategoriesHandler(ICategoryServices categoryServices)
    {
        _categoryServices = categoryServices;
    }

    public Task<List<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = _categoryServices.List(request.ApplicationId)
            .Where(c => c.ParentId == request.ParentId)
            .ToList();

        return Task.FromResult(categories);
    }
}
