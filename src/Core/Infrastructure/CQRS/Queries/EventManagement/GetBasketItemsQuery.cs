using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetBasketItemsQuery(string UserId) : IRequest<List<TicketBasketDto>>;
}