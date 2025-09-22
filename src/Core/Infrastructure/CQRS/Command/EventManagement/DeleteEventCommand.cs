using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record DeleteEventCommand(int EventId) : IRequest;
}