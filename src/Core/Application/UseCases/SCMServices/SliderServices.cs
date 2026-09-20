using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domains.Entities.CustomModule;
using Application.UnitOfWork;

namespace Services.SCMServices
{
    public class SliderServices : ISliderServices
    {
        private readonly global::Application.SCMRepository.ISliderRepository _sliderRepository;
        private readonly IUnitOfWork _unitOfWork;
        public SliderServices(global::Application.SCMRepository.ISliderRepository sliderRepository, IUnitOfWork unitOfWork)
        {
            _sliderRepository = sliderRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Slider> Create(Slider slider)
        {
            var created = await _sliderRepository.Create(slider);
            await _unitOfWork.SaveChangesAsync();
            return created;
        }

        public async Task<SliderItem> CreateSliderItem(SliderItem sliderItem)
        {
            var created = await _sliderRepository.CreateSliderItem(sliderItem);
            await _unitOfWork.SaveChangesAsync();
            return created;
        }

        public async Task DeactiveSliderItem(int sliderItemId)
        {
            await _sliderRepository.DeactiveSliderItem(sliderItemId);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task ActiveSliderItem(int sliderItemId)
        {
            await _sliderRepository.ActiveSliderItem(sliderItemId);
            await _unitOfWork.SaveChangesAsync();
        }

        public List<Slider> GetSliders(int applicationId)
        {
            return _sliderRepository.GetSliders(applicationId);
        }

        public List<SliderItem> GetSliderItems(int sliderId)
        {
            return _sliderRepository.GetSliderItems(sliderId);
        }

        public async Task DeleteSliderItem(int sliderItemId)
        {
            await _sliderRepository.DeleteSliderItem(sliderItemId);
            await _unitOfWork.SaveChangesAsync();
        }
        public Slider GetSliderWithItems(int sliderId)
        {
            return _sliderRepository.GetSliderWithItems(sliderId);
        }

        public async Task<SliderItem> GetSliderItem(int sliderItemId)
        {
            return await _sliderRepository.GetSliderItem(sliderItemId);
        }

        public async Task<SliderItem> UpdateSliderItem(SliderItem sliderItem)
        {
            var updated = await _sliderRepository.UpdateSliderItem(sliderItem);
            await _unitOfWork.SaveChangesAsync();
            return updated;
        }
    }
}