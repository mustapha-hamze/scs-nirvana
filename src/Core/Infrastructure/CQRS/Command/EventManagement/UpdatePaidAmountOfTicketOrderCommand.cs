using MediatR;

namespace Infrastructure.CQRS.Command.EventManagement
{
    public record UpdatePaidAmountOfTicketOrderCommand(int ApplicationId, int OrderId, decimal PaidAmount) : IRequest;
}