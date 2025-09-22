namespace Infrastructure.CQRS.Command.EventManagement;
public record AddTicketToBasketCommand(TicketBasketDto TicketBasketDto) : IRequest;