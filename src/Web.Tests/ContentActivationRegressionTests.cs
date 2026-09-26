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

    private async Task<(HttpClient Client, int ApplicationId)> SignIn(bool superAdmin = true, string? keys = null)
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

    private Task<int> SeedContent(int applicationId, bool isActive = false, string? farsi = LegacyFarsi) => Db(async context =>
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

    private Task<(bool IsActive, string? FarsiContent)> ContentState(int contentId) =>
        Db(async context => await context.Contents.Where(c => c.Id == contentId).Select(c => new ValueTuple<bool, string?>(c.IsActive, c.FarsiContent)).SingleAsync());

    private static async Task<string?> TranslationState(HttpResponseMessage response) =>
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
            ["Title"] = title
        }));

    private Task<Content> Master(int contentId) =>
        Db(c => c.Contents.AsNoTracking().Include(x => x.Metadata).Include(x => x.Sections).ThenInclude(x => x.Elements).SingleAsync(x => x.Id == contentId));

    private async Task<string> CurrentFingerprint(int contentId) => ContentSourceFingerprint.Compute(await Master(contentId));

    // The fingerprint the rendered form will post back.
    private static string FormFingerprint(string body) =>
        System.Text.RegularExpressions.Regex.Match(body, "name=\"SourceFingerprint\" value=\"([0-9a-f]{64})\"").Groups[1].Value;

    // Every field the rendered form would post (hidden IDs, fingerprint, input values), so a save
    // test posts what the editor actually has; `overrides` are the translator's edits.
    private static System.Collections.Generic.Dictionary<string, string> FormFields(string body, System.Collections.Generic.Dictionary<string, string> overrides)
    {
        var fields = new System.Collections.Generic.Dictionary<string, string>();
        var start = body.IndexOf("id=\"frmFarsiContent\"", StringComparison.Ordinal);
        var form = body[start..body.IndexOf("</form>", start, StringComparison.Ordinal)];
        foreach (System.Text.RegularExpressions.Match input in System.Text.RegularExpressions.Regex.Matches(form, "<input[^>]*>"))
        {
            var name = System.Text.RegularExpressions.Regex.Match(input.Value, "name=\"([^\"]+)\"");
            var value = System.Text.RegularExpressions.Regex.Match(input.Value, "value=\"([^\"]*)\"");
            if (name.Success && name.Groups[1].Value != "__RequestVerificationToken")
                fields[name.Groups[1].Value] = WebUtility.HtmlDecode(value.Groups[1].Value);
        }
        foreach (System.Text.RegularExpressions.Match textarea in System.Text.RegularExpressions.Regex.Matches(form, "<textarea[^>]*name=\"([^\"]+)\"[^>]*>(.*?)</textarea>", System.Text.RegularExpressions.RegexOptions.Singleline))
            fields[textarea.Groups[1].Value] = WebUtility.HtmlDecode(textarea.Groups[2].Value).TrimStart('\r', '\n');
        foreach (var (key, value) in overrides)
            fields[key] = value;
        return fields;
    }

    private Task<ContentTranslation?> Translation(int contentId) =>
        Db(c => c.ContentTranslations.IgnoreQueryFilters().SingleOrDefaultAsync(t => t.ContentId == contentId));

    [Fact]
    public async Task ManualFarsiSave_PersistsOnlyCanonicalTranslation_AndContentActivatesImmediately()
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId);
        var before = await Snapshot(contentId);

        var save = await SaveFarsi(client, contentId, "عنوان دستی");

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal("Done", await save.Content.ReadAsStringAsync());
        var after = await Snapshot(contentId);
        Assert.Equal((LegacyFarsi, before.UpdatedDT, 0), (after.FarsiContent, after.UpdatedDT, after.Jobs)); // exact legacy bytes, no queued work
        var translation = await Translation(contentId);
        Assert.NotNull(translation);
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
            ["Description"] = "<p>FA <em>desc</em></p>"
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
        Assert.Equal((false, (string?)null), await ContentState(foreignContentId));
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

        // The form lists the added section first (master priority order), then the kept one.
        var fields = FormFields(body, new() { ["Title"] = "FA-title-2", ["Sections[1].SectionElements[0].TinyText"] = "FA-kept-2" });
        Assert.Equal((metadataId.ToString(), keptSectionId.ToString(), keptElementId.ToString()),
            (fields["Metadata.Id"], fields["Sections[1].Id"], fields["Sections[1].SectionElements[0].Id"]));
        Assert.DoesNotContain(fields.Keys, k => k.Contains("FileNameText") || k.Contains("ElementTitle") || k.Contains("ElementType") || k.Contains("Priority"));
        var save = await client.PostAsync("/BackOffice/Content/SaveFarsiContentForm", new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal("Done", await save.Content.ReadAsStringAsync());
        var translation = await Translation(contentId);
        Assert.NotNull(translation);
        Assert.Equal((TranslationStatus.Ready, await CurrentFingerprint(contentId), "manual"),
            (translation.TranslationStatus, translation.SourceFingerprint, translation.Provider));
        var text = LegacyFarsiContentParser.Deserialize(translation.LocalizedTextJson);
        Assert.Equal("FA-title-2", text.Title);
        Assert.Equal(new[] { "EN-added", "FA-kept-2" }, text.Sections.SelectMany(s => s.Elements).Select(e => e.TinyText));
        Assert.Equal("FA-meta", text.Metadata!.Title);
        var after = await Snapshot(contentId);
        Assert.Equal((before.FarsiContent, before.UpdatedDT), (after.FarsiContent, after.UpdatedDT)); // legacy snapshot left for its later cutover

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
        var translation = await Translation(contentId);
        Assert.NotNull(translation);
        Assert.Equal(TranslationStatus.Stale, translation.TranslationStatus);
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
        var translation = await Translation(contentId);
        Assert.NotNull(translation);
        Assert.Equal(TranslationStatus.Stale, translation.TranslationStatus);
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
        Assert.NotNull(translation);
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

    // A partial historical snapshot saved unchanged: only omitted fields take the current English;
    // explicit nulls stay blank, and the legacy bytes are untouched.
    [Fact]
    public async Task PartialLegacyFarsi_UnchangedSave_OmittedFieldsInheritSource_ExplicitNullsStayBlank()
    {
        var (client, applicationId) = await SignIn();
        var (contentId, legacy) = await Db(async context =>
        {
            if (!await context.Cultures.IgnoreQueryFilters().AnyAsync(c => c.Id == ActivationCultureId))
                context.Cultures.Add(new Culture { Id = ActivationCultureId, ApplicationId = applicationId, Title = "Farsi", Key = "fa-IR", IsActive = true });
            var tiny = new SectionElement { ElementType = 1000, TinyText = "EN-tiny" };
            var editor = new SectionElement { ElementType = 1005, EditorText = "EN-editor" };
            var content = new Content
            {
                ApplicationId = applicationId, TypeId = 1000, Title = "EN-title", HeadLine = "EN-head", Abstract = "EN-abstract", PublishDt = DateTime.UtcNow,
                Metadata = new ContentMetadata { Title = "EN-meta", Author = "EN-author" },
                Sections = new System.Collections.Generic.List<ContentSection> { new() { Priority = 1, Elements = new System.Collections.Generic.List<SectionElement> { tiny, editor } } }
            };
            context.Contents.Add(content);
            await context.SaveChangesAsync();
            content.FarsiContent = $$"""
                {"Id":{{content.Id}},"Title":"FA-title","Abstract":null,"Metadata":{"Id":{{content.Metadata.Id}},"Author":null},
                 "Sections":[{"Id":{{tiny.SectionId}},"Elements":[{"Id":{{tiny.Id}}},{"Id":{{editor.Id}},"EditorText":null}]}]}
                """;
            await context.SaveChangesAsync();
            return (content.Id, content.FarsiContent);
        });
        var before = await Snapshot(contentId);

        var body = await client.GetStringAsync($"/BackOffice/Content/FarsiContentForm/{contentId}/1000");
        Assert.Equal(before, await Snapshot(contentId));
        var save = await client.PostAsync("/BackOffice/Content/SaveFarsiContentForm", new FormUrlEncodedContent(FormFields(body, new())));

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var translation = await Translation(contentId);
        Assert.NotNull(translation);
        Assert.Equal((TranslationStatus.Ready, "manual"), (translation.TranslationStatus, translation.Provider));
        var text = LegacyFarsiContentParser.Deserialize(translation.LocalizedTextJson);
        Assert.Equal(("FA-title", "EN-head", null, "EN-meta", null), (text.Title, text.HeadLine, text.Abstract, text.Metadata!.Title, text.Metadata.Author));
        var elements = text.Sections.Single().Elements;
        Assert.Equal(("EN-tiny", (string?)null), (elements[0].TinyText, elements[1].EditorText));
        Assert.Equal(legacy, (await Snapshot(contentId)).FarsiContent);
    }

    // The request's DataAnnotations limits are enforced at the boundary, top-level and nested, before
    // anything is persisted. Section-element fields are posted at the form's own indexes.
    [Theory]
    [InlineData("Title", 257)]
    [InlineData("HeadLine", 2049)]
    [InlineData("Abstract", 2049)]
    [InlineData("Metadata.Title", 257)]
    [InlineData("Metadata.Author", 129)]
    [InlineData("Metadata.Keywords", 1025)]
    [InlineData("Metadata.Description", 2049)]
    [InlineData("Sections[1].SectionElements[0].TinyText", 257)]
    public async Task FarsiSave_OversizedBoundedField_IsRejected_AndWritesNothing(string field, int length)
    {
        var (client, applicationId) = await SignIn();
        var (contentId, _, _, _, legacy) = await SeedStaleFarsi(applicationId);
        var body = await client.GetStringAsync($"/BackOffice/Content/FarsiContentForm/{contentId}/1000");
        var fields = FormFields(body, new() { [field] = new string('ف', length) });
        Assert.Contains("Sections[1].SectionElements[0].TinyText", fields.Keys); // the nested field really is part of the form
        var before = await Snapshot(contentId);

        var save = await client.PostAsync("/BackOffice/Content/SaveFarsiContentForm", new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.BadRequest, save.StatusCode);
        var json = await save.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InvalidInput", json.GetProperty("translationState").GetString());
        Assert.Equal(new[] { field }, json.GetProperty("fields").EnumerateArray().Select(f => f.GetString()));
        Assert.Equal(before, await Snapshot(contentId));
        Assert.Equal(legacy, before.FarsiContent);
        Assert.Null(await Translation(contentId));
    }

    // At-limit bounded fields and long unbounded rich text (Description, EditorText) still save.
    [Fact]
    public async Task FarsiSave_AtLimitAndUnboundedText_Saves()
    {
        var (client, applicationId) = await SignIn();
        var (contentId, _, _, keptElementId, _) = await SeedStaleFarsi(applicationId);
        var body = await client.GetStringAsync($"/BackOffice/Content/FarsiContentForm/{contentId}/1000");
        var fields = FormFields(body, new()
        {
            ["Title"] = new string('ف', 256),
            ["Metadata.Author"] = new string('ف', 128),
            ["Sections[1].SectionElements[0].TinyText"] = new string('ف', 256),
            ["Description"] = new string('ف', 20000),
        });

        var save = await client.PostAsync("/BackOffice/Content/SaveFarsiContentForm", new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal("Done", await save.Content.ReadAsStringAsync());
        var translation = await Translation(contentId);
        Assert.NotNull(translation);
        var text = LegacyFarsiContentParser.Deserialize(translation.LocalizedTextJson);
        Assert.Equal((256, 128, 20000), (text.Title!.Length, text.Metadata!.Author!.Length, text.Description!.Length));
        Assert.Equal(256, text.Sections.SelectMany(s => s.Elements).Single(e => e.Id == keptElementId).TinyText!.Length);
    }

    // Source and translation are rendered side by side: master text read-only, Farsi editable,
    // media/titles preserved from master and never posted.
    [Fact]
    public async Task FarsiForm_ShowsSourceBesideTranslation_AndMasterMediaReadOnly()
    {
        var (client, applicationId) = await SignIn();
        var (contentId, _, keptSectionId, _, _) = await SeedStaleFarsi(applicationId);
        await Db(async c =>
        {
            c.SectionElements.Add(new SectionElement { SectionId = keptSectionId, ElementType = 1001, FileNameText = "master-image.jpg", ElementTitle = "Hero" });
            return await c.SaveChangesAsync();
        });

        var body = await client.GetStringAsync($"/BackOffice/Content/FarsiContentForm/{contentId}/1000");

        Assert.Contains("value=\"FA-kept\"", body);
        Assert.Contains("EN: EN-kept", body);
        Assert.Contains("EN: EN-title", body);
        Assert.Contains("/Storage/Section/Images/master-image.jpg", body);
        Assert.Contains(">Hero<", body);
        Assert.DoesNotContain("name=\"Sections[1].SectionElements[1].TinyText\"", body);
        Assert.Contains("Translation status: <strong>Not translated</strong>", body);
    }

    [Fact]
    public async Task FarsiSave_DeletedTranslation_IsNotResurrected_AndWritesNothing()
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId);
        await Db(async c =>
        {
            c.ContentTranslations.Add(new ContentTranslation
            {
                ContentId = contentId, CultureId = ActivationCultureId, TranslationStatus = TranslationStatus.Stale, SourceFingerprint = "old",
                LocalizedTextJson = "{}", IsActive = true, IsDeleted = true
            });
            return await c.SaveChangesAsync();
        });
        var before = await Snapshot(contentId);

        var save = await SaveFarsi(client, contentId, "عنوان");

        Assert.Equal(HttpStatusCode.Conflict, save.StatusCode);
        Assert.Equal("TranslationDeleted", await TranslationState(save));
        Assert.Equal(before, await Snapshot(contentId));
        var translation = await Translation(contentId);
        Assert.NotNull(translation);
        Assert.Equal((true, TranslationStatus.Stale, "{}"), (translation.IsDeleted, translation.TranslationStatus, translation.LocalizedTextJson));
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
