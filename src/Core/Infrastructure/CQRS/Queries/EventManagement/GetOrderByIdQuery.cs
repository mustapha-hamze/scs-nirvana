using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetOrderByIdQuery(int Id, string UserId) : IRequest<TicketOrderDto>;
}