using Infrastructure.Dto.CMSDtos;
using MediatR;

namespace Infrastructure.CQRS.Queries.ContentManagement.Content
{
    public record GetContentQuery(int ApplicationId, int TypeId, int Id) : IRequest<ContentDto>;
}