using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record ChangeOrderStatusCommand(int OrderId, int Status, string UserId) : IRequest;
}