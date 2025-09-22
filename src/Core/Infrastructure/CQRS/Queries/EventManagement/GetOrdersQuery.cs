using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetOrdersQuery(int ApplicationId, int Status, int UserType) : IRequest<List<TicketOrderBackOfficeDto>>;
}