using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetOrdersByUsersIdQuery(string UserId) : IRequest<List<SP_TicketOrderDto>>;
}