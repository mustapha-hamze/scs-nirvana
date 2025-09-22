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
    public class SliderRepository : Repository<Slider>, ISliderRepository
    {
        // fields
        private readonly ApplicationDbContext _dbContext;

        // constructor
        public SliderRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public List<Slider> GetSliders(int applicationId)
        {
            return _dbContext.Sliders.Where(s => s.ApplicationId == applicationId).ToList();
        }

        public List<SliderItem> GetSliderItems(int sliderId)
        {
            return _dbContext.SliderItems.Where(si => si.SliderId == sliderId && !si.IsDeleted).ToList();
        }

        public async Task<SliderItem> GetSliderItem(int sliderItemId)
        {
            return await _dbContext.SliderItems.SingleAsync(si => si.Id == sliderItemId);
        }

        public async Task<SliderItem> CreateSliderItem(SliderItem sliderItem)
        {
            sliderItem.CreatedDT = DateTime.Now;
            sliderItem.UpdatedDT = DateTime.Now;
            sliderItem.IsDeleted = false;
            sliderItem.IsActive = true;

            _dbContext.SliderItems.Add(sliderItem);
            await _dbContext.SaveChangesAsync();

            return sliderItem;
        }
        public async Task DeactiveSliderItem(int sliderItemId)
        {
            var sliderItem = await _dbContext.SliderItems.SingleAsync(si => si.Id == sliderItemId);
            sliderItem.IsActive = false;
            _dbContext.Entry(sliderItem).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync();
        }

        public async Task ActiveSliderItem(int sliderItemId)
        {
            var sliderItem = await _dbContext.SliderItems.SingleAsync(si => si.Id == sliderItemId);
            sliderItem.IsActive = true;
            _dbContext.Entry(sliderItem).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteSliderItem(int sliderItemId)
        {
            var sliderItem = await _dbContext.SliderItems.SingleAsync(si => si.Id == sliderItemId);
            sliderItem.IsDeleted = true;
            _dbContext.Entry(sliderItem).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync();
        }

        public Slider GetSliderWithItems(int sliderId)
        {
            var result = _dbContext.Sliders.Where(s => s.Id == sliderId)
                .Include(s => s.SliderItems.Where(si => !si.IsDeleted && si.IsActive))
                .OrderByDescending(s => s.CreatedDT).ToList();

            if (result.Any())
                return result[0];

            return new Slider();
        }

        public async Task<SliderItem> UpdateSliderItem(SliderItem sliderItem)
        {
            sliderItem.UpdatedDT = DateTime.Now;
            _dbContext.SliderItems.Update(sliderItem);
            _dbContext.Entry(sliderItem).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync();

            return sliderItem;
        }
    }
}