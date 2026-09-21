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
        var user = await _dbContext.Users.ToListAsync(cancellationToken);
        var filtered = email?.Length == 0
            ? user.Where(u => u.IsAdminUser)
            : user.Where(u => u.IsAdminUser == isAdminUser && u.Email.Contains(email));

        return _mapper.Map<List<UserDto>>(filtered.ToList());
    }

    public async Task<string> GetUserAccesses(string email, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        var userAccesses = await _dbContext.UserAccesses.FirstOrDefaultAsync(
            ua => ua.UserId == user.Id && ua.ApplicationId == user.CurrentApplicationId, cancellationToken
        );

        if (userAccesses == null)
            return "";

        return userAccesses.Access;
    }

    public async Task<string> GetUserAccesses(string email, int appId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
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

    public async Task SetCurrentApplicationId(string email, int appId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        user.CurrentApplicationId = appId;
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