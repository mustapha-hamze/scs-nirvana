namespace Infrastructure.CQRS.Command.EventManagement;
public record CreateEventCommand(EventDto Event) : IRequest<int>;