using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domains.Entities.General;
using Infrastructure.Data;
using Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.GeneralRepository
{
    public class ApplicationRepository : Repository<Domains.Entities.General.Application>, global::Application.GeneralRepository.IApplicationRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public ApplicationRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public List<Domains.Entities.General.Application> List()
        {
            return _dbContext.Applications
                .Where(a => !a.IsDeleted)
                .OrderBy(a => a.CreatedDT).ToList();
        }

        public async Task<List<UserInApplication>> GetUserApplications(string email)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserName == email);

            return _dbContext.UserInApplications
                .Where(u => u.UserId == user.Id && !u.IsDeleted)
                .OrderByDescending(u => u.CreatedDT)
                .ToList();
        }

        public async Task AddUserToApplication(string userId, int applicationId)
        {
            await _dbContext.UserInApplications.AddAsync(new UserInApplication
            {
                UserId = userId,
                ApplicationId = applicationId,
                IsActive = true,
                UpdatedDT = DateTime.Now,
                CreatedDT = DateTime.Now
            });

            await _dbContext.SaveChangesAsync();
        }

        public async Task RemoveUserFromApplication(int relationId)
        {
            var relation = _dbContext.UserInApplications.Single(u => u.Id == relationId);
            relation.IsDeleted = true;
            relation.UpdatedDT = DateTime.Now;

            await _dbContext.SaveChangesAsync();
        }

        public List<ApplicationSetting> GetApplicationSetting(int applicationId, int settingId = 0)
        {
            if (settingId == 0)
                return _dbContext.ApplicationSettings
                    .Where(a => a.ApplicationId == applicationId && !a.IsDeleted)
                    .OrderByDescending(a => a.CreatedDT)
                    .ToList();
            else
                return _dbContext.ApplicationSettings
                    .Where(a => a.ApplicationId == applicationId && a.SettingId == settingId && !a.IsDeleted)
                    .OrderByDescending(a => a.CreatedDT)
                    .ToList();
        }
    }
}