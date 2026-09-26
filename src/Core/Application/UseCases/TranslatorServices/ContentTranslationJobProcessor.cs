using System;
using System.Threading.Tasks;
using Application.CMSRepository;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

// Bound from the "ContentTranslation" configuration section.
public class ContentTranslationOptions
{
    public const string SectionName = "ContentTranslation";

    // Off until the DBA has deployed CMS_ContentTranslationJobs.
    public bool WorkerEnabled { get; set; }

    public int PollIntervalSeconds { get; set; } = 15;

    // How long a claim is held. The provider call is cut off at half of it, so a live worker
    // always finishes (or fails) before its lease can expire.
    public int LeaseMinutes { get; set; } = 15;

    // Provider calls per job before it fails for explicit retry; only definitive rejections
    // (rate limiting) are retried automatically.
    public int MaxAttempts { get; set; } = 5;

    // Culture whose Ready translation content activation requires; 0 = not configured.
    public int ActivationCultureId { get; set; }

    // Culture-aware public read rollout: off for the whole environment unless set, and then only
    // served for the applications listed here.
    public bool LocalizedReadEnabled { get; set; }

    public int[] LocalizedReadApplicationIds { get; set; } = [];

    // Culture whose reads may fall back to the legacy Content.FarsiContent snapshot; 0 = none.
    public int LegacyFarsiCultureId { get; set; }
}

// Safe, fixed codes stored in ContentTranslationJob.ErrorCode.
public static class ContentTranslationErrorCodes
{
    public const string ProviderRateLimited = "provider_rate_limited";
    public const string ProviderRetriesExhausted = "provider_retries_exhausted";
    public const string ProviderError = "provider_error";
    public const string ProviderTimeout = "provider_timeout";
    public const string ProviderCancelled = "provider_cancelled";
    public const string InvalidOutput = "invalid_output";
    public const string LeaseExpired = "lease_expired";
    public const string CultureUnavailable = "culture_unavailable";
    public const string TranslationDeleted = "translation_deleted";
}

// Claims and runs one background translation job per call. Never runs inside a database
// transaction across the provider call, never writes Content/FarsiContent, and never logs or
// stores prompts, payloads or provider responses.
public class ContentTranslationJobProcessor
{
    private readonly IContentTranslationJobRepository _repository;
    private readonly ITranslationPort _translationPort;
    private readonly ContentTranslationOptions _options;
    private readonly TimeProvider _timeProvider;

    public ContentTranslationJobProcessor(IContentTranslationJobRepository repository, ITranslationPort translationPort,
        ContentTranslationOptions options, TimeProvider timeProvider)
    {
        _repository = repository;
        _translationPort = translationPort;
        _options = options;
        _timeProvider = timeProvider;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    // Returns false when no job was due, so the caller can wait before polling again.
    public async Task<bool> RunOnce(string workerId, CancellationToken stoppingToken)
    {
        var job = await _repository.FindNextClaimable(UtcNow, stoppingToken);
        if (job == null)
            return false;

        if (job.State == ContentTranslationJobState.Processing)
        {
            // Lease expired: its worker died mid-job, possibly after the provider accepted the
            // request. Chat completions have no idempotency key, so never resend automatically.
            await Complete(job, ContentTranslationJobState.Failed, ContentTranslationErrorCodes.LeaseExpired, stoppingToken);
            return true;
        }

        job.State = ContentTranslationJobState.Processing;
        job.AttemptCount++;
        job.LeaseOwner = workerId;
        job.LeaseExpiresAt = UtcNow.AddMinutes(_options.LeaseMinutes);
        if (!await Save(job, stoppingToken))
            return true; // another worker claimed it first

        await Process(job, stoppingToken);
        return true;
    }

    private async Task Process(ContentTranslationJob job, CancellationToken stoppingToken)
    {
        var source = await _repository.FindSourceGraph(job.ContentId, stoppingToken);
        if (!IsCurrent(source, job))
        {
            await Complete(job, ContentTranslationJobState.Superseded, null, stoppingToken);
            return;
        }

        var culture = await _repository.FindCulture(job.CultureId, stoppingToken);
        if (culture == null)
        {
            await Complete(job, ContentTranslationJobState.Failed, ContentTranslationErrorCodes.CultureUnavailable, stoppingToken);
            return;
        }

        var document = TranslationSourceDocument.Serialize(source);
        TranslationResult result;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
        {
            timeout.CancelAfter(TimeSpan.FromMinutes(_options.LeaseMinutes / 2.0));
            try
            {
                result = await _translationPort.TranslateAsync(
                    new TranslationRequest(document, TranslationSourceDocument.TranslatableFields, culture.Title), timeout.Token);
            }
            catch (OperationCanceledException)
            {
                // Ambiguous: the provider may have processed (and billed) the request. Fail for
                // explicit retry. CancellationToken.None: record the outcome even during shutdown.
                var code = stoppingToken.IsCancellationRequested ? ContentTranslationErrorCodes.ProviderCancelled : ContentTranslationErrorCodes.ProviderTimeout;
                await Complete(job, ContentTranslationJobState.Failed, code, CancellationToken.None);
                return;
            }
        }

        // From here the provider work is paid for: finish recording it even if the host is stopping.
        if (!result.Success)
        {
            if (!result.Retryable)
                await Complete(job, ContentTranslationJobState.Failed, ContentTranslationErrorCodes.ProviderError, CancellationToken.None);
            else if (job.AttemptCount >= _options.MaxAttempts)
                await Complete(job, ContentTranslationJobState.Failed, ContentTranslationErrorCodes.ProviderRetriesExhausted, CancellationToken.None);
            else
                await Requeue(job);
            return;
        }

        var localizedTextJson = TranslationSourceDocument.ToLocalizedTextJson(document, result.TranslatedJson);
        if (localizedTextJson == null)
        {
            await Complete(job, ContentTranslationJobState.Failed, ContentTranslationErrorCodes.InvalidOutput, CancellationToken.None);
            return;
        }

        // The source may have changed during the (slow) provider call.
        if (!IsCurrent(await _repository.FindSourceGraph(job.ContentId, CancellationToken.None), job))
        {
            await Complete(job, ContentTranslationJobState.Superseded, null, CancellationToken.None);
            return;
        }

        var translation = await _repository.FindTranslation(job.ContentId, job.CultureId, CancellationToken.None);
        if (translation is { IsDeleted: true })
        {
            // A deliberately deleted row is never resurrected.
            await Complete(job, ContentTranslationJobState.Failed, ContentTranslationErrorCodes.TranslationDeleted, CancellationToken.None);
            return;
        }

        // A matching Ready row (e.g. written concurrently) is kept as is.
        if (!IsReady(translation, job.SourceFingerprint))
        {
            if (translation == null)
            {
                translation = new ContentTranslation { ContentId = job.ContentId, CultureId = job.CultureId, IsActive = true };
                _repository.AddTranslation(translation);
            }

            translation.TranslationStatus = TranslationStatus.Ready;
            translation.SourceFingerprint = job.SourceFingerprint;
            translation.LocalizedTextJson = localizedTextJson;
            translation.Provider = result.Provider;
            translation.Model = result.Model;
            translation.TranslatedAt = UtcNow;
            translation.Error = null;
        }

        // One SaveChanges: the translation and the job's success commit together, or (lost lease,
        // concurrent row insert) neither does.
        // ponytail: a lost write leaves the job Processing until its lease expires (-> Failed,
        // lease_expired); an explicit request then finds any concurrently written Ready row.
        await Complete(job, ContentTranslationJobState.Succeeded, null, CancellationToken.None);
    }

    public static bool IsReady(ContentTranslation translation, string sourceFingerprint) =>
        translation is { IsDeleted: false, TranslationStatus: TranslationStatus.Ready } && translation.SourceFingerprint == sourceFingerprint;

    private static bool IsCurrent(Content source, ContentTranslationJob job) =>
        source != null && ContentSourceFingerprint.Compute(source) == job.SourceFingerprint;

    private Task Requeue(ContentTranslationJob job)
    {
        // 30s, 1m, 2m, ... capped at 30m.
        var backoff = TimeSpan.FromSeconds(Math.Min(30 * Math.Pow(2, job.AttemptCount - 1), 1800));
        job.State = ContentTranslationJobState.Queued;
        job.NextAttemptAt = UtcNow + backoff;
        job.ErrorCode = ContentTranslationErrorCodes.ProviderRateLimited;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;
        return Save(job, CancellationToken.None);
    }

    private Task Complete(ContentTranslationJob job, ContentTranslationJobState state, string errorCode, CancellationToken cancellationToken)
    {
        job.State = state;
        job.ErrorCode = errorCode;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;
        job.CompletedAt = UtcNow;
        return Save(job, cancellationToken);
    }

    private Task<bool> Save(ContentTranslationJob job, CancellationToken cancellationToken)
    {
        job.Version++;
        return _repository.TrySaveChanges(cancellationToken);
    }
}
