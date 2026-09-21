using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.General;

namespace Application.GeneralRepository
{
    // Culture is a global lookup in this codebase today, not scoped to an application anywhere
    // (see ContentServices.CreateContentCultures and CultureServices.List), so unlike the other
    // tenant-owned ports its Create/Delete/GetById are intentionally left unscoped rather than
    // requiring an applicationId that nothing here currently enforces.
    public interface ICultureRepository
    {
        Task<Culture> Create(Culture culture);
        Task Delete(int id, CancellationToken cancellationToken = default);
        Task<Culture> GetById(int id, CancellationToken cancellationToken = default);
        Task<List<Culture>> List(CancellationToken cancellationToken = default);
    }
}