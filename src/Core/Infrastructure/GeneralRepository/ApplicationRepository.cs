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
        private readonly Repository<ApplicationSetting> _applicationSettingRepository;

        public ApplicationRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
            _applicationSettingRepository = new Repository<ApplicationSetting>(dbContext);
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
                .Where(u => u.UserId == user.Id && u.IsActive && !u.IsDeleted)
                .OrderByDescending(u => u.CreatedDT)
                .ToList();
        }

        // Idempotent: restores an existing soft-deleted membership row instead of inserting a
        // duplicate, since (UserId, ApplicationId) is unique - a second insert for the same pair
        // would violate that constraint once it's enforced at the database level.
        //
        // Administrative opt-out: this is the one place in the codebase that must see a
        // soft-deleted row on purpose (to restore it instead of colliding with the unique
        // index), so it explicitly bypasses the global soft-delete filter.
        public async Task AddUserToApplication(string userId, int applicationId)
        {
            var existing = await _dbContext.UserInApplications
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(m => m.UserId == userId && m.ApplicationId == applicationId);

            if (existing != null)
            {
                existing.Restore();
                return;
            }

            await _dbContext.UserInApplications.AddAsync(new UserInApplication
            {
                UserId = userId,
                ApplicationId = applicationId,
                IsActive = true
            });
        }

        public Task RemoveUserFromApplication(int relationId, int applicationId)
        {
            var relation = _dbContext.UserInApplications.Single(u => u.Id == relationId && u.ApplicationId == applicationId);
            _dbContext.UserInApplications.Remove(relation);

            return Task.CompletedTask;
        }

        public async Task<bool> ExistsActiveApplication(int applicationId)
        {
            return await _dbContext.Applications.AnyAsync(a =>
                a.Id == applicationId && a.IsActive && !a.IsDeleted);
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

        public Task<ApplicationSetting> CreateApplicationSetting(ApplicationSetting setting)
        {
            return _applicationSettingRepository.Create(setting);
        }
    }
}