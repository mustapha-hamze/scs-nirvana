using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetOrderItemsQuery(int OrderId) : IRequest<List<TicketOrderItemDto>>;
}