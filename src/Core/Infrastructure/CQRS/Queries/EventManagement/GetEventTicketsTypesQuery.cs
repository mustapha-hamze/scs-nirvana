using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetEventTicketsTypesQuery(int EventId) : IRequest<List<EventTicketsTypeDto>>;
}