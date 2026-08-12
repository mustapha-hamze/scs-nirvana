using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domains.Entities.CustomModule;

namespace Services.SCMServices
{
    public class SliderServices : ISliderServices
    {
        private readonly global::Application.SCMRepository.ISliderRepository _sliderRepository;
        public SliderServices(global::Application.SCMRepository.ISliderRepository sliderRepository)
        {
            _sliderRepository = sliderRepository;
        }

        public async Task<Slider> Create(Slider slider)
        {
            return await _sliderRepository.Create(slider);
        }

        public async Task<SliderItem> CreateSliderItem(SliderItem sliderItem)
        {
            return await _sliderRepository.CreateSliderItem(sliderItem);
        }

        public async Task DeactiveSliderItem(int sliderItemId)
        {
            await _sliderRepository.DeactiveSliderItem(sliderItemId);
        }

        public async Task ActiveSliderItem(int sliderItemId)
        {
            await _sliderRepository.ActiveSliderItem(sliderItemId);
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
            return await _sliderRepository.UpdateSliderItem(sliderItem);
        }
    }
}