using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record DecreaseTicketTypeCountCommand(int TicketTypeId, int Count) : IRequest;
}