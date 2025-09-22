using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record IncreaseTicketTypeCountCommand(int TicketTypeId, int Count) : IRequest;
}