using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetEventByIdQuery(int EventId) : IRequest<EventDto>;
}