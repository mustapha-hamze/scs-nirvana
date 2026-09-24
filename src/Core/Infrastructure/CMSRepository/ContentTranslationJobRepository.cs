using Application.CMSRepository;
using Domains.Entities.General;

namespace Infrastructure.CMSRepository;

public class ContentTranslationJobRepository : IContentTranslationJobRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ContentTranslationJobRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ContentBelongsToApplication(int contentId, int applicationId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Contents.AnyAsync(c => c.Id == contentId && c.ApplicationId == applicationId, cancellationToken);
    }

    public Task<Culture> FindCulture(int cultureId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Cultures.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cultureId, cancellationToken);
    }

    public Task<Content> FindSourceGraph(int contentId, CancellationToken cancellationToken = default)
    {
        // Same shape as ContentTranslationRepository.GetSourceGraph, but a deleted content is a
        // normal outcome here (the job is superseded), not an error.
        return _dbContext.Contents.AsNoTracking()
            .Include(c => c.Metadata)
            .Include(c => c.Sections).ThenInclude(s => s.Elements)
            .AsSplitQuery()
            .SingleOrDefaultAsync(c => c.Id == contentId, cancellationToken);
    }

    public Task<ContentTranslation> FindTranslation(int contentId, int cultureId, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters: a soft-deleted row still occupies the unique (ContentId, CultureId)
        // key, so callers must see it rather than try to insert a second row.
        return _dbContext.ContentTranslations.IgnoreQueryFilters()
            .SingleOrDefaultAsync(t => t.ContentId == contentId && t.CultureId == cultureId, cancellationToken);
    }

    public void AddTranslation(ContentTranslation translation)
    {
        _dbContext.ContentTranslations.Add(translation);
    }

    public Task<ContentTranslationJob> FindJob(int contentId, int cultureId, string sourceFingerprint, CancellationToken cancellationToken = default)
    {
        return _dbContext.ContentTranslationJobs.SingleOrDefaultAsync(
            j => j.ContentId == contentId && j.CultureId == cultureId && j.SourceFingerprint == sourceFingerprint, cancellationToken);
    }

    public void AddJob(ContentTranslationJob job)
    {
        _dbContext.ContentTranslationJobs.Add(job);
    }

    public Task<ContentTranslationJob> FindNextClaimable(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        return _dbContext.ContentTranslationJobs
            .Where(j => (j.State == ContentTranslationJobState.Queued && j.NextAttemptAt <= utcNow)
                || (j.State == ContentTranslationJobState.Processing && j.LeaseExpiresAt <= utcNow))
            .OrderBy(j => j.NextAttemptAt).ThenBy(j => j.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TrySaveChanges(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Concurrency-token mismatch or unique-key violation: another writer won. SaveChanges
            // is one transaction, so nothing was written; drop the staged state so a later save in
            // this scope can't replay it.
            _dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
