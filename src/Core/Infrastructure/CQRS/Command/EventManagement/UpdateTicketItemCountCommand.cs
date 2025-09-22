using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record UpdateTicketItemCountCommand(int ItemId, int Count) : IRequest;
}