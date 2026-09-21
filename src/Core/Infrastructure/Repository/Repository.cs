using Domains.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repository
{
    public class Repository<T> : IRepository<T> where T : BaseEntity
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly DbSet<T> _entities;

        public Repository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
            _entities = dbContext.Set<T>();
        }

        // Stages the change only; the calling use case owns SaveChangesAsync/ExecuteInTransactionAsync.
        // CreatedDT/UpdatedDT/IsDeleted are not set here - ApplicationDbContext stamps them
        // centrally (UTC, via TimeProvider) at SaveChanges time for every BaseEntity.
        public Task<T> Create(T entity)
        {
            _entities.Add(entity);
            _dbContext.Entry(entity).State = EntityState.Added;
            return Task.FromResult(entity);
        }

        // A physical Remove(); ApplicationDbContext converts this into a soft delete
        // (IsDeleted = true, State = Modified) at SaveChanges time - see ApplyLifecyclePolicy.
        public async Task Delete(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _entities.SingleAsync(e => e.Id == id, cancellationToken);
            _entities.Remove(entity);
        }

        public async Task<T> GetById(int id, CancellationToken cancellationToken = default)
        {
            return await _entities.AsNoTracking().SingleAsync(s => s.Id == id, cancellationToken);
        }

        public Task<T> Update(T entity)
        {
            _entities.Update(entity);
            _dbContext.Entry(entity).State = EntityState.Modified;
            return Task.FromResult(entity);
        }
    }
}
