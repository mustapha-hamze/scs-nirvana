using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domains.Entities.General;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.GeneralRepository
{
    // Application is the tenant root, never a self-scoped exception - it deliberately does not
    // inherit the generic Repository<T> the way tenant-owned aggregates do, so root
    // administration (Create/Update/Delete/GetById below) can never pick up a capability added to
    // that shared base for tenant-scoped entities without a matching, reviewed change here.
    public class ApplicationRepository : global::Application.GeneralRepository.IApplicationRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public ApplicationRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<Domains.Entities.General.Application> Create(Domains.Entities.General.Application application)
        {
            _dbContext.Applications.Add(application);
            _dbContext.Entry(application).State = EntityState.Added;
            return Task.FromResult(application);
        }

        public Task<Domains.Entities.General.Application> Update(Domains.Entities.General.Application application)
        {
            _dbContext.Applications.Update(application);
            _dbContext.Entry(application).State = EntityState.Modified;
            return Task.FromResult(application);
        }

        public async Task Delete(int id, CancellationToken cancellationToken = default)
        {
            var application = await _dbContext.Applications.SingleAsync(a => a.Id == id, cancellationToken);
            _dbContext.Applications.Remove(application);
        }

        public async Task<Domains.Entities.General.Application> GetById(int id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Applications.AsNoTracking().SingleAsync(a => a.Id == id, cancellationToken);
        }

        public Task<List<Domains.Entities.General.Application>> List(CancellationToken cancellationToken = default)
        {
            return _dbContext.Applications
                .Where(a => !a.IsDeleted)
                .OrderBy(a => a.CreatedDT).ToListAsync(cancellationToken);
        }

        public async Task<List<UserInApplication>> GetUserApplications(string email, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserName == email, cancellationToken);

            return await _dbContext.UserInApplications
                .Where(u => u.UserId == user.Id && u.IsActive && !u.IsDeleted)
                .OrderByDescending(u => u.CreatedDT)
                .ToListAsync(cancellationToken);
        }

        // Idempotent: restores an existing soft-deleted membership row instead of inserting a
        // duplicate, since (UserId, ApplicationId) is unique - a second insert for the same pair
        // would violate that constraint once it's enforced at the database level.
        //
        // Administrative opt-out: this is the one place in the codebase that must see a
        // soft-deleted row on purpose (to restore it instead of colliding with the unique
        // index), so it explicitly bypasses the global soft-delete filter.
        public async Task AddUserToApplication(string userId, int applicationId, CancellationToken cancellationToken = default)
        {
            var existing = await _dbContext.UserInApplications
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(m => m.UserId == userId && m.ApplicationId == applicationId, cancellationToken);

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
            }, cancellationToken);
        }

        public async Task RemoveUserFromApplication(int relationId, int applicationId, CancellationToken cancellationToken = default)
        {
            var relation = await _dbContext.UserInApplications.SingleAsync(u => u.Id == relationId && u.ApplicationId == applicationId, cancellationToken);
            _dbContext.UserInApplications.Remove(relation);
        }

        public async Task<bool> ExistsActiveApplication(int applicationId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Applications.AnyAsync(a =>
                a.Id == applicationId && a.IsActive && !a.IsDeleted, cancellationToken);
        }

        public Task<List<ApplicationSetting>> GetApplicationSetting(int applicationId, int settingId = 0, CancellationToken cancellationToken = default)
        {
            if (settingId == 0)
                return _dbContext.ApplicationSettings
                    .Where(a => a.ApplicationId == applicationId && !a.IsDeleted)
                    .OrderByDescending(a => a.CreatedDT)
                    .ToListAsync(cancellationToken);
            else
                return _dbContext.ApplicationSettings
                    .Where(a => a.ApplicationId == applicationId && a.SettingId == settingId && !a.IsDeleted)
                    .OrderByDescending(a => a.CreatedDT)
                    .ToListAsync(cancellationToken);
        }

        public Task<ApplicationSetting> CreateApplicationSetting(ApplicationSetting setting)
        {
            _dbContext.ApplicationSettings.Add(setting);
            _dbContext.Entry(setting).State = EntityState.Added;
            return Task.FromResult(setting);
        }
    }
}