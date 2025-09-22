using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record CanCreateOrderQuery(string UserId) : IRequest<bool>;
}