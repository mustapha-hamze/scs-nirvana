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

        public const string ConnectionString = "Data Source=A2NWPLSK14SQL-v02.shr.prod.iad2.secureserver.net;Initial Catalog=ph19571531397_ShakibGroup_DB;User=sa_shakib_group;Password=Shakib2022%^mos;Trust Server Certificate=True;";

        public Repository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
            _entities = dbContext.Set<T>();
        }

        public async Task<T> Create(T entity)
        {
            //entity.Id = Guid.NewGuid().ToString();
            entity.IsDeleted = false;
            entity.CreatedDT = DateTime.Now;
            entity.UpdatedDT = DateTime.Now;

            _entities.Add(entity);
            _dbContext.Entry(entity).State = EntityState.Added;
            await _dbContext.SaveChangesAsync();
            return entity;
        }

        public async Task Delete(int id)
        {
            var entity = _entities.Single(e => e.Id == id);
            entity.IsDeleted = true;
            entity.UpdatedDT = DateTime.Now;
            _dbContext.Entry(entity).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync();
        }

        public async Task<T> GetById(int id)
        {
            return await _entities.AsNoTracking().SingleAsync(s => s.Id == id);
        }

        public async Task<T> Update(T entity)
        {
            entity.UpdatedDT = DateTime.Now;

            _entities.Update(entity);
            _dbContext.Entry(entity).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync();
            return entity;
        }
    }
}
