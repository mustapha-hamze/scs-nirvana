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
    public List<UserDto> List(bool isAdminUser, string email = "")
    {
        var user = _dbContext.Users.ToList();
        var filtered = email?.Length == 0
            ? user.Where(u => u.IsAdminUser)
            : user.Where(u => u.IsAdminUser == isAdminUser && u.Email.Contains(email));

        return _mapper.Map<List<UserDto>>(filtered.ToList());
    }

    public async Task<string> GetUserAccesses(string email)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        var userAccesses = await _dbContext.UserAccesses.FirstOrDefaultAsync(
            ua => ua.UserId == user.Id && ua.ApplicationId == user.CurrentApplicationId
        );

        if (userAccesses == null)
            return "";

        return userAccesses.Access;
    }

    public async Task<string> GetUserAccesses(string email, int appId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        var userAccesses = await _dbContext.UserAccesses.FirstOrDefaultAsync(
            ua => ua.UserId == user.Id && ua.ApplicationId == appId
        );

        if (userAccesses == null)
            return "";

        return userAccesses.Access;
    }

    public async Task SetUserAccesses(string accesses, string userId, int appId)
    {
        var userAccesses = await _dbContext.UserAccesses.FirstOrDefaultAsync(
            ua => ua.UserId == userId && ua.ApplicationId == appId
        );

        if (userAccesses == null)
        {
            _dbContext.UserAccesses.Add(new UserAccess
            {
                Status = 1,
                IsActive = true,
                Access = accesses,
                UserId = userId,
                ApplicationId = appId,
                CreatedDT = DateTime.Now,
                UpdatedDT = DateTime.Now
            });
        }
        else
        {
            userAccesses.Access = accesses;
        }
    }

    public async Task SetCurrentApplicationId(string email, int appId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        user.CurrentApplicationId = appId;
    }

    public UserDto GetUserByEmailAddress(string email)
    {
        return _mapper.Map<UserDto>(_dbContext.Users.FirstOrDefault(u => u.Email == email));
    }

    public async Task<bool> HasActiveMembership(string userId, int applicationId)
    {
        return await _dbContext.UserInApplications.AnyAsync(m =>
            m.UserId == userId && m.ApplicationId == applicationId && m.IsActive && !m.IsDeleted);
    }

    // public async Task CreateUserAttachment(UserAttachment userAttachment)
    // {
    //     throw new NotImplementedException();
    // }

    // public async Task<List<UserAttachment>> GetUserAttachments(string userId)
    // {
    //     throw new NotImplementedException();
    // }
}