using Application.Contracts.UserManagement;
using Application.UserManagementRepository;
using AutoMapper;
using Domains.Entities.User;
using Domains.Entities.General;

namespace Infrastructure.UserManagementRepository;
public class UserManagementRepository : IUserManagementRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMapper _mapper;

    public UserManagementRepository(ApplicationDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }
    public async Task<List<UserDto>> List(bool isAdminUser, string email = "", CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users.Where(u => u.IsAdminUser == isAdminUser);
        if (!string.IsNullOrEmpty(email))
            query = query.Where(u => u.Email.Contains(email));

        var users = await query.ToListAsync(cancellationToken);

        return _mapper.Map<List<UserDto>>(users);
    }

    public async Task<string> GetUserAccesses(string email, int appId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user == null)
            throw new KeyNotFoundException();

        var userAccesses = await _dbContext.UserAccesses.FirstOrDefaultAsync(
            ua => ua.UserId == user.Id && ua.ApplicationId == appId, cancellationToken
        );

        if (userAccesses == null)
            return "";

        return userAccesses.Access;
    }

    public async Task SetUserAccesses(string accesses, string userId, int appId, CancellationToken cancellationToken = default)
    {
        var userAccesses = await _dbContext.UserAccesses.FirstOrDefaultAsync(
            ua => ua.UserId == userId && ua.ApplicationId == appId, cancellationToken
        );

        if (userAccesses == null)
        {
            _dbContext.UserAccesses.Add(new UserAccess
            {
                Status = 1,
                IsActive = true,
                Access = accesses,
                UserId = userId,
                ApplicationId = appId
            });
        }
        else
        {
            userAccesses.Access = accesses;
        }
    }

    public async Task<UserDto> GetUserByEmailAddress(string email, CancellationToken cancellationToken = default)
    {
        return _mapper.Map<UserDto>(await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken));
    }

    public async Task<bool> HasActiveMembership(string userId, int applicationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserInApplications.AnyAsync(m =>
            m.UserId == userId && m.ApplicationId == applicationId && m.IsActive && !m.IsDeleted, cancellationToken);
    }
}