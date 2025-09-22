using Domains.Entities.User;
using Domains.Entities.General;

namespace Infrastructure.UserManagementRepository;
public class UserManagementRepository : IUserManagementRepository
{
    private readonly ApplicationDbContext _dbContext;

    public UserManagementRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public List<ApplicationUser> List(bool isAdminUser, string email = "")
    {
        var user = _dbContext.Users.ToList();
        if (email?.Length == 0)
            return user.Where(u => u.IsAdminUser).ToList();
        else
            return user.Where(u => u.IsAdminUser == isAdminUser && u.Email.Contains(email)).ToList();
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
            await _dbContext.SaveChangesAsync();
        }
        else
        {
            userAccesses.Access = accesses;
            await _dbContext.SaveChangesAsync();
        }

    }

    public async Task SetCurrentApplicationId(string email, int appId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        user.CurrentApplicationId = appId;
        await _dbContext.SaveChangesAsync();
    }

    public ApplicationUser GetUserByEmailAddress(string email)
    {
        return _dbContext.Users.FirstOrDefault(u => u.Email == email);
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