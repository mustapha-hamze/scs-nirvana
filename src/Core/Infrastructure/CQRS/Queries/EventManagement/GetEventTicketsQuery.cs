using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetEventTicketsQuery(int OrderItemId) : IRequest<List<EventTicketDto>>;
}