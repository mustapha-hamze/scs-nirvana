using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetEventTicketsTypeByIdQuery(int EventTicketsTypeId) : IRequest<EventTicketsTypeDto>;
}