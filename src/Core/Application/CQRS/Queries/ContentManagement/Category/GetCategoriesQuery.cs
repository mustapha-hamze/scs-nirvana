using System.Collections.Generic;
using Application.Contracts.CMS;
using MediatR;

namespace Application.CQRS.Queries.ContentManagement.Category;

public record GetCategoriesQuery(int ApplicationId, int ParentId = 0) : IRequest<List<CategoryDto>>;
