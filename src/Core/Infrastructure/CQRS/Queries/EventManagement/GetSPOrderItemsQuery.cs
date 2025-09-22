using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Dto.EventManagement;
using MediatR;

namespace Infrastructure.CQRS.Queries.EventManagement
{
    public record GetSPOrderItemsQuery(int OrderId) : IRequest<List<SP_TicketOrderItemDto>>;
}