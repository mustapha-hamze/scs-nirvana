using System;
using System.Linq;
using System.Threading.Tasks;
using Application.CMSRepository;
using Application.UnitOfWork;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

public record ContentTranslationBackfillBatchResult(int Scanned, int ImportedStale, int NeedsReview, int SkippedExisting,
    int NextCheckpoint, bool Completed);

// Operator-invoked, resumable backfill of legacy Content.FarsiContent into ContentTranslation.
// Not a hosted service, not scheduled, not routed: an operator calls RunBatch repeatedly with the
// same runKey until Completed. No translator/OpenAI dependency - imports are text copies only.
//
// Conservative by design: a valid import is Stale (never Ready, a human or later pipeline must
// confirm it), anything the parser rejects is NeedsReview with a reason code and no payload, and
// an existing (ContentId, CultureId) row is never overwritten. FarsiContent is only ever read.
public class ContentTranslationBackfill
{
    public const string LegacyProvider = "legacy-farsicontent";
    public const int MaxBatchSize = 100;

    private readonly IContentTranslationBackfillRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ContentTranslationBackfill(IContentTranslationBackfillRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    // One batch, its translations and its checkpoint advance commit in one transaction; a retry
    // after failure resumes from the last committed checkpoint.
    public async Task<ContentTranslationBackfillBatchResult> RunBatch(int cultureId, string runKey, int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runKey) || runKey.Length > 100)
            throw new ArgumentException("Run key must be 1..100 characters.", nameof(runKey));
        if (batchSize is < 1 or > MaxBatchSize)
            throw new ArgumentOutOfRangeException(nameof(batchSize), $"Batch size must be 1..{MaxBatchSize}.");
        if (!await _repository.CultureExists(cultureId, cancellationToken))
            throw new InvalidOperationException($"Culture {cultureId} does not exist.");

        ContentTranslationBackfillBatchResult result = null;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var checkpoint = await _repository.GetCheckpoint(runKey, cancellationToken);
            if (checkpoint == null)
            {
                // A concurrent first batch for the same runKey fails on the unique RunKey index.
                checkpoint = new ContentTranslationBackfillCheckpoint { RunKey = runKey, CultureId = cultureId, IsActive = true };
                _repository.AddCheckpoint(checkpoint);
            }
            else if (checkpoint.CultureId != cultureId)
            {
                throw new InvalidOperationException($"Run key '{runKey}' belongs to a different culture.");
            }

            var contents = await _repository.GetLegacyFarsiBatch(checkpoint.LastContentId, batchSize, cancellationToken);
            var existing = (await _repository.GetTranslatedContentIds(contents.Select(c => c.Id).ToList(), cultureId, cancellationToken)).ToHashSet();

            int imported = 0, needsReview = 0;
            foreach (var content in contents.Where(c => !existing.Contains(c.Id)))
            {
                var parsed = LegacyFarsiContentParser.Parse(content.FarsiContent, content);
                var valid = parsed.ReasonCode == null;
                _repository.AddTranslation(new ContentTranslation
                {
                    ContentId = content.Id,
                    CultureId = cultureId,
                    TranslationStatus = valid ? TranslationStatus.Stale : TranslationStatus.NeedsReview,
                    SourceFingerprint = ContentSourceFingerprint.Compute(content),
                    LocalizedTextJson = parsed.LocalizedTextJson,
                    Provider = LegacyProvider,
                    TranslatedAt = null,
                    Error = parsed.ReasonCode,
                    IsActive = true
                });
                if (valid) imported++;
                else needsReview++;
            }

            var skipped = contents.Count - imported - needsReview;
            if (contents.Count > 0)
                checkpoint.LastContentId = contents[^1].Id;
            checkpoint.ScannedCount += contents.Count;
            checkpoint.ImportedStaleCount += imported;
            checkpoint.NeedsReviewCount += needsReview;
            checkpoint.SkippedExistingCount += skipped;
            checkpoint.Version++;

            result = new ContentTranslationBackfillBatchResult(contents.Count, imported, needsReview, skipped,
                checkpoint.LastContentId, contents.Count < batchSize);
        }, cancellationToken);

        return result;
    }
}
