using Web.Areas.BackOffice.Features.Content;

using Web.Areas.BackOffice.Features.Content.ViewModels;

namespace Web.Areas.BackOffice.Controllers;

// Sections/layout: the schema-driven section editor, creating/updating/deleting sections and
// their elements, and reordering them. ElementType -> field routing for create/update is pure
// and lives in SectionElementRequestMapper.
public partial class ContentController
{
    [HttpGet]
    [RequireAccess(AccessKeys.Content.PreviewBody)]
    [Route("/{area}/Content/ContentSections/{contentId}/{typeId}")]
    public async Task<IActionResult> ContentSections(int contentId, int typeId)
    {
        int schemaTypeId = 0;
        if (typeId >= 1111)
            schemaTypeId = 1000;
        else
            schemaTypeId = 1001;

        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var schemas = await _schemaServices.List(currentApplicationId, schemaTypeId);
        var sections = await _contentServices.GetSections(contentId, currentApplicationId);
        var priority = sections.Count > 0 ? sections[^1].Priority + 1 : 0;
        var canSaveBody = await _accessKeyAuthorizer.HasAccessAsync(User, currentApplicationId, AccessKeys.Content.SaveBody);

        return View(new ContentSectionsViewModel
        {
            Schemas = schemas,
            Sections = sections,
            Priority = priority,
            CanSaveBody = canSaveBody,
        });
    }

    [HttpGet]
    [RequireAccess(AccessKeys.Content.SaveBody)]
    [Route("/{area}/Content/CreateContentSection/{schemaId}/{priority}")]
    public async Task<IActionResult> CreateContentSection(int schemaId, int priority)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var schemaDetails = await _schemaServices.DetailsList(schemaId, currentApplicationId);
        return View(new CreateContentSectionViewModel { SchemaDetails = schemaDetails, Priority = priority });
    }

    [HttpPost]
    [RequireAccess(AccessKeys.Content.SaveBody)]
    public async Task<IActionResult> SaveSection([FromBody] SaveContentBodyDto section)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();

        if (section == null)
            return Content("Failed");

        if (section.SectionId != 0)
        {
            foreach (var item in section.Elements)
            {
                var updateDto = SectionElementRequestMapper.BuildForUpdate(item);
                if (updateDto != null)
                    await _contentServices.UpdateSectionElement(updateDto, currentApplicationId);
            }
            return Content("Done");
        }
        else
        {
            var _section = await _contentServices.CreateSection(new SectionDto
            {
                ContentId = section.ContentId,
                Priority = section.Priority,
                IsActive = true
            }, currentApplicationId);

            foreach (var item in section.Elements)
            {
                var createDto = SectionElementRequestMapper.BuildForCreate(item, _section.Id);
                if (createDto != null)
                    await _contentServices.CreateSectionElement(createDto, currentApplicationId);
            }
            return Content("Done");
        }
    }

    [HttpPost]
    [RequireAccess(AccessKeys.Content.SaveBody)]
    public async Task<IActionResult> UpdateSectionsLayoutOrder([FromBody] string sectionsOrder)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();

        var sectionsOrderArray = sectionsOrder.Split(',');
        for (int i = 0; i < sectionsOrderArray.Length - 1; i++)
        {
            await _contentServices.UpdateSectionPriority(Convert.ToInt32(sectionsOrderArray[i]), i + 1, currentApplicationId);
        }

        return Ok("Done");
    }

    [Route("/{area}/Content/DeleteSection/{id}")]
    [HttpDelete]
    [RequireAccess(AccessKeys.Content.SaveBody)]
    public async Task<IActionResult> DeleteSection(int id)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        await _contentServices.DeleteSection(id, currentApplicationId);
        return Content("Done");
    }
}
