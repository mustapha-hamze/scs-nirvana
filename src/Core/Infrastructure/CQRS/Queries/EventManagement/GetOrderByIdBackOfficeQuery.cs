using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetOrderByIdBackOfficeQuery(int Id) : IRequest<TicketOrderDto>;
}