using System;
using System.Linq;
using System.Threading.Tasks;
using Application.CMSRepository;
using Domains.Entities.ContentManagement;
using Microsoft.Extensions.Logging;

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

    // Content types the legacy-Farsi bulk translation queue may list; empty = none. Distinct, positive.
    public int[] LegacyBulkCandidateTypeIds { get; set; } = [];

    // Most content items one bulk translation request may queue.
    public int BulkRequestMaxItems { get; set; } = 25;

    public const int BulkRequestMaxItemsLimit = 100;

    // Days a terminal (Succeeded/Failed/Superseded) job stays in the queue dashboard's recovered-jobs
    // list after CompletedAt; Queued/Processing jobs are always listed.
    public int LegacyBulkRecentJobDays { get; set; } = 7;

    public const int LegacyBulkRecentJobDaysLimit = 30;

    public bool HasValidLegacyBulkSettings() =>
        LegacyBulkCandidateTypeIds != null
        && LegacyBulkCandidateTypeIds.All(id => id > 0)
        && LegacyBulkCandidateTypeIds.Distinct().Count() == LegacyBulkCandidateTypeIds.Length
        && BulkRequestMaxItems is >= 1 and <= BulkRequestMaxItemsLimit
        && LegacyBulkRecentJobDays is >= 1 and <= LegacyBulkRecentJobDaysLimit;
}

// Safe, fixed codes stored in ContentTranslationJob.ErrorCode (and TranslationResult.FailureCode).
public static class ContentTranslationErrorCodes
{
    // 429: rejected before processing.
    public const string ProviderRateLimited = "provider_rate_limited";
    // 503: overloaded, rejected before processing.
    public const string ProviderTransient = "provider_transient";
    public const string ProviderRetriesExhausted = "provider_retries_exhausted";
    // Non-transient 4xx: auth, model access, bad request.
    public const string ProviderRejected = "provider_rejected";
    // Connection/DNS/TLS failure before any response.
    public const string ProviderNetwork = "provider_network";
    // Unknown/ambiguous provider failure (other 5xx, 408, unexpected exceptions); also historical rows.
    public const string ProviderError = "provider_error";
    public const string ProviderTimeout = "provider_timeout";
    public const string ProviderCancelled = "provider_cancelled";
    public const string EmptyResponse = "empty_response";
    public const string InvalidJson = "invalid_json";
    // JSON that breaks the protected-field/hierarchy/type/HTML contract.
    public const string InvalidStructure = "invalid_structure";
    // The processor's own post-port check (and historical rows).
    public const string InvalidOutput = "invalid_output";
    public const string LeaseExpired = "lease_expired";
    public const string CultureUnavailable = "culture_unavailable";
    public const string TranslationDeleted = "translation_deleted";

    public const string GenericFailureReason = "Translation could not be completed. Try again later.";

    // Only rejections the provider documents as "not processed" are resent automatically. Chat
    // completions have no idempotency key, so network/timeout/cancel/other-5xx outcomes may already
    // be processed and billed: they stay terminal for explicit operator retry.
    public static bool IsRetryable(string code) => code is ProviderRateLimited or ProviderTransient;

    // The only failure text shown to operators: a fixed message per code, the generic one for anything
    // else (the retryable codes are only ever stored on Queued jobs; unknown, blank or future codes) -
    // never the stored value itself.
    public static string FailureReason(string code) => code switch
    {
        ProviderRetriesExhausted => "Translation could not be completed after several attempts. Try again later.",
        ProviderRejected => "The translation service rejected this request. Check the server logs.",
        ProviderNetwork => "The server could not reach the translation service. Try again later.",
        ProviderError => "Translation provider could not complete the request. Try again later.",
        ProviderTimeout => "Translation timed out. Try again.",
        ProviderCancelled => "Translation was interrupted. Try again.",
        EmptyResponse => "The translation service returned no text. Try again.",
        InvalidJson => "The translation response was not valid JSON. Try again.",
        InvalidStructure => "The translation response changed required content structure. Try again.",
        InvalidOutput => "The translation response could not be used. Try again.",
        LeaseExpired => "Translation processing was interrupted. Try again.",
        CultureUnavailable => "The target language is unavailable. Contact an administrator.",
        TranslationDeleted => "The translation was removed before completion.",
        _ => GenericFailureReason
    };

    // FailureReason for a Failed job; null for every other state, whatever code is stored.
    public static string FailureReasonFor(ContentTranslationJobState state, string code) =>
        state == ContentTranslationJobState.Failed ? FailureReason(code) : null;
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
    private readonly ILogger<ContentTranslationJobProcessor> _logger;

    // Codes a port may report; anything else is stored as provider_error.
    private static readonly HashSet<string> PortFailureCodes =
    [
        ContentTranslationErrorCodes.ProviderRateLimited, ContentTranslationErrorCodes.ProviderTransient,
        ContentTranslationErrorCodes.ProviderRejected, ContentTranslationErrorCodes.ProviderNetwork,
        ContentTranslationErrorCodes.ProviderError, ContentTranslationErrorCodes.ProviderTimeout,
        ContentTranslationErrorCodes.ProviderCancelled, ContentTranslationErrorCodes.EmptyResponse,
        ContentTranslationErrorCodes.InvalidJson, ContentTranslationErrorCodes.InvalidStructure
    ];

    public ContentTranslationJobProcessor(IContentTranslationJobRepository repository, ITranslationPort translationPort,
        ContentTranslationOptions options, TimeProvider timeProvider, ILogger<ContentTranslationJobProcessor> logger)
    {
        _repository = repository;
        _translationPort = translationPort;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
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

        try
        {
            await Process(job, stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested && LogUnexpected(job, ex))
        {
            throw; // unreachable: the filter only logs, the exception propagates unchanged
        }
        return true;
    }

    // Exception type only: database/provider messages can echo payload text. The job stays
    // Processing until its lease expires (-> lease_expired).
    private bool LogUnexpected(ContentTranslationJob job, Exception ex)
    {
        _logger.LogError("Content translation job {JobId} (content {ContentId}, attempt {Attempt}) failed with {FailureKind} {ExceptionType}",
            job.Id, job.ContentId, job.AttemptCount, "processing_error", ex.GetType().Name);
        return false;
    }

    // Approved fields only: never text, prompts, JSON, response bodies, exception messages or headers.
    private void LogFailure(ContentTranslationJob job, string code, int? httpStatus = null, string exceptionType = null) =>
        _logger.LogWarning("Content translation job {JobId} (content {ContentId}, attempt {Attempt}) failed with {FailureKind} (HTTP {HttpStatus}, {ExceptionType})",
            job.Id, job.ContentId, job.AttemptCount, code, httpStatus, exceptionType);

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
            catch (OperationCanceledException ex)
            {
                // Ambiguous: the provider may have processed (and billed) the request. Fail for
                // explicit retry. CancellationToken.None: record the outcome even during shutdown.
                var code = stoppingToken.IsCancellationRequested ? ContentTranslationErrorCodes.ProviderCancelled : ContentTranslationErrorCodes.ProviderTimeout;
                LogFailure(job, code, exceptionType: ex.GetType().Name);
                await Complete(job, ContentTranslationJobState.Failed, code, CancellationToken.None);
                return;
            }
        }

        // From here the provider work is paid for: finish recording it even if the host is stopping.
        if (!result.Success)
        {
            var code = PortFailureCodes.Contains(result.FailureCode ?? "") ? result.FailureCode : ContentTranslationErrorCodes.ProviderError;
            LogFailure(job, code, result.HttpStatus, result.ExceptionType);
            if (!ContentTranslationErrorCodes.IsRetryable(code))
                await Complete(job, ContentTranslationJobState.Failed, code, CancellationToken.None);
            else if (job.AttemptCount >= _options.MaxAttempts)
                await Complete(job, ContentTranslationJobState.Failed, ContentTranslationErrorCodes.ProviderRetriesExhausted, CancellationToken.None);
            else
                await Requeue(job, code, result.RetryAfter);
            return;
        }

        var localizedTextJson = TranslationSourceDocument.ToLocalizedTextJson(document, result.TranslatedJson);
        if (localizedTextJson == null)
        {
            LogFailure(job, ContentTranslationErrorCodes.InvalidOutput);
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

    private Task Requeue(ContentTranslationJob job, string code, TimeSpan? retryAfter)
    {
        // The provider's Retry-After when given, else 30s, 1m, 2m, ...; both capped at 30m.
        // ponytail: a longer Retry-After is cut to 30m and may spend another attempt early.
        var backoff = TimeSpan.FromSeconds(Math.Min(retryAfter?.TotalSeconds ?? 30 * Math.Pow(2, job.AttemptCount - 1), 1800));
        job.State = ContentTranslationJobState.Queued;
        job.NextAttemptAt = UtcNow + backoff;
        job.ErrorCode = code;
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
