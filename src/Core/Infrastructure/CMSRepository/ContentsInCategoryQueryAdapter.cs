using Application.CMSRepository;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.CMSRepository;

// The only place in Content's persistence layer that touches Dapper/a raw SqlConnection/the
// connection string - everything here is EF Core.
//
// EF Core's global soft-delete query filter (ConfigureAudit<T> -> HasQueryFilter) does not, and
// cannot, apply here: SP_ContentsInCategory runs as raw SQL against SQL Server, entirely outside
// EF's query pipeline. Any soft-delete/active gating for the rows it returns has to live in the
// stored procedure itself (production schema is externally managed - not something this codebase
// can inspect or change) or be re-checked by the caller. ContentServices.GetContentsInCategory
// already gates the category itself (must be active/not-deleted/owned by applicationId) before
// calling this; it does not re-check each returned Content row, so if the stored procedure ever
// stops filtering IsDeleted/IsActive on Content internally, a soft-deleted Content could leak
// through undetected. See the Phase 5 schema-readiness doc for auditing the procedure itself.
public class ContentsInCategoryQueryAdapter : IContentsInCategoryQueryAdapter
{
    private readonly string _connectionString;

    public ContentsInCategoryQueryAdapter(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId, CancellationToken cancellationToken = default)
    {
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("@P_CategoryId", categoryId);
        parameters.Add("@P_ApplicationId", applicationId);

        // Short-lived connection, disposed even if the query throws, instead of a long-lived
        // field opened/closed by hand (which leaked an open connection on any exception between
        // Open() and Close(), and hid real failures behind an empty-list catch-all).
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(
            "SP_ContentsInCategory", parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);
        var queryResult = await connection.QueryAsync<ContentDto>(command);

        return queryResult.ToList();
    }
}
