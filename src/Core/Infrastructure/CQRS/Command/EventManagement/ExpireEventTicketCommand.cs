using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record ExpireEventTicketCommand(string PublicId) : IRequest;
}