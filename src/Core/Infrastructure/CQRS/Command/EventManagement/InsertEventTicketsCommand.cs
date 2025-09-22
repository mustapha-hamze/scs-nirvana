using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record InsertEventTicketsCommand(List<EventTicketDto> Tickets) : IRequest;
}