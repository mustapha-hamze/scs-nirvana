namespace Web.Areas.BackOffice.Features.Content.ViewModels;

public sealed class ContentImagesViewModel
{
    public IReadOnlyList<ApplicationSettingDto> ContentImageAspectRatio { get; init; } = Array.Empty<ApplicationSettingDto>();
    public IReadOnlyList<ApplicationSettingDto> ContentImageSizes { get; init; } = Array.Empty<ApplicationSettingDto>();
    public IReadOnlyList<ContentImageDto> ContentImages { get; init; } = Array.Empty<ContentImageDto>();
    public bool CanUploadImages { get; init; }
}
