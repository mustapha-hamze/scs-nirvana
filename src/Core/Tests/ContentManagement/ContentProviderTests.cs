using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.ContentManagement;
using Xunit;

namespace Core.Tests.ContentManagement;

// Guards against the substring-matching bug in ContentProvider's category/tag reads: a relation
// id of 1 must not match content related to id 11.
public class ContentProviderTests
{
    [Fact]
    public void GetContentsListByCategoryId_CategoryOneDoesNotMatchCategoryEleven()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        context.Categories.Add(new Category { Id = 1, ApplicationId = 1, Title = "Category One" });
        var wronglyMatchedBefore = new Content { ApplicationId = 1, TypeId = 1000, Title = "Tagged 11", Categories = "11", IsActive = true };
        context.Contents.Add(wronglyMatchedBefore);
        context.SaveChanges();
        context.ContentInCategories.Add(new ContentInCategory { ContentId = wronglyMatchedBefore.Id, CategoryId = 11, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var provider = new ContentProvider(context);
        var result = provider.GetContentsListByCategoryId(applicationId: 1, categoryId: 1);

        Assert.Empty(result.Contents);
        Assert.Equal(0, result.PageCount);
        Assert.Equal("Category One", result.Title);
    }

    [Fact]
    public void GetContentsListByCategoryId_ReturnsExactMatch_AndPreservesResultShape()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        context.Categories.Add(new Category { Id = 2, ApplicationId = 1, Title = "Category Two" });
        var matching = new Content { ApplicationId = 1, TypeId = 1000, Title = "In category two", Categories = "2", IsActive = true, UpdatedDT = DateTime.Now };
        context.Contents.Add(matching);
        context.SaveChanges();
        context.ContentInCategories.Add(new ContentInCategory { ContentId = matching.Id, CategoryId = 2, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var provider = new ContentProvider(context);
        var result = provider.GetContentsListByCategoryId(applicationId: 1, categoryId: 2, pageIndex: 0, pageSize: 20);

        var content = Assert.Single(result.Contents);
        Assert.Equal("In category two", content.Title);
        // The pipe-delimited field on the returned content is untouched by the relation-filter fix.
        Assert.Equal("2", content.Categories);
        Assert.Equal(0, result.CurrentPage);
        Assert.Equal(1, result.PageCount);
        Assert.Equal("Category Two", result.Title);
    }

    [Fact]
    public void GetContentsListByCategoryId_FarsiKeyLang_UsesSameJoinTableFilter()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        context.Categories.Add(new Category { Id = 3, ApplicationId = 1, Title = "Category Three" });
        var wronglyMatchedBefore = new Content { ApplicationId = 1, TypeId = 1000, Title = "Tagged 13", Categories = "13", IsActive = true };
        context.Contents.Add(wronglyMatchedBefore);
        context.SaveChanges();
        context.ContentInCategories.Add(new ContentInCategory { ContentId = wronglyMatchedBefore.Id, CategoryId = 13, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var provider = new ContentProvider(context);
        var result = provider.GetContentsListByCategoryId(applicationId: 1, categoryId: 3, keyLang: "fa");

        Assert.Empty(result.Contents);
    }

    [Fact]
    public void GetContentsListByCategoryId_PagesAreOrderedByUpdatedDTDescending_AcrossPageBoundaries()
    {
        // Regression guard: Skip/Take used to run before OrderByDescending in the fluent chain,
        // so the "page" was sliced before the sort was applied. Insertion order here is the exact
        // opposite of the desired UpdatedDT order (Id 1 = oldest, Id 25 = newest), so a fallback/
        // insertion-order slice would produce the wrong 20 rows, not just the right rows in the
        // wrong order.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        context.Categories.Add(new Category { Id = 20, ApplicationId = 1, Title = "Category Twenty" });
        var baseTime = DateTime.Now;
        var contents = new List<Content>();
        for (var i = 1; i <= 25; i++)
        {
            var content = new Content { ApplicationId = 1, TypeId = 1000, Title = $"Item {i}", Categories = "20", IsActive = true, UpdatedDT = baseTime.AddMinutes(i) };
            contents.Add(content);
            context.Contents.Add(content);
        }
        context.SaveChanges();
        foreach (var content in contents)
        {
            context.ContentInCategories.Add(new ContentInCategory { ContentId = content.Id, CategoryId = 20, CreatedDt = DateTime.Now });
        }
        context.SaveChanges();

        var provider = new ContentProvider(context);
        var firstPage = provider.GetContentsListByCategoryId(applicationId: 1, categoryId: 20, pageIndex: 0, pageSize: 20);
        var secondPage = provider.GetContentsListByCategoryId(applicationId: 1, categoryId: 20, pageIndex: 1, pageSize: 20);

        Assert.Equal(20, firstPage.Contents.Count);
        Assert.Equal(Enumerable.Range(6, 20).Reverse().Select(i => $"Item {i}"), firstPage.Contents.Select(c => c.Title));

        Assert.Equal(5, secondPage.Contents.Count);
        Assert.Equal(Enumerable.Range(1, 5).Reverse().Select(i => $"Item {i}"), secondPage.Contents.Select(c => c.Title));
    }

    [Fact]
    public void GetContentsListByTagId_TagOneDoesNotMatchTagEleven()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        context.Tags.Add(new Tag { Id = 1, ApplicationId = 1, Title = "Tag One" });
        var wronglyMatchedBefore = new Content { ApplicationId = 1, TypeId = 1000, Title = "Tagged 11", Tags = "11", IsActive = true };
        context.Contents.Add(wronglyMatchedBefore);
        context.SaveChanges();
        context.ContentInTags.Add(new ContentInTag { ContentId = wronglyMatchedBefore.Id, TagId = 11 });
        context.SaveChanges();

        var provider = new ContentProvider(context);
        var result = provider.GetContentsListByTagId(applicationId: 1, tagId: 1);

        Assert.Empty(result.Contents);
        Assert.Equal("Tag One", result.Title);
    }

    [Fact]
    public void GetContentsListByTagId_ReturnsExactMatch_AndPreservesResultShape()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        context.Tags.Add(new Tag { Id = 4, ApplicationId = 1, Title = "Tag Four" });
        var matching = new Content { ApplicationId = 1, TypeId = 1000, Title = "Tagged four", Tags = "4", IsActive = true, UpdatedDT = DateTime.Now };
        context.Contents.Add(matching);
        context.SaveChanges();
        context.ContentInTags.Add(new ContentInTag { ContentId = matching.Id, TagId = 4 });
        context.SaveChanges();

        var provider = new ContentProvider(context);
        var result = provider.GetContentsListByTagId(applicationId: 1, tagId: 4, pageIndex: 0, pageSize: 20);

        var content = Assert.Single(result.Contents);
        Assert.Equal("Tagged four", content.Title);
        Assert.Equal("4", content.Tags);
        Assert.Equal(1, result.PageCount);
        Assert.Equal("Tag Four", result.Title);
    }

    [Fact]
    public void GetContentsListByTagId_PagesAreOrderedByUpdatedDTDescending_AcrossPageBoundaries()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        context.Tags.Add(new Tag { Id = 30, ApplicationId = 1, Title = "Tag Thirty" });
        var baseTime = DateTime.Now;
        var contents = new List<Content>();
        for (var i = 1; i <= 25; i++)
        {
            var content = new Content { ApplicationId = 1, TypeId = 1000, Title = $"Item {i}", Tags = "30", IsActive = true, UpdatedDT = baseTime.AddMinutes(i) };
            contents.Add(content);
            context.Contents.Add(content);
        }
        context.SaveChanges();
        foreach (var content in contents)
        {
            context.ContentInTags.Add(new ContentInTag { ContentId = content.Id, TagId = 30 });
        }
        context.SaveChanges();

        var provider = new ContentProvider(context);
        var firstPage = provider.GetContentsListByTagId(applicationId: 1, tagId: 30, pageIndex: 0, pageSize: 20);
        var secondPage = provider.GetContentsListByTagId(applicationId: 1, tagId: 30, pageIndex: 1, pageSize: 20);

        Assert.Equal(20, firstPage.Contents.Count);
        Assert.Equal(Enumerable.Range(6, 20).Reverse().Select(i => $"Item {i}"), firstPage.Contents.Select(c => c.Title));

        Assert.Equal(5, secondPage.Contents.Count);
        Assert.Equal(Enumerable.Range(1, 5).Reverse().Select(i => $"Item {i}"), secondPage.Contents.Select(c => c.Title));
    }
}
