using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record CreateTicketOrderCommand(TicketOrderDto TicketOrderDto) : IRequest<int>;
}