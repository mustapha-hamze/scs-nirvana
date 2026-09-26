using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Web.Tests;

// ChangeContentActiveMode(mode: true) only activates when the configured activation culture has a
// Ready translation of the current source. It never calls the translation provider (a throwing
// port double is registered), never blocks on translation, and never treats legacy FarsiContent as
// readiness. RequestTranslation queues the background job instead.
public sealed class ContentActivationRegressionTests : IClassFixture<TestWebApplicationFactory>
{
    private const int ActivationCultureId = 7001;
    private const string Password = "CorrectHorseBattery12";
    private const string LegacyFarsi = "{\"Title\":\"عنوان موجود\"}";

    private readonly WebApplicationFactory<Program> _factory;

    public ContentActivationRegressionTests(TestWebApplicationFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ContentTranslation:ActivationCultureId", ActivationCultureId.ToString());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITranslationPort>();
                services.AddTransient<ITranslationPort, ThrowingTranslationPort>();
            });
        });
    }

    private sealed class ThrowingTranslationPort : ITranslationPort
    {
        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The translation provider must never be called from a request.");
    }

    private async Task<(HttpClient Client, int ApplicationId)> SignIn(bool superAdmin = true, string keys = null)
    {
        var email = $"content-activation-{Guid.NewGuid():N}@test.local";
        var user = superAdmin
            ? await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, Password)
            : await AccountFlowHelper.SeedAdminUserAsync(_factory, email, Password);
        var client = await AccountFlowHelper.LoginAsync(_factory, email, Password);
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        if (keys != null)
            await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, keys);
        return (client, applicationId);
    }

    private async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private Task<int> SeedContent(int applicationId, bool isActive = false, string farsi = LegacyFarsi) => Db(async context =>
    {
        if (!await context.Cultures.IgnoreQueryFilters().AnyAsync(c => c.Id == ActivationCultureId))
            context.Cultures.Add(new Culture { Id = ActivationCultureId, ApplicationId = applicationId, Title = "Farsi", Key = "fa-IR", IsActive = true });

        var content = new Content
        {
            ApplicationId = applicationId, TypeId = 1000, Title = "English", PublishDt = DateTime.UtcNow,
            IsActive = isActive, FarsiContent = farsi
        };
        context.Contents.Add(content);
        await context.SaveChangesAsync();
        return content.Id;
    });

    private Task SeedTranslation(int contentId, TranslationStatus status, bool currentSource = true) => Db(async context =>
    {
        var source = await context.Contents.AsNoTracking().Include(c => c.Metadata).Include(c => c.Sections).SingleAsync(c => c.Id == contentId);
        context.ContentTranslations.Add(new ContentTranslation
        {
            ContentId = contentId, CultureId = ActivationCultureId, TranslationStatus = status,
            SourceFingerprint = currentSource ? ContentSourceFingerprint.Compute(source) : new string('0', 64),
            LocalizedTextJson = "{}", Provider = "test", IsActive = true
        });
        return await context.SaveChangesAsync();
    });

    private Task<(bool IsActive, string FarsiContent)> ContentState(int contentId) =>
        Db(async context => await context.Contents.Where(c => c.Id == contentId).Select(c => new ValueTuple<bool, string>(c.IsActive, c.FarsiContent)).SingleAsync());

    private static async Task<string> TranslationState(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("translationState").GetString();

    private static Task<HttpResponseMessage> Activate(HttpClient client, int contentId, bool mode = true) =>
        client.PostAsync($"/BackOffice/Content/ChangeContentActiveMode/1000/{contentId}/{mode.ToString().ToLowerInvariant()}", null);

    [Fact]
    public async Task Activation_WithReadyTranslationOfCurrentSource_Activates()
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId);
        await SeedTranslation(contentId, TranslationStatus.Ready);

        var response = await Activate(client, contentId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Done", await response.Content.ReadAsStringAsync());
        Assert.Equal((true, LegacyFarsi), await ContentState(contentId));
    }

    [Theory]
    [InlineData(null, true, "Missing")] // legacy FarsiContent alone is not readiness
    [InlineData(TranslationStatus.Ready, false, "Stale")]
    [InlineData(TranslationStatus.Stale, true, "Stale")]
    [InlineData(TranslationStatus.NeedsReview, true, "NeedsReview")]
    [InlineData(TranslationStatus.Failed, true, "Failed")]
    public async Task Activation_WithoutReadyTranslation_ConflictsAndStaysInactive(TranslationStatus? status, bool currentSource, string expectedState)
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId);
        if (status != null)
            await SeedTranslation(contentId, status.Value, currentSource);

        var response = await Activate(client, contentId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(expectedState, await TranslationState(response));
        Assert.Equal((false, LegacyFarsi), await ContentState(contentId));
    }

    [Fact]
    public async Task RequestTranslation_QueuesOneJob_AndActivationReportsQueued()
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId);

        var first = await client.PostAsync($"/BackOffice/Content/RequestTranslation/{contentId}/{ActivationCultureId}", null);
        var second = await client.PostAsync($"/BackOffice/Content/RequestTranslation/{contentId}/{ActivationCultureId}", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Queued", firstBody.GetProperty("translationState").GetString());
        Assert.Equal(firstBody.GetProperty("jobId").GetInt32(), secondBody.GetProperty("jobId").GetInt32());
        Assert.Equal(1, await Db(c => c.ContentTranslationJobs.CountAsync(j => j.ContentId == contentId)));

        var activation = await Activate(client, contentId);
        Assert.Equal(HttpStatusCode.Conflict, activation.StatusCode);
        Assert.Equal("Queued", await TranslationState(activation));
        Assert.Equal((false, LegacyFarsi), await ContentState(contentId));
        Assert.False(await Db(c => c.ContentTranslations.AnyAsync(t => t.ContentId == contentId)));
    }

    [Fact]
    public async Task RequestTranslation_OtherApplicationsContentOrUnknownCulture_IsNotFound()
    {
        var (client, _) = await SignIn();
        var (_, otherApplicationId) = await SignIn();
        var foreignContentId = await SeedContent(otherApplicationId);

        var foreign = await client.PostAsync($"/BackOffice/Content/RequestTranslation/{foreignContentId}/{ActivationCultureId}", null);
        var foreignUnknownCulture = await client.PostAsync($"/BackOffice/Content/RequestTranslation/{foreignContentId}/999999", null);

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignUnknownCulture.StatusCode);
        Assert.False(await Db(c => c.ContentTranslationJobs.AnyAsync(j => j.ContentId == foreignContentId)));
    }

    [Fact]
    public async Task RequestTranslation_UnknownCulture_IsUnavailableConflict()
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId);

        var response = await client.PostAsync($"/BackOffice/Content/RequestTranslation/{contentId}/999999", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CultureUnavailable", await TranslationState(response));
        Assert.False(await Db(c => c.ContentTranslationJobs.AnyAsync(j => j.ContentId == contentId)));
    }

    // The content form's control, as used by a non-SuperAdmin with exactly the keys it needs.
    [Fact]
    public async Task ContentForm_WithChangeActivity_RendersConfiguredCultureControl_AndItQueues()
    {
        var (client, applicationId) = await SignIn(superAdmin: false,
            keys: string.Join(',', Web.Authorization.AccessKeys.Content.Edit, Web.Authorization.AccessKeys.Content.ChangeActivity));
        var contentId = await SeedContent(applicationId);
        await Db(async c =>
        {
            c.ApplicationSettings.Add(new ApplicationSetting { ApplicationId = applicationId, SettingId = 5000, Title = "Setting", Value = "https://example.test" });
            return await c.SaveChangesAsync();
        });

        var form = await client.GetStringAsync($"/BackOffice/Content/ContentForm/{contentId}/1000");
        Assert.Contains($"requestContentTranslation({contentId}, {ActivationCultureId}, 'btnRequestTranslation')", form);
        Assert.Contains("id=\"contentTranslationState\"", form);

        var response = await client.PostAsync($"/BackOffice/Content/RequestTranslation/{contentId}/{ActivationCultureId}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Queued", await TranslationState(response));
        Assert.Equal(1, await Db(c => c.ContentTranslationJobs.CountAsync(j => j.ContentId == contentId)));
    }

    [Fact]
    public async Task RequestTranslation_WithoutChangeActivityAccess_IsForbidden()
    {
        var (client, applicationId) = await SignIn(superAdmin: false);
        var contentId = await SeedContent(applicationId);

        var response = await client.PostAsync($"/BackOffice/Content/RequestTranslation/{contentId}/{ActivationCultureId}", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await Db(c => c.ContentTranslationJobs.AnyAsync(j => j.ContentId == contentId)));
    }

    private async Task<HttpResponseMessage> SaveFarsi(HttpClient client, int contentId, string title) =>
        await client.PostAsync("/BackOffice/Content/SaveFarsiContentForm", new FormUrlEncodedContent(new System.Collections.Generic.Dictionary<string, string>
        {
            ["Id"] = contentId.ToString(),
            ["SourceFingerprint"] = await CurrentFingerprint(contentId),
            ["Title"] = title,
            // The form always posts the metadata node's hidden fields, even when master has none.
            ["Metadata.Id"] = "0",
            ["Metadata.ContentId"] = contentId.ToString()
        }));

    private Task<Content> Master(int contentId) =>
        Db(c => c.Contents.AsNoTracking().Include(x => x.Metadata).Include(x => x.Sections).ThenInclude(x => x.Elements).SingleAsync(x => x.Id == contentId));

    private async Task<string> CurrentFingerprint(int contentId) => ContentSourceFingerprint.Compute(await Master(contentId));

    // The fingerprint the rendered form will post back.
    private static string FormFingerprint(string body) =>
        System.Text.RegularExpressions.Regex.Match(body, "name=\"SourceFingerprint\" value=\"([0-9a-f]{64})\"").Groups[1].Value;

    private Task<ContentTranslation> Translation(int contentId) =>
        Db(c => c.ContentTranslations.IgnoreQueryFilters().SingleOrDefaultAsync(t => t.ContentId == contentId));

    [Fact]
    public async Task ManualFarsiSave_PersistsBothPayloads_AndContentActivatesImmediately()
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId, farsi: null);

        var save = await SaveFarsi(client, contentId, "عنوان دستی");

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal("Done", await save.Content.ReadAsStringAsync());
        var (_, farsiContent) = await ContentState(contentId);
        Assert.Contains("عنوان دستی", farsiContent);
        var translation = await Translation(contentId);
        Assert.Equal((TranslationStatus.Ready, "manual", null), (translation.TranslationStatus, translation.Provider, translation.Error));
        Assert.NotNull(translation.TranslatedAt);
        Assert.Equal("عنوان دستی", LegacyFarsiContentParser.Deserialize(translation.LocalizedTextJson).Title);

        var activation = await Activate(client, contentId);
        Assert.Equal(HttpStatusCode.OK, activation.StatusCode);
        Assert.True((await ContentState(contentId)).IsActive);
    }

    // Rebasing never loosens the save: the rebased graph is still validated against the master.
    [Fact]
    public async Task ManualFarsiSave_ChangedHtmlStructure_ConflictsAndPreservesBothPayloads()
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId);
        await Db(async c =>
        {
            (await c.Contents.SingleAsync(x => x.Id == contentId)).Description = "<p>EN <strong>desc</strong></p>";
            return await c.SaveChangesAsync();
        });

        var save = await client.PostAsync("/BackOffice/Content/SaveFarsiContentForm", new FormUrlEncodedContent(new System.Collections.Generic.Dictionary<string, string>
        {
            ["Id"] = contentId.ToString(),
            ["SourceFingerprint"] = await CurrentFingerprint(contentId),
            ["Title"] = "FA",
            ["Description"] = "<p>FA <em>desc</em></p>",
            ["Metadata.Id"] = "0"
        }));

        Assert.Equal(HttpStatusCode.Conflict, save.StatusCode);
        Assert.Equal("InvalidStructure", await TranslationState(save));
        Assert.Equal((false, LegacyFarsi), await ContentState(contentId));
        Assert.Null(await Translation(contentId));
    }

    [Fact]
    public async Task ManualFarsiSave_OtherApplicationsContent_IsNotFound_AndUnchanged()
    {
        var (client, _) = await SignIn();
        var (_, otherApplicationId) = await SignIn();
        var foreignContentId = await SeedContent(otherApplicationId, farsi: null);

        var save = await SaveFarsi(client, foreignContentId, "نفوذ");

        Assert.Equal(HttpStatusCode.NotFound, save.StatusCode);
        Assert.Equal((false, (string)null), await ContentState(foreignContentId));
        Assert.Null(await Translation(foreignContentId));
    }

    // Master whose layout changed after `legacy` was snapshotted: element "kept" survives,
    // element "added" and its section are new; the snapshot also has since-removed nodes.
    private Task<(int ContentId, int MetadataId, int KeptSectionId, int KeptElementId, string Legacy)> SeedStaleFarsi(int applicationId, bool malformed = false) => Db(async context =>
    {
        if (!await context.Cultures.IgnoreQueryFilters().AnyAsync(c => c.Id == ActivationCultureId))
            context.Cultures.Add(new Culture { Id = ActivationCultureId, ApplicationId = applicationId, Title = "Farsi", Key = "fa-IR", IsActive = true });
        var kept = new SectionElement { ElementType = 1000, TinyText = "EN-kept" };
        var content = new Content
        {
            ApplicationId = applicationId, TypeId = 1000, Title = "EN-title", PublishDt = DateTime.UtcNow,
            Metadata = new ContentMetadata { Title = "EN-meta" },
            Sections = new System.Collections.Generic.List<ContentSection>
            {
                new() { Priority = 1, Elements = new System.Collections.Generic.List<SectionElement> { new() { ElementType = 1000, TinyText = "EN-added" } } },
                new() { Priority = 2, Elements = new System.Collections.Generic.List<SectionElement> { kept } }
            }
        };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        var legacy = malformed ? "{\"Id\": " : $$"""
            {"Id":{{content.Id}},"Title":"FA-title","Metadata":{"Id":{{content.Metadata.Id}},"Title":"FA-meta"},
             "Sections":[{"Id":{{kept.SectionId}},"Priority":7,"Elements":[{"Id":{{kept.Id}},"TinyText":"FA-kept"},{"Id":999998,"TinyText":"FA-removed-element"}]},
                         {"Id":999997,"Elements":[{"Id":999996,"TinyText":"FA-removed-section"}]}]}
            """;
        content.FarsiContent = legacy;
        await context.SaveChangesAsync();
        return (content.Id, content.Metadata.Id, kept.SectionId, kept.Id, legacy);
    });

    private Task<(string FarsiContent, DateTime UpdatedDT, int Translations, int Jobs)> Snapshot(int contentId) => Db(async c =>
    {
        var content = await c.Contents.AsNoTracking().SingleAsync(x => x.Id == contentId);
        return (content.FarsiContent, content.UpdatedDT,
            await c.ContentTranslations.IgnoreQueryFilters().CountAsync(t => t.ContentId == contentId),
            await c.ContentTranslationJobs.CountAsync(j => j.ContentId == contentId));
    });

    [Fact]
    public async Task StaleLegacyFarsi_FormIsRebasedWithoutWriting_ThenSaveProducesCurrentReadyTranslation()
    {
        var (client, applicationId) = await SignIn();
        var (contentId, metadataId, keptSectionId, keptElementId, _) = await SeedStaleFarsi(applicationId);
        var before = await Snapshot(contentId);

        var form = await client.GetAsync($"/BackOffice/Content/FarsiContentForm/{contentId}/1000");
        var body = await form.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        Assert.Contains("FA-title", body);
        Assert.Contains("FA-meta", body);
        Assert.Contains("FA-kept", body);
        Assert.Contains("EN-added", body);
        Assert.DoesNotContain("FA-removed", body);
        Assert.DoesNotContain("999997", body);
        Assert.DoesNotContain("Farsi content was not found", body);
        Assert.Equal(before, await Snapshot(contentId));
        Assert.Equal(await CurrentFingerprint(contentId), FormFingerprint(body));

        var save = await client.PostAsync("/BackOffice/Content/SaveFarsiContentForm", new FormUrlEncodedContent(new System.Collections.Generic.Dictionary<string, string>
        {
            ["Id"] = contentId.ToString(),
            ["SourceFingerprint"] = FormFingerprint(body),
            ["Title"] = "FA-title-2",
            ["Metadata.Id"] = metadataId.ToString(),
            ["Metadata.Title"] = "FA-meta",
            ["Sections[0].Id"] = keptSectionId.ToString(),
            ["Sections[0].SectionElements[0].Id"] = keptElementId.ToString(),
            ["Sections[0].SectionElements[0].TinyText"] = "FA-kept-2",
        }));

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal("Done", await save.Content.ReadAsStringAsync());
        var translation = await Translation(contentId);
        Assert.Equal((TranslationStatus.Ready, await CurrentFingerprint(contentId), "manual"),
            (translation.TranslationStatus, translation.SourceFingerprint, translation.Provider));
        var text = LegacyFarsiContentParser.Deserialize(translation.LocalizedTextJson);
        Assert.Equal("FA-title-2", text.Title);
        Assert.Equal(new[] { "EN-added", "FA-kept-2" }, text.Sections.SelectMany(s => s.Elements).Select(e => e.TinyText));
        var (_, farsiContent) = await ContentState(contentId);
        Assert.DoesNotContain("FA-removed", farsiContent);
        Assert.DoesNotContain("\"FarsiContent\":\"{", farsiContent); // no nested snapshot

        Assert.Equal(HttpStatusCode.OK, (await Activate(client, contentId)).StatusCode);
    }

    [Fact]
    public async Task FarsiForm_PrefersCanonicalTranslation_AndLeavesItUnchanged()
    {
        var (client, applicationId) = await SignIn();
        var (contentId, metadataId, keptSectionId, keptElementId, _) = await SeedStaleFarsi(applicationId);
        await Db(async c =>
        {
            c.ContentTranslations.Add(new ContentTranslation
            {
                ContentId = contentId, CultureId = ActivationCultureId, TranslationStatus = TranslationStatus.Stale, SourceFingerprint = "old", IsActive = true,
                LocalizedTextJson = LegacyFarsiContentParser.Serialize(new LocalizedContentText("CANON-title", null, null, null,
                    new LocalizedMetadataText(metadataId, "CANON-meta", null, null, null),
                    new() { new LocalizedSectionText(keptSectionId, new() { new LocalizedElementText(keptElementId, "CANON-kept", null) }) }))
            });
            return await c.SaveChangesAsync();
        });
        var before = await Snapshot(contentId);

        var body = await client.GetStringAsync($"/BackOffice/Content/FarsiContentForm/{contentId}/1000");

        Assert.Contains("CANON-kept", body);
        Assert.Contains("CANON-meta", body);
        Assert.DoesNotContain("FA-kept", body);
        Assert.Contains("EN-added", body);
        Assert.Equal(before, await Snapshot(contentId));
        Assert.Equal(TranslationStatus.Stale, (await Translation(contentId)).TranslationStatus);
        Assert.Equal(HttpStatusCode.Conflict, (await Activate(client, contentId)).StatusCode);
    }

    // Parseable but incomplete canonical JSON is not a seed: the form falls back to legacy Farsi.
    [Fact]
    public async Task FarsiForm_IncompleteCanonical_FallsBackToLegacy_WithoutWriting()
    {
        var (client, applicationId) = await SignIn();
        var (contentId, metadataId, keptSectionId, keptElementId, _) = await SeedStaleFarsi(applicationId);
        await Db(async c =>
        {
            c.ContentTranslations.Add(new ContentTranslation
            {
                ContentId = contentId, CultureId = ActivationCultureId, TranslationStatus = TranslationStatus.Stale, SourceFingerprint = "old", IsActive = true,
                // No headLine/abstract/description, no metadata text, element without editorText.
                LocalizedTextJson = $$"""
                    {"title":"CANON-title","metadata":{"id":{{metadataId}},"title":"CANON-meta"},
                     "sections":[{"id":{{keptSectionId}},"elements":[{"id":{{keptElementId}},"tinyText":"CANON-kept"}]}]}
                    """
            });
            return await c.SaveChangesAsync();
        });
        var before = await Snapshot(contentId);

        var body = await client.GetStringAsync($"/BackOffice/Content/FarsiContentForm/{contentId}/1000");

        Assert.DoesNotContain("CANON-", body);
        Assert.Contains("FA-kept", body);
        Assert.Contains("FA-meta", body);
        Assert.Equal(before, await Snapshot(contentId));
        Assert.Equal(TranslationStatus.Stale, (await Translation(contentId)).TranslationStatus);
    }

    // English edited between GET and save: the save conflicts instead of marking the Farsi,
    // translated from the older source, as Ready for the new one.
    [Theory]
    [InlineData("text")]
    [InlineData("layout")]
    public async Task FarsiSave_AfterSourceChangedSinceGet_ConflictsAndWritesNothing(string change)
    {
        var (client, applicationId) = await SignIn();
        var (contentId, metadataId, keptSectionId, keptElementId, legacy) = await SeedStaleFarsi(applicationId);
        await Db(async c =>
        {
            c.ContentTranslations.Add(new ContentTranslation
            {
                ContentId = contentId, CultureId = ActivationCultureId, TranslationStatus = TranslationStatus.Stale, SourceFingerprint = "old", IsActive = true,
                LocalizedTextJson = "{}"
            });
            return await c.SaveChangesAsync();
        });
        var body = await client.GetStringAsync($"/BackOffice/Content/FarsiContentForm/{contentId}/1000");
        await Db(async c =>
        {
            if (change == "text")
                (await c.SectionElements.SingleAsync(e => e.Id == keptElementId)).TinyText = "EN-kept-edited";
            else
                c.SectionElements.Add(new SectionElement { SectionId = keptSectionId, ElementType = 1000, TinyText = "EN-new" });
            return await c.SaveChangesAsync();
        });
        var before = await Snapshot(contentId);

        var save = await client.PostAsync("/BackOffice/Content/SaveFarsiContentForm", new FormUrlEncodedContent(new System.Collections.Generic.Dictionary<string, string>
        {
            ["Id"] = contentId.ToString(),
            ["SourceFingerprint"] = FormFingerprint(body),
            ["Title"] = "FA-title-2",
            ["Metadata.Id"] = metadataId.ToString(),
            ["Sections[0].Id"] = keptSectionId.ToString(),
            ["Sections[0].SectionElements[0].Id"] = keptElementId.ToString(),
            ["Sections[0].SectionElements[0].TinyText"] = "FA-kept-2",
        }));

        Assert.Equal(HttpStatusCode.Conflict, save.StatusCode);
        Assert.Equal("SourceChanged", await TranslationState(save));
        Assert.Equal(before, await Snapshot(contentId));
        Assert.Equal(legacy, before.FarsiContent);
        var translation = await Translation(contentId);
        Assert.Equal((TranslationStatus.Stale, "old", "{}"), (translation.TranslationStatus, translation.SourceFingerprint, translation.LocalizedTextJson));
    }

    [Fact]
    public async Task FarsiForm_MalformedLegacy_FallsBackToEnglish_WithoutWriting()
    {
        var (client, applicationId) = await SignIn();
        var (contentId, _, _, _, legacy) = await SeedStaleFarsi(applicationId, malformed: true);
        var before = await Snapshot(contentId);

        var body = await client.GetStringAsync($"/BackOffice/Content/FarsiContentForm/{contentId}/1000");

        Assert.Contains("Farsi content was not found", body);
        Assert.Contains("EN-kept", body);
        Assert.Equal(before, await Snapshot(contentId));
        Assert.Equal(legacy, before.FarsiContent);
    }

    [Fact]
    public async Task FarsiForm_OtherApplicationsContent_IsNotFound()
    {
        var (client, _) = await SignIn();
        var (_, otherApplicationId) = await SignIn();
        var (contentId, _, _, _, _) = await SeedStaleFarsi(otherApplicationId);

        var response = await client.GetAsync($"/BackOffice/Content/FarsiContentForm/{contentId}/1000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivation_IsUnchanged_AndNeedsNoTranslation()
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId, isActive: true);

        var response = await Activate(client, contentId, mode: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((false, LegacyFarsi), await ContentState(contentId));
    }
}
