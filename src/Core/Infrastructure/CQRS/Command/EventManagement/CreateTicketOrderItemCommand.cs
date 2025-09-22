using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record CreateTicketOrderItemCommand(TicketOrderItemDto TicketOrderItemDto) : IRequest;
}