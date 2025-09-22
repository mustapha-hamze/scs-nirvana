using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetEventTicketsByOrderItemIdQuery(int OrderItemId) : IRequest<List<string>>;
}