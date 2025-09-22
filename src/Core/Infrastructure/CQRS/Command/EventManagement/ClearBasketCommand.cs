using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record ClearBasketCommand(string UserId) : IRequest;
}