using Domains.Entities.User;

namespace Infrastructure.CQRS.Queries.UserManagement;
public record GetEventResellersQuery(string Key) : IRequest<List<ApplicationUser>>;