using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record CanAddItemToBasketQuery(string UserId) : IRequest<bool>;
}