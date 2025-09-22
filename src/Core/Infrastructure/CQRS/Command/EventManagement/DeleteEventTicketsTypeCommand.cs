using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record DeleteEventTicketsTypeCommand(int Id) : IRequest;
}