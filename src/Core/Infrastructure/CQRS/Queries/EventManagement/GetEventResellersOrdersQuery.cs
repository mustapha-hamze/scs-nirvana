using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetEventResellersOrdersQuery(int ApplicationId) : IRequest;
}