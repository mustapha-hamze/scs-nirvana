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

        public List<Domains.Entities.CustomModule.Slider> GetSliders(int applicationId)
        {
            return _dbContext.Sliders.Where(s => s.ApplicationId == applicationId && !s.IsDeleted).ToList();
        }

        public async Task<Domains.Entities.CustomModule.Slider> GetByIdForApplication(int id, int applicationId)
        {
            return await _dbContext.Sliders.AsNoTracking()
                .SingleAsync(s => s.Id == id && s.ApplicationId == applicationId && !s.IsDeleted);
        }

        public List<SliderItem> GetSliderItems(int sliderId, int applicationId)
        {
            return _dbContext.SliderItems
                .Where(si => si.SliderId == sliderId && !si.IsDeleted
                    && si.Slider.ApplicationId == applicationId && !si.Slider.IsDeleted)
                .ToList();
        }

        public async Task<SliderItem> GetItemForApplication(int sliderItemId, int applicationId)
        {
            return await _dbContext.SliderItems.AsNoTracking()
                .SingleAsync(si => si.Id == sliderItemId && !si.IsDeleted
                    && si.Slider.ApplicationId == applicationId && !si.Slider.IsDeleted);
        }

        public Task<SliderItem> CreateSliderItem(SliderItem sliderItem)
        {
            sliderItem.IsActive = true;

            _dbContext.SliderItems.Add(sliderItem);

            return Task.FromResult(sliderItem);
        }
        public async Task DeactiveSliderItem(int sliderItemId)
        {
            var sliderItem = await _dbContext.SliderItems.SingleAsync(si => si.Id == sliderItemId);
            sliderItem.IsActive = false;
            _dbContext.Entry(sliderItem).State = EntityState.Modified;
        }

        public async Task ActiveSliderItem(int sliderItemId)
        {
            var sliderItem = await _dbContext.SliderItems.SingleAsync(si => si.Id == sliderItemId);
            sliderItem.IsActive = true;
            _dbContext.Entry(sliderItem).State = EntityState.Modified;
        }

        public async Task DeleteSliderItem(int sliderItemId)
        {
            var sliderItem = await _dbContext.SliderItems.SingleAsync(si => si.Id == sliderItemId);
            _dbContext.SliderItems.Remove(sliderItem);
        }

        public Domains.Entities.CustomModule.Slider GetSliderWithItems(int sliderId, int applicationId)
        {
            var result = _dbContext.Sliders
                .Where(s => s.Id == sliderId && s.ApplicationId == applicationId && !s.IsDeleted)
                .Include(s => s.SliderItems.Where(si => !si.IsDeleted && si.IsActive))
                .OrderByDescending(s => s.CreatedDT).ToList();

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