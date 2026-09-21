using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domains.Entities.CustomModule;
using Application.UnitOfWork;

namespace Application.UseCases.SCMServices
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

        public async Task<Slider> Create(Slider slider, int applicationId, CancellationToken cancellationToken = default)
        {
            // Never trust ApplicationId from the caller - server-pin it.
            slider.ApplicationId = applicationId;
            var created = await _sliderRepository.Create(slider);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return created;
        }

        public async Task<SliderItem> CreateSliderItem(SliderItem sliderItem, int applicationId, CancellationToken cancellationToken = default)
        {
            // Verify the target slider belongs to this application (and isn't soft-deleted)
            // before attaching the item to it - never trust SliderId from the caller.
            var slider = await _sliderRepository.GetByIdForApplication(sliderItem.SliderId, applicationId, cancellationToken);
            sliderItem.SliderId = slider.Id;
            var created = await _sliderRepository.CreateSliderItem(sliderItem);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return created;
        }

        public async Task DeactiveSliderItem(int sliderItemId, int applicationId, CancellationToken cancellationToken = default)
        {
            await _sliderRepository.GetItemForApplication(sliderItemId, applicationId, cancellationToken);
            await _sliderRepository.DeactiveSliderItem(sliderItemId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task ActiveSliderItem(int sliderItemId, int applicationId, CancellationToken cancellationToken = default)
        {
            await _sliderRepository.GetItemForApplication(sliderItemId, applicationId, cancellationToken);
            await _sliderRepository.ActiveSliderItem(sliderItemId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public Task<List<Slider>> GetSliders(int applicationId, CancellationToken cancellationToken = default)
        {
            return _sliderRepository.GetSliders(applicationId, cancellationToken);
        }

        public Task<List<SliderItem>> GetSliderItems(int sliderId, int applicationId, CancellationToken cancellationToken = default)
        {
            return _sliderRepository.GetSliderItems(sliderId, applicationId, cancellationToken);
        }

        public async Task DeleteSliderItem(int sliderItemId, int applicationId, CancellationToken cancellationToken = default)
        {
            await _sliderRepository.GetItemForApplication(sliderItemId, applicationId, cancellationToken);
            await _sliderRepository.DeleteSliderItem(sliderItemId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        public Task<Slider> GetSliderWithItems(int sliderId, int applicationId, CancellationToken cancellationToken = default)
        {
            return _sliderRepository.GetSliderWithItems(sliderId, applicationId, cancellationToken);
        }

        public async Task<SliderItem> GetSliderItem(int sliderItemId, int applicationId, CancellationToken cancellationToken = default)
        {
            return await _sliderRepository.GetItemForApplication(sliderItemId, applicationId, cancellationToken);
        }

        public async Task<SliderItem> UpdateSliderItem(SliderItem sliderItem, int applicationId, CancellationToken cancellationToken = default)
        {
            // Resolve ownership through item -> slider -> application, then preserve the
            // verified SliderId instead of trusting whatever was bound from the request - this
            // is what stops an item being re-parented into a different (possibly cross-app) slider.
            var existing = await _sliderRepository.GetItemForApplication(sliderItem.Id, applicationId, cancellationToken);
            sliderItem.SliderId = existing.SliderId;
            var updated = await _sliderRepository.UpdateSliderItem(sliderItem);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return updated;
        }
    }
}