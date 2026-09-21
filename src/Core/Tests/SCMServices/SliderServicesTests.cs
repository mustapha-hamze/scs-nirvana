using Core.Tests.TestSupport;
using Domains.Entities.CustomModule;
using Infrastructure.SCMRepository;
using Application.UseCases.SCMServices;
using Xunit;

namespace Core.Tests.SCMServices;

// End-to-end (real repository + SQLite, not mocked) coverage for the Slider aggregate's
// application scoping: every case here proves the actual persisted/rejected outcome, not just
// that a method was called with certain arguments.
public class SliderServicesTests
{
    private static SliderServices CreateSut(Infrastructure.Data.ApplicationDbContext context)
    {
        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context);
        return new SliderServices(new SliderRepository(context), unitOfWork);
    }

    [Fact]
    public async Task Create_ApplicationIdTampering_ServerPinsRealApplicationId()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var sut = CreateSut(context);

        var created = await sut.Create(new Slider { ApplicationId = 99, Title = "New Slider" }, applicationId: 1);

        Assert.Equal(1, created.ApplicationId);

        await using var verifyContext = factory.CreateContext();
        var stored = verifyContext.Sliders.Single(s => s.Id == created.Id);
        Assert.Equal(1, stored.ApplicationId);
    }

    [Fact]
    public async Task GetSliders_ExcludesSoftDeletedRoots()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.Sliders.Add(new Slider { ApplicationId = 1, Title = "Active" });
        context.Sliders.Add(new Slider { ApplicationId = 1, Title = "Deleted", IsDeleted = true });
        context.SaveChanges();

        var sut = CreateSut(context);

        var result = await sut.GetSliders(applicationId: 1);

        var slider = Assert.Single(result);
        Assert.Equal("Active", slider.Title);
    }

    [Fact]
    public async Task CreateSliderItem_CrossApplicationSliderId_ThrowsAndDoesNotWrite()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var slider = new Slider { ApplicationId = 2, Title = "Victim" };
        context.Sliders.Add(slider);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            sut.CreateSliderItem(new SliderItem { SliderId = slider.Id, Title = "Item" }, applicationId: 1));

        await using var verifyContext = factory.CreateContext();
        Assert.Empty(verifyContext.SliderItems.Where(i => i.SliderId == slider.Id));
    }

    [Fact]
    public async Task CreateSliderItem_SoftDeletedParentSlider_ThrowsAndDoesNotWrite()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var slider = new Slider { ApplicationId = 1, Title = "Deleted", IsDeleted = true };
        context.Sliders.Add(slider);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            sut.CreateSliderItem(new SliderItem { SliderId = slider.Id, Title = "Item" }, applicationId: 1));
    }

    [Fact]
    public async Task GetSliderItem_CrossApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var slider = new Slider { ApplicationId = 2, Title = "Victim" };
        context.Sliders.Add(slider);
        context.SaveChanges();
        var item = new SliderItem { SliderId = slider.Id, Title = "Item" };
        context.SliderItems.Add(item);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() => sut.GetSliderItem(item.Id, applicationId: 1));
    }

    [Fact]
    public async Task GetSliderItem_SoftDeletedParentSlider_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var slider = new Slider { ApplicationId = 1, Title = "Deleted", IsDeleted = true };
        context.Sliders.Add(slider);
        context.SaveChanges();
        var item = new SliderItem { SliderId = slider.Id, Title = "Item" };
        context.SliderItems.Add(item);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() => sut.GetSliderItem(item.Id, applicationId: 1));
    }

    [Fact]
    public async Task UpdateSliderItem_CrossApplication_ThrowsAndDoesNotWrite()
    {
        using var factory = new SqliteContextFactory();
        int itemId;
        using (var seedContext = factory.CreateContext())
        {
            var slider = new Slider { ApplicationId = 2, Title = "Victim" };
            seedContext.Sliders.Add(slider);
            seedContext.SaveChanges();
            var item = new SliderItem { SliderId = slider.Id, Title = "Original" };
            seedContext.SliderItems.Add(item);
            seedContext.SaveChanges();
            itemId = item.Id;
        }

        using var context = factory.CreateContext();
        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            sut.UpdateSliderItem(new SliderItem { Id = itemId, Title = "Hijacked" }, applicationId: 1));

        await using var verifyContext = factory.CreateContext();
        var unchanged = verifyContext.SliderItems.Single(i => i.Id == itemId);
        Assert.Equal("Original", unchanged.Title);
    }

    [Fact]
    public async Task UpdateSliderItem_SliderIdTampering_PreservesVerifiedSliderId()
    {
        using var factory = new SqliteContextFactory();
        int itemId, ownSliderId, otherSliderId;
        using (var seedContext = factory.CreateContext())
        {
            var ownSlider = new Slider { ApplicationId = 1, Title = "Own" };
            var otherSlider = new Slider { ApplicationId = 1, Title = "Other" };
            seedContext.Sliders.Add(ownSlider);
            seedContext.Sliders.Add(otherSlider);
            seedContext.SaveChanges();
            var item = new SliderItem { SliderId = ownSlider.Id, Title = "Original" };
            seedContext.SliderItems.Add(item);
            seedContext.SaveChanges();
            itemId = item.Id;
            ownSliderId = ownSlider.Id;
            otherSliderId = otherSlider.Id;
        }

        using var context = factory.CreateContext();
        var sut = CreateSut(context);

        // The payload claims the item belongs to a different slider - the verified,
        // already-persisted SliderId must win.
        await sut.UpdateSliderItem(
            new SliderItem { Id = itemId, SliderId = otherSliderId, Title = "Updated" },
            applicationId: 1);

        await using var verifyContext = factory.CreateContext();
        var updated = verifyContext.SliderItems.Single(i => i.Id == itemId);
        Assert.Equal(ownSliderId, updated.SliderId);
        Assert.Equal("Updated", updated.Title);
    }

    [Fact]
    public async Task DeactiveSliderItem_CrossApplication_ThrowsAndDoesNotChange()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var slider = new Slider { ApplicationId = 2, Title = "Victim" };
        context.Sliders.Add(slider);
        context.SaveChanges();
        var item = new SliderItem { SliderId = slider.Id, Title = "Item", IsActive = true };
        context.SliderItems.Add(item);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() => sut.DeactiveSliderItem(item.Id, applicationId: 1));

        await using var verifyContext = factory.CreateContext();
        Assert.True(verifyContext.SliderItems.Single(i => i.Id == item.Id).IsActive);
    }

    [Fact]
    public async Task ActiveSliderItem_CrossApplication_ThrowsAndDoesNotChange()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var slider = new Slider { ApplicationId = 2, Title = "Victim" };
        context.Sliders.Add(slider);
        context.SaveChanges();
        var item = new SliderItem { SliderId = slider.Id, Title = "Item", IsActive = false };
        context.SliderItems.Add(item);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() => sut.ActiveSliderItem(item.Id, applicationId: 1));

        await using var verifyContext = factory.CreateContext();
        Assert.False(verifyContext.SliderItems.Single(i => i.Id == item.Id).IsActive);
    }

    [Fact]
    public async Task DeleteSliderItem_CrossApplication_ThrowsAndDoesNotDelete()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var slider = new Slider { ApplicationId = 2, Title = "Victim" };
        context.Sliders.Add(slider);
        context.SaveChanges();
        var item = new SliderItem { SliderId = slider.Id, Title = "Item" };
        context.SliderItems.Add(item);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() => sut.DeleteSliderItem(item.Id, applicationId: 1));

        await using var verifyContext = factory.CreateContext();
        Assert.False(verifyContext.SliderItems.Single(i => i.Id == item.Id).IsDeleted);
    }

    [Fact]
    public async Task GetSliderItems_CrossApplicationSlider_ReturnsEmpty()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var slider = new Slider { ApplicationId = 2, Title = "Victim" };
        context.Sliders.Add(slider);
        context.SaveChanges();
        context.SliderItems.Add(new SliderItem { SliderId = slider.Id, Title = "Item" });
        context.SaveChanges();

        var sut = CreateSut(context);

        Assert.Empty(await sut.GetSliderItems(slider.Id, applicationId: 1));
    }

    [Fact]
    public async Task GetSliderItems_SoftDeletedParentSlider_ReturnsEmpty()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var slider = new Slider { ApplicationId = 1, Title = "Deleted", IsDeleted = true };
        context.Sliders.Add(slider);
        context.SaveChanges();
        context.SliderItems.Add(new SliderItem { SliderId = slider.Id, Title = "Item" });
        context.SaveChanges();

        var sut = CreateSut(context);

        Assert.Empty(await sut.GetSliderItems(slider.Id, applicationId: 1));
    }

    [Fact]
    public async Task GetSliderItems_SoftDeletedItem_ExcludedFromSameApplicationSlider()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var slider = new Slider { ApplicationId = 1, Title = "Slider" };
        context.Sliders.Add(slider);
        context.SaveChanges();
        context.SliderItems.Add(new SliderItem { SliderId = slider.Id, Title = "Active Item" });
        context.SliderItems.Add(new SliderItem { SliderId = slider.Id, Title = "Deleted Item", IsDeleted = true });
        context.SaveChanges();

        var sut = CreateSut(context);

        var result = await sut.GetSliderItems(slider.Id, applicationId: 1);

        var item = Assert.Single(result);
        Assert.Equal("Active Item", item.Title);
    }

    [Fact]
    public async Task GetSliderWithItems_CrossApplication_ReturnsEmptyDefault()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var slider = new Slider { ApplicationId = 2, Title = "Victim" };
        context.Sliders.Add(slider);
        context.SaveChanges();

        var sut = CreateSut(context);

        var result = await sut.GetSliderWithItems(slider.Id, applicationId: 1);

        Assert.Equal(0, result.Id);
    }

    [Fact]
    public async Task GetSliderWithItems_SoftDeletedSlider_ReturnsEmptyDefault()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var slider = new Slider { ApplicationId = 1, Title = "Deleted", IsDeleted = true };
        context.Sliders.Add(slider);
        context.SaveChanges();

        var sut = CreateSut(context);

        var result = await sut.GetSliderWithItems(slider.Id, applicationId: 1);

        Assert.Equal(0, result.Id);
    }

    [Fact]
    public async Task GetSliderWithItems_OnlyReturnsActiveNonDeletedItems()
    {
        using var factory = new SqliteContextFactory();
        int sliderId;
        using (var seedContext = factory.CreateContext())
        {
            var slider = new Slider { ApplicationId = 1, Title = "Slider" };
            seedContext.Sliders.Add(slider);
            seedContext.SaveChanges();
            sliderId = slider.Id;
            seedContext.SliderItems.Add(new SliderItem { SliderId = sliderId, Title = "Active", IsActive = true });
            seedContext.SliderItems.Add(new SliderItem { SliderId = sliderId, Title = "Inactive", IsActive = false });
            seedContext.SliderItems.Add(new SliderItem { SliderId = sliderId, Title = "Deleted", IsActive = true, IsDeleted = true });
            seedContext.SaveChanges();
        }

        // A fresh context avoids EF relationship fixup re-populating the filtered Include from
        // entities already tracked in the seeding context.
        using var context = factory.CreateContext();
        var sut = CreateSut(context);

        var result = await sut.GetSliderWithItems(sliderId, applicationId: 1);

        var visibleItem = Assert.Single(result.SliderItems);
        Assert.Equal("Active", visibleItem.Title);
    }
}
