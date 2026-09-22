using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domains.Entities.CustomModule;
using Infrastructure.Data;
using Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.SCMRepository
{
    public class SliderRepository : Repository<Domains.Entities.CustomModule.Slider>, global::Application.SCMRepository.ISliderRepository
    {
        // fields
        private readonly ApplicationDbContext _dbContext;

        // constructor
        public SliderRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<List<Domains.Entities.CustomModule.Slider>> GetSliders(int applicationId, CancellationToken cancellationToken = default)
        {
            return _dbContext.Sliders.Where(s => s.ApplicationId == applicationId && !s.IsDeleted).ToListAsync(cancellationToken);
        }

        public async Task<Domains.Entities.CustomModule.Slider> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Sliders.AsNoTracking()
                .SingleAsync(s => s.Id == id && s.ApplicationId == applicationId && !s.IsDeleted, cancellationToken);
        }

        public Task<List<SliderItem>> GetSliderItems(int sliderId, int applicationId, CancellationToken cancellationToken = default)
        {
            return _dbContext.SliderItems
                .Where(si => si.SliderId == sliderId && !si.IsDeleted
                    && si.Slider.ApplicationId == applicationId && !si.Slider.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<SliderItem> GetItemForApplication(int sliderItemId, int applicationId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.SliderItems.AsNoTracking()
                .SingleAsync(si => si.Id == sliderItemId && !si.IsDeleted
                    && si.Slider.ApplicationId == applicationId && !si.Slider.IsDeleted, cancellationToken);
        }

        public Task<SliderItem> CreateSliderItem(SliderItem sliderItem)
        {
            sliderItem.IsActive = true;

            _dbContext.SliderItems.Add(sliderItem);

            return Task.FromResult(sliderItem);
        }
        public async Task DeactiveSliderItem(int sliderItemId, CancellationToken cancellationToken = default)
        {
            var sliderItem = await _dbContext.SliderItems.SingleAsync(si => si.Id == sliderItemId, cancellationToken);
            sliderItem.IsActive = false;
            _dbContext.Entry(sliderItem).State = EntityState.Modified;
        }

        public async Task ActiveSliderItem(int sliderItemId, CancellationToken cancellationToken = default)
        {
            var sliderItem = await _dbContext.SliderItems.SingleAsync(si => si.Id == sliderItemId, cancellationToken);
            sliderItem.IsActive = true;
            _dbContext.Entry(sliderItem).State = EntityState.Modified;
        }

        public async Task DeleteSliderItem(int sliderItemId, CancellationToken cancellationToken = default)
        {
            var sliderItem = await _dbContext.SliderItems.SingleAsync(si => si.Id == sliderItemId, cancellationToken);
            _dbContext.SliderItems.Remove(sliderItem);
        }

        public async Task<Domains.Entities.CustomModule.Slider> GetSliderWithItems(int sliderId, int applicationId, CancellationToken cancellationToken = default)
        {
            var result = await _dbContext.Sliders
                .Where(s => s.Id == sliderId && s.ApplicationId == applicationId && !s.IsDeleted)
                .Include(s => s.SliderItems.Where(si => !si.IsDeleted && si.IsActive))
                .OrderByDescending(s => s.CreatedDT).ToListAsync(cancellationToken);

            if (result.Any())
                return result[0];

            return new Domains.Entities.CustomModule.Slider();
        }

        public Task<SliderItem> UpdateSliderItem(SliderItem sliderItem)
        {
            _dbContext.SliderItems.Update(sliderItem);
            _dbContext.Entry(sliderItem).State = EntityState.Modified;

            return Task.FromResult(sliderItem);
        }
    }
}