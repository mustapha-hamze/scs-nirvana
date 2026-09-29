using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Application.UseCases.TranslatorServices;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Web.Tests;

// Phase 4: the server-rendered Legacy Farsi Translation Queue dashboard. Options are pinned per
// host (never taken from appsettings) and the background worker is not hosted, so WorkerEnabled
// is a rendered configuration flag only. Client behaviour lives in js/legacy-farsi-queue.test.mjs.
public sealed class LegacyFarsiTranslationQueueDashboardTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Password = "CorrectHorseBattery12";
    private const string PageUrl = "/BackOffice/LegacyFarsiTranslationQueue/Index";
    private const string ScriptPath = "/BackOffice/js/features/legacy-farsi-queue.js";
    private const int MissingCultureId = 987654;

    private readonly TestWebApplicationFactory _fixture;

    public LegacyFarsiTranslationQueueDashboardTests(TestWebApplicationFactory fixture)
    {
        _fixture = fixture;
    }

    private WebApplicationFactory<Program> Host(bool workerEnabled) => _fixture.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            foreach (var worker in services.Where(d => d.ImplementationType == typeof(Web.Services.Translation.ContentTranslationWorker)).ToList())
                services.Remove(worker);
            services.RemoveAll<ITranslationPort>();
            services.AddTransient<ITranslationPort, ThrowingTranslationPort>();
            services.PostConfigure<ContentTranslationOptions>(options =>
            {
                options.WorkerEnabled = workerEnabled;
                options.ActivationCultureId = MissingCultureId;
                options.LegacyBulkCandidateTypeIds = [1001, 1003];
                options.BulkRequestMaxItems = 7;
            });
        }));

    private sealed class ThrowingTranslationPort : ITranslationPort
    {
        public Task<TranslationResult> TranslateAsync(TranslationRequest request, System.Threading.CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The dashboard must never reach the translation provider.");
    }

    private static async Task<HttpClient> SignIn(WebApplicationFactory<Program> host, bool superAdmin = true)
    {
        var email = $"legacy-dashboard-{Guid.NewGuid():N}@test.local";
        var user = superAdmin
            ? await AccountFlowHelper.SeedSuperAdminUserAsync(host, email, Password)
            : await AccountFlowHelper.SeedAdminUserAsync(host, email, Password);
        var client = await AccountFlowHelper.LoginAsync(host, email, Password);
        await AccountFlowHelper.SelectApplicationAsync(host, client, user);
        return client;
    }

    private static async Task<string> Dashboard(WebApplicationFactory<Program> host)
    {
        var response = await (await SignIn(host)).GetAsync(PageUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string RootTag(string html) => Regex.Match(html, "<div id=\"lfqDashboard\"[^>]*>").Value;

    [Fact]
    public async Task Dashboard_RendersConfiguredTypesLimitAndControls_WithScriptOnce()
    {
        var html = await Dashboard(Host(workerEnabled: true));

        Assert.Equal("<div id=\"lfqDashboard\" data-worker-enabled=\"true\" data-max-items=\"7\"\n    data-type-ids=\"1001,1003\">",
            RootTag(html).Replace("\r\n", "\n"));
        var typeSelect = Regex.Match(html, "<select[^>]*id=\"lfqTypeId\"[^>]*>(.*?)</select>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(new[] { ("", "All configured types"), ("1001", "1001"), ("1003", "1003") },
            Regex.Matches(typeSelect, "<option value=\"([^\"]*)\">([^<]*)</option>").Select(m => (m.Groups[1].Value, m.Groups[2].Value)));
        Assert.Equal(new[] { "Id", "Title", "TypeId", "UpdatedAt" },
            Regex.Matches(Regex.Match(html, "<select[^>]*id=\"lfqSort\"[^>]*>(.*?)</select>", RegexOptions.Singleline).Groups[1].Value,
                "value=\"([^\"]*)\"").Select(m => m.Groups[1].Value));
        foreach (var label in new[] { "for=\"lfqTypeId\"", "for=\"lfqTitle\"", "for=\"lfqContentId\"", "for=\"lfqSort\"", "for=\"lfqPage\"" })
            Assert.Contains(label, html);
        Assert.Matches("<button[^>]*id=\"lfqQueue\"[^>]*disabled", html);
        Assert.Contains("Up to 7 per request.", html);
        Assert.Single(Regex.Matches(html, Regex.Escape(ScriptPath)));
        Assert.Contains("LegacyFarsiQueue.init(document.getElementById(\"lfqDashboard\"))", html);
        Assert.Contains("name=\"request-verification-token\"", html); // the header source for $.ajaxSetup
        Assert.DoesNotContain("id=\"lfqWorkerUnavailable\"", html);
    }

    [Fact]
    public async Task Dashboard_RendersTwoAccessibleTabs_WithTheQueueAndRecoveredWorkflowsInSeparatePanels()
    {
        var html = await Dashboard(Host(workerEnabled: true));

        var tablist = Regex.Match(html, "<ul[^>]*role=\"tablist\"[^>]*>(.*?)</ul>", RegexOptions.Singleline);
        Assert.True(tablist.Success);
        Assert.Contains("aria-label=", tablist.Value);
        var tabs = Regex.Matches(tablist.Groups[1].Value, "<button([^>]*)>(.*?)</button>", RegexOptions.Singleline);
        Assert.Equal(2, tabs.Count);
        Assert.Single(Regex.Matches(html, "role=\"tablist\""));
        Assert.Equal(2, Regex.Matches(html, "role=\"tab\"").Count);
        Assert.Equal(2, Regex.Matches(html, "role=\"tabpanel\"").Count);
        Assert.Equal(new[] { "To Translate", "Recovered Jobs" }, tabs.Select(t => Regex.Replace(t.Groups[2].Value, "<[^>]*>", "").Trim()));
        foreach (var (tab, panel, selected) in new[] { ("lfqTabCandidates", "lfqPanelCandidates", "true"), ("lfqTabRecovered", "lfqPanelRecovered", "false") })
        {
            var attributes = tabs.Single(t => t.Groups[1].Value.Contains($"id=\"{tab}\"")).Groups[1].Value;
            Assert.Contains("type=\"button\"", attributes);
            Assert.Contains("role=\"tab\"", attributes);
            Assert.Contains($"aria-selected=\"{selected}\"", attributes);
            Assert.Contains($"aria-controls=\"{panel}\"", attributes);
            Assert.DoesNotContain("data-bs-toggle", attributes); // switched by the dashboard script alone
            Assert.Matches($"<div id=\"{panel}\" role=\"tabpanel\" aria-labelledby=\"{tab}\" tabindex=\"0\"{(selected == "true" ? "" : " hidden")}>", html);
        }
        Assert.Contains("<span id=\"lfqRecoveredTabCount\"></span>", html); // the active count is filled in only when there is one

        // To Translate holds the filters, candidate list, selection and the session's batch bar; Recovered Jobs the
        // activity counts and the recovered list, with no percentage bar.
        var candidates = html.Substring(html.IndexOf("id=\"lfqPanelCandidates\"", StringComparison.Ordinal));
        var recovered = candidates.Substring(candidates.IndexOf("id=\"lfqPanelRecovered\"", StringComparison.Ordinal));
        candidates = candidates.Substring(0, candidates.Length - recovered.Length);
        foreach (var id in new[] { "lfqFilters", "lfqRows", "lfqPage", "lfqSelectedCount", "lfqClear", "lfqQueue", "lfqSummary", "lfqProgressBar" })
        {
            Assert.Contains($"id=\"{id}\"", candidates);
            Assert.DoesNotContain($"id=\"{id}\"", recovered);
        }
        foreach (var id in new[] { "lfqRecoveredStatus", "lfqRecoveredStatusTitle", "lfqRecoveredStatusCounts", "lfqRecoveredRows", "lfqRecoveredNext" })
        {
            Assert.Contains($"id=\"{id}\"", recovered);
            Assert.DoesNotContain($"id=\"{id}\"", candidates);
        }
        Assert.DoesNotContain("progress", recovered.Substring(0, recovered.IndexOf("<script", StringComparison.Ordinal)));
        // The failure reason has its own column, between the job state and attempts.
        Assert.Matches("<th scope=\"col\">Translation job</th>\\s*<th scope=\"col\">Failure reason</th>\\s*<th scope=\"col\">Attempts</th>", recovered);
        Assert.True(html.IndexOf("role=\"tablist\"", StringComparison.Ordinal) < html.IndexOf("id=\"lfqPanelCandidates\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dashboard_IsSuperAdminOnly()
    {
        var host = Host(workerEnabled: true);
        var response = await (await SignIn(host, superAdmin: false)).GetAsync(PageUrl);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("lfqTabRecovered", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Dashboard_WorkerDisabled_RendersUnavailableState()
    {
        var html = await Dashboard(Host(workerEnabled: false));

        Assert.Contains("data-worker-enabled=\"false\"", RootTag(html));
        Assert.Contains("id=\"lfqWorkerUnavailable\"", html);
        Assert.Contains("Background translation is unavailable in this environment", html);
        Assert.Matches("<button[^>]*id=\"lfqQueue\"[^>]*disabled", html);
    }

    [Fact]
    public async Task Dashboard_ExposesNoCultureApplicationOrWorkerDetails()
    {
        var html = await Dashboard(Host(workerEnabled: true));
        var main = Regex.Match(html, "<div id=\"lfqDashboard\".*?<script src=\"/BackOffice/js/features/legacy-farsi-queue.js\">", RegexOptions.Singleline).Value;

        Assert.NotEmpty(main);
        Assert.DoesNotContain(MissingCultureId.ToString(), html);
        // The fixed culture-unavailable alert is the one expected mention of "culture", and the fixed
        // recovered-jobs intro the one mention of "application" (the selected one, never an ID).
        var withoutAlert = Regex.Replace(main, "<div[^>]*id=\"lfqCultureUnavailable\".*?</div>", "", RegexOptions.Singleline);
        Assert.NotEqual(main, withoutAlert);
        var intro = Regex.Match(withoutAlert, "<p[^>]*id=\"lfqRecoveredIntro\".*?</p>", RegexOptions.Singleline).Value;
        Assert.Contains("not only yours", intro);
        Assert.Contains("completed in the last 7 days", intro);
        withoutAlert = withoutAlert.Replace(intro, "");
        foreach (var secret in new[] { "culture", "application", "FarsiContent", "fingerprint", "provider", "model", "WorkerEnabled", "PollInterval", "Lease" })
            Assert.DoesNotContain(secret, withoutAlert, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnavailableCulture_CandidatesReportIt_ForTheDashboard()
    {
        var client = await SignIn(Host(workerEnabled: true));

        var body = await client.GetFromJsonAsync<JsonElement>("/BackOffice/LegacyFarsiTranslationQueue/Candidates");

        Assert.False(body.GetProperty("cultureAvailable").GetBoolean());
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task UnavailableCulture_RecoveredJobsReportIt_WithNoJobs()
    {
        var client = await SignIn(Host(workerEnabled: true));

        var body = await client.GetFromJsonAsync<JsonElement>("/BackOffice/LegacyFarsiTranslationQueue/RecoveredJobs");

        Assert.False(body.GetProperty("cultureAvailable").GetBoolean());
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task SidebarLink_IsShownToSuperAdminOnly()
    {
        var host = Host(workerEnabled: true);
        var link = $"href=\"{PageUrl}\"";

        Assert.Contains(link, await (await SignIn(host)).GetStringAsync("/BackOffice/Home/Index"));
        Assert.DoesNotContain(link, await (await SignIn(host, superAdmin: false)).GetStringAsync("/BackOffice/Home/Index"));
    }

    [Fact]
    public async Task Script_IsServed_AndOnlyCallsTheCandidatesQueueProgressAndRecoveredJobsEndpoints()
    {
        var host = Host(workerEnabled: true);
        var script = await host.CreateClient().GetStringAsync(ScriptPath);

        Assert.Equal(new[]
            {
                "/BackOffice/LegacyFarsiTranslationQueue/Candidates", "/BackOffice/LegacyFarsiTranslationQueue/Queue", "/BackOffice/LegacyFarsiTranslationQueue/Progress",
                "/BackOffice/LegacyFarsiTranslationQueue/RecoveredJobs"
            },
            Regex.Matches(script, "\"(/BackOffice/[^\"]*)\"").Select(m => m.Groups[1].Value).Distinct());
        Assert.Contains("contentType: \"application/json\"", script);
        Assert.Contains("$.ajax(", script);
        foreach (var banned in new[] { "setInterval", "EventSource", "WebSocket", "signalR", "signalr", "innerHTML", ".html(", "fetch(", "applicationId", "cultureId", "errorCode",
                     "localStorage", "sessionStorage" })
            Assert.DoesNotContain(banned, script, StringComparison.Ordinal);

        var client = await SignIn(host);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/BackOffice/LegacyFarsiTranslationQueue/Candidates")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync("/BackOffice/LegacyFarsiTranslationQueue/Queue", new StringContent("{\"contentIds\":[]}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
    }
}
