using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetUpcomingEventsQuery(int ApplicationId) : IRequest<List<EventDto>>;
}