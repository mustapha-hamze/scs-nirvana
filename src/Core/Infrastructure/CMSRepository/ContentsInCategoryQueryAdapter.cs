using Application.CMSRepository;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.CMSRepository;

// The only place in Content's persistence layer that touches Dapper/a raw SqlConnection/the
// connection string - everything else here is EF Core.
public class ContentsInCategoryQueryAdapter : IContentsInCategoryQueryAdapter
{
    private readonly string _connectionString;

    public ContentsInCategoryQueryAdapter(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId)
    {
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("@P_CategoryId", categoryId);
        parameters.Add("@P_ApplicationId", applicationId);

        // Short-lived connection, disposed even if the query throws, instead of a long-lived
        // field opened/closed by hand (which leaked an open connection on any exception between
        // Open() and Close(), and hid real failures behind an empty-list catch-all).
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var queryResult = await connection.QueryAsync<ContentDto>(
            "SP_ContentsInCategory", parameters, commandType: CommandType.StoredProcedure);

        return queryResult.ToList();
    }
}
