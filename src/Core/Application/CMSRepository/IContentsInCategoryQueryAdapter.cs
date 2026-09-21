using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.CMS;

namespace Application.CMSRepository;

// SQL Server stored-procedure query adapter (SP_ContentsInCategory). Dapper and connection
// strings are an Infrastructure/SQL Server concern and must not leak into the EF-based read or
// write ports.
public interface IContentsInCategoryQueryAdapter
{
    Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId);
}
