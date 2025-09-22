using Infrastructure.Dto.CMSDtos;
using MediatR;

namespace Infrastructure.CQRS.Queries.ContentManagement.Category;

public record GetCategoriesQuery(int ApplicationId, int ParentId = 0):IRequest<List<CategoryDto>>;
