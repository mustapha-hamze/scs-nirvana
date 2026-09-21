using System;

namespace Domains.Entities;

public class BaseEntity
{
    public int Id { get; set; }

    // Legacy column, required by schema but not read anywhere as a business rule today -
    // every read site just copies it through into a DTO. The one write site
    // (UserManagementRepository.SetUserAccesses) sets it to the literal 1 with no documented
    // meaning. Do not attach new semantics to it without a verified product rule.
    public int Status { get; set; }

    // Authoritative "this row no longer exists" flag. Enforced globally: every BaseEntity query
    // (including navigations) excludes IsDeleted rows via the EF query filter in
    // EntityTypeBuilderExtensions.ConfigureAudit. Set exclusively by ApplicationDbContext's
    // lifecycle policy, which turns an EF delete into IsDeleted = true rather than a physical
    // delete. Reading a soft-deleted row requires an explicit, commented IgnoreQueryFilters().
    public bool IsDeleted { get; set; }

    // Per-entity business enable/disable toggle (publish/unpublish a Content, show/hide a
    // SliderItem, active membership on UserInApplication), independent of deletion. Unlike
    // IsDeleted it is NOT enforced by a global query filter - each read path that cares about it
    // (e.g. GetUserApplications) checks IsActive explicitly. New rows typically default it to
    // true at creation; there is no single cross-entity meaning beyond "on/off" for that entity's
    // own use case.
    public bool IsActive { get; set; }
    public DateTime UpdatedDT { get; set; }
    public DateTime CreatedDT { get; set; }
}