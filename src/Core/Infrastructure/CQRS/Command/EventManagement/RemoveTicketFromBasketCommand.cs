using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record RemoveTicketFromBasketCommand(int ItemId, string UserId) : IRequest;
}