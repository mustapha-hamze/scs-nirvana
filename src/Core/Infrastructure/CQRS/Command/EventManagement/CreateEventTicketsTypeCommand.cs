using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record CreateEventTicketsTypeCommand(EventTicketsTypeDto EventTicketsType) : IRequest;
}