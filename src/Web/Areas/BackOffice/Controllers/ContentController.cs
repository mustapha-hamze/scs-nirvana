namespace Web.Areas.BackOffice.Controllers;

[Authorize]

[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class ContentController : BaseController
{
    // fields
    #region fields
    private readonly IContentServices _contentServices;
    private readonly ISchemaServices _schemaServices;
    private readonly ICategoryServices _categoryServices;
    private readonly ITagServices _tagServices;
    private readonly ICultureServices _cultureServices;
    private readonly IHostEnvironment _appEnvironment;
    private readonly IApplicationServices _applicationServices;
    private readonly ISystemTypeServices _systemTypeServices;
    private readonly IUserManagementServices _userManagementServices;
    #endregion

    // constructor
    #region constructor
    public ContentController(IContentServices contentServices, ISchemaServices schemaServices,
        ICategoryServices categoryServices,
        ITagServices tagServices, ICultureServices cultureServices, IHostEnvironment appEnvironment,
        IApplicationServices applicationServices, ISystemTypeServices systemTypeServices,
        IUserManagementServices userManagementServices)
    {
        _applicationServices = applicationServices;
        _contentServices = contentServices;
        _schemaServices = schemaServices;
        _categoryServices = categoryServices;
        _tagServices = tagServices;
        _cultureServices = cultureServices;
        _appEnvironment = appEnvironment;
        _systemTypeServices = systemTypeServices;
        _userManagementServices = userManagementServices;
    }
    #endregion

    // methods
    #region methods

    [Route("/{area}/{controller}/Index/{id}")]
    public IActionResult Index(int id)
    {
        //TODO: Implement Realistic Implementation
        ViewData["TypeId"] = id;
        return View();
    }

    [Route("/{area}/{controller}/ContentForm/{id}/{typeId}")]
    public async Task<IActionResult> ContentForm(int id = 0, int typeId = 0)
    {
        ViewData["TypeId"] = typeId;
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        //TODO: Implement Realistic Implementation
        ViewData["Types"] = _systemTypeServices.GetTypesInTypeGroup(user.CurrentApplicationId, TypeId.Content);
        if (id != 0)
        {
            var content = await _contentServices.GetById(id);
            var appSetting = _applicationServices.GetApplicationSetting(user.CurrentApplicationId, 5000);
            ViewData["WebsiteUrl"] = appSetting[0].Value;
            return View(content);
        }
        else
        {
            return View(new ContentDto
            {
                PublishDt = DateTime.Now // Set default publish date to now
            });
        }
    }

    [Route("/{area}/Content/ContentSections/{contentId}")]
    public IActionResult ContentSections(int contentId)
    {
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        ViewData["Schemas"] = _schemaServices.List(user.CurrentApplicationId);
        ViewData["Sections"] = _contentServices.GetSections(contentId);
        return View();
    }

    [Route("/{area}/Content/ContentRelations/{contentId}")]
    public async Task<IActionResult> ContentRelations(int contentId)
    {
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        ViewData["Categories"] = _categoryServices.GetAllFullPath(user.CurrentApplicationId);
        ViewData["Tags"] = _tagServices.FindTagsByTypeId(user.CurrentApplicationId, TypeId.Content);
        ViewData["Cultures"] = _cultureServices.List();
        //TODO: Implement Realistic Implementation
        var content = await _contentServices.GetById(contentId);
        return View(content);
    }

    [Route("/{area}/Content/ContentMetadata/{contentId}")]
    public IActionResult ContentMetadata(int contentId)
    {
        //TODO: Implement Realistic Implementation
        var contentMetadata = _contentServices.GetContentMetadata(contentId);
        contentMetadata.ContentId = contentId;

        return View(contentMetadata);
    }

    [Route("/{area}/Content/ContentImages/{contentId}")]
    public IActionResult ContentImages(int contentId)
    {
        //TODO: Implement Realistic Implementation
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        ViewData["ContentImageAspectRatio"] = _applicationServices.GetApplicationSetting(user.CurrentApplicationId, 1001);
        ViewData["ContentImageSizes"] = _applicationServices.GetApplicationSetting(user.CurrentApplicationId, 1000);
        ViewData["ContentImage"] = _contentServices.GetAllContentImages(contentId);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveContentForm(ContentDto content)
    {
        Console.WriteLine("==============================================" + content.PublishDt);
        //TODO: Implement Realistic Implementation
        if (content.Id == 0)
        {
            var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
            content.ApplicationId = user.CurrentApplicationId;
            var _content = await _contentServices.Create(content);
            return RedirectToAction("ContentForm", new { id = _content.Id, typeId = _content.TypeId });
        }
        else
        {
            var _content = await _contentServices.GetById(content.Id);
            content.Categories = _content.Categories;
            content.Tags = _content.Tags;
            content.Cultures = _content.Cultures;
            await _contentServices.Update(content);

            return RedirectToAction("ContentForm", new { id = content.Id, typeId = _content.TypeId });
        }
    }

    [Route("/{area}/{controller}/{action}/{id}")]
    public IActionResult ContentList(int id)
    {
        //TODO: Implement Realistic Implementation
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        var contents = _contentServices.List(user.CurrentApplicationId).Where(c => c.TypeId == id).ToList();
        ViewData["Types"] = _systemTypeServices.GetTypesInTypeGroup(user.CurrentApplicationId, TypeId.Content);
        return View(contents);
    }

    [Route("/{area}/Content/ChangeContentActiveMode/{typeId}/{contentId}/{mode}")]
    public async Task<IActionResult> ChangeContentActiveMode(int typeId, int contentId, bool mode)
    {
        //TODO: Implement Realistic Implementation
        await _contentServices.ChangeContentActiveMode(contentId, mode);

        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);

        var frontContentTypes = _applicationServices.GetApplicationSetting(user.CurrentApplicationId, 1002);
        if (frontContentTypes.Any(x => x.Value.Contains(typeId.ToString())))
        {
            var frontContentTypeIds = new List<int>();
            foreach (var contentType in frontContentTypes)
                frontContentTypeIds.Add(Convert.ToInt32(contentType.Value));
        }

        return Content("Done");
    }

    [Route("/{area}/Content/CreateContentSection/{schemaId}/{priority}")]
    public IActionResult CreateContentSection(int schemaId, int priority)
    {
        //TODO: Implement Realistic Implementation
        ViewData["Priority"] = priority;
        var schemaDetails = _schemaServices.DetailsList(schemaId);
        return View(schemaDetails);
    }

    [Route("/{area}/Content/DeleteContent/{id}")]
    public async Task<IActionResult> DeleteContent(int id)
    {
        await _contentServices.Delete(id);
        return Content("Done");
    }

    [HttpPost]
    [Route("/{area}/Content/SaveRelation/{Entity}/{contentId}")]
    public async Task<IActionResult> SaveRelation([FromForm] string Data, string Entity, int contentId)
    {
        if (Data != "" && Data != string.Empty && Data != null)
            Data = Data[..^1];
        switch (Entity)
        {
            case "Category":
                await _contentServices.CreateContentCategories(Data, Entity, contentId);
                break;
            case "Tag":
                await _contentServices.CreateContentTags(Data, Entity, contentId);
                break;
            case "Culture":
                await _contentServices.CreateContentCultures(Data, Entity, contentId);
                break;
        }

        return Content("Done");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveContentMetadata(ContentMetadataDto contentMetadata)
    {
        if (contentMetadata.Id == 0)
            await _contentServices.CreateContentMetadata(contentMetadata);
        else
            await _contentServices.UpdateContentMetadata(contentMetadata);

        return Content("Done");
    }

    [HttpPost]
    public async Task<IActionResult> SaveSection([FromBody] SaveContentBodyDto section)
    {
        if (section != null)
        {
            if (section.SectionId != 0)
            {
                foreach (var item in section.Elements)
                {
                    switch (item.ElementType)
                    {
                        case 1000:
                            await _contentServices.UpdateSectionElement(new SectionElementDto
                            {
                                Id = item.Id,
                                TinyText = item.Value,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1001:
                            await _contentServices.UpdateSectionElement(new SectionElementDto
                            {
                                Id = item.Id,
                                FileNameText = item.Value,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1002:
                            await _contentServices.UpdateSectionElement(new SectionElementDto
                            {
                                Id = item.Id,
                                EditorText = item.Value,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1003:
                            await _contentServices.UpdateSectionElement(new SectionElementDto
                            {
                                Id = item.Id,
                                GalleryImages = item.Value,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1004:
                            await _contentServices.UpdateSectionElement(new SectionElementDto
                            {
                                Id = item.Id,
                                FileNameText = item.Value,
                                ElementTitle = item.Title
                            });
                            break;
                    }
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
                });

                foreach (var item in section.Elements)
                {
                    switch (item.ElementType)
                    {
                        case 1000:
                            await _contentServices.CreateSectionElement(new SectionElementDto
                            {
                                TinyText = item.Value,
                                SectionId = _section.Id,
                                IsActive = true,
                                ElementType = 1000,
                                Size = item.Size,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1001:
                            await _contentServices.CreateSectionElement(new SectionElementDto
                            {
                                FileNameText = item.Value,
                                SectionId = _section.Id,
                                IsActive = true,
                                ElementType = 1001,
                                Size = item.Size,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1002:
                            await _contentServices.CreateSectionElement(new SectionElementDto
                            {
                                EditorText = item.Value,
                                SectionId = _section.Id,
                                IsActive = true,
                                ElementType = 1002,
                                Size = item.Size,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1003:
                            await _contentServices.CreateSectionElement(new SectionElementDto
                            {
                                GalleryImages = item.Value,
                                SectionId = _section.Id,
                                IsActive = true,
                                ElementType = 1003,
                                Size = item.Size,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1004:
                            await _contentServices.CreateSectionElement(new SectionElementDto
                            {
                                FileNameText = item.Value,
                                SectionId = _section.Id,
                                IsActive = true,
                                ElementType = 1004,
                                Size = item.Size,
                                ElementTitle = item.Title
                            });
                            break;
                    }
                }
                return Content("Done");
            }
        }
        else
        {
            return Content("Failed");
        }
        // return Content(Newtonsoft.Json.JsonConvert.SerializeObject(section));
    }

    [HttpPost]
    public IActionResult UploadBodyImage(IFormFile File, string Extension)
    {
        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Section/Images/");

        var _extension = Extension.Split('/');
        string ImageName = Guid.NewGuid().ToString();

        // if (await UploadImage(File, savePath, ImageName + "." + _extension[1]))
        //     return Content("Done|" + ImageName + "." + _extension[1]);
        // else
            return Content("Failed");
    }

    [HttpPost]
    public async Task<IActionResult> UploadBodyFile(IFormFile File, string FileName, string Extension)
    {
        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Section/Files/");

        var _extension = Extension.Split('/');

        if (await UploadFile(File, savePath, FileName + "." + _extension[1]))
            return Content("Done|" + FileName + "." + _extension[1]);
        else
            return Content("Failed");
    }

    [HttpPost]
    public IActionResult UploadBodyImageGallery()
    {
        //TODO: Implement Realistic Implementation
        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Section/Gallery/");

        // 

        // if (await UploadImage(File, savePath, FileName + "." + _extension[1]))
        //     return Content("Done|" + FileName + "." + _extension[1]);
        // else
        string result = string.Empty;
        var uploadedImages = Request.Form.Files;
        // foreach (var item in uploadedImages)
        // {
        //     string FileName = Guid.NewGuid().ToString();
        //     var fileNameArray = item.FileName.ToString().Split(".");
        //     string fileExtension = fileNameArray[fileNameArray.Length - 1];
        //     if (await UploadImage(item, savePath, FileName + "." + fileExtension))
        //         result += FileName + "." + fileExtension + ",";
        // }

        return Content("Done|" + result);
    }

    [HttpPost]
    public async Task<IActionResult> UploadContentImage(IFormFile file, int contentId, int settingId)
    {
        //TODO: Implement Realistic Implementation
        if (file.Length == 0)
            return Content("Failed");

        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Content/Image/" + contentId);
        if (!Directory.Exists(savePath))
            Directory.CreateDirectory(savePath);
        else
        {
            foreach (var item in Directory.GetFiles(savePath))
                System.IO.File.Delete(item);
        }

        await _contentServices.DeleteAllContentImages(contentId);

        var _fileName = file.FileName.Split(".");
        var fileExtension = _fileName[_fileName.Length - 1];

        string arrImagesName = string.Empty;

        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        var imageSettings = _applicationServices.GetApplicationSetting(user.CurrentApplicationId, 1000);

        // var currentImageSettings = imageSettings.Single(s => s.Id == settingId);
        // foreach (var item in currentImageSettings.Value.Split(","))
        // {
        //     var sizes = item.Split("-");
        //     using var image = Image.Load(file.OpenReadStream());
        //     image.Mutate(x => x.Resize(Convert.ToInt32(sizes[0]), Convert.ToInt32(sizes[1])));

        //     string imageName = Guid.NewGuid().ToString();
        //     image.Save(savePath + "/" + imageName + "." + fileExtention);

        //     await _contentServices.CreateContentImage(new ContentImageDto
        //     {
        //         ContentId = contentId,
        //         ImageFileName = imageName + "." + fileExtention,
        //         IsActive = true,
        //         Size = Convert.ToInt32(sizes[0])
        //     });
        // }

        return Content("Done," + arrImagesName);
    }

    [Route("/{area}/Content/DeleteSection/{id}")]
    public async Task<IActionResult> DeleteSection(int id)
    {
        //TODO: Implement Realistic Implementation
        await _contentServices.DeleteSection(id);
        return Content("Done");
    }
    #endregion
}