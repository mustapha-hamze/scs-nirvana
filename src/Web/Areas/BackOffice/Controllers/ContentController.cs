using Application.ContentManagement;
using Core.Services.TranslatorServices;
using Domains.Entities.ContentManagement;
using SkiaSharp;

namespace Web.Areas.BackOffice.Controllers;

[Authorize]

[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class ContentController : BaseController
{
    // fields
    #region fields
    private readonly IContentServices _contentServices;
    private readonly IContentProvider _contentProvider;
    private readonly ISchemaServices _schemaServices;
    private readonly ICategoryServices _categoryServices;
    private readonly ITagServices _tagServices;
    private readonly ICultureServices _cultureServices;
    private readonly IHostEnvironment _appEnvironment;
    private readonly IApplicationServices _applicationServices;
    private readonly ISystemTypeServices _systemTypeServices;
    private readonly IUserManagementServices _userManagementServices;
    private readonly IContentTranslator _contentTranslator;

    private static readonly JsonSerializerSettings _farsiJsonSettings = new JsonSerializerSettings
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        NullValueHandling = NullValueHandling.Include
    };
    #endregion

    // constructor
    #region constructor
    public ContentController(IContentServices contentServices, ISchemaServices schemaServices,
        ICategoryServices categoryServices,
        ITagServices tagServices, ICultureServices cultureServices, IHostEnvironment appEnvironment,
        IApplicationServices applicationServices, ISystemTypeServices systemTypeServices,
        IUserManagementServices userManagementServices, IContentTranslator contentTranslator,
        IContentProvider contentProvider)
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
        _contentTranslator = contentTranslator;
        _contentProvider = contentProvider;
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
                TypeId = typeId,
                PublishDt = DateTime.Now // Set default publish date to now
            });
        }
    }

    [Route("/{area}/Content/ContentSections/{contentId}/{typeId}")]
    public IActionResult ContentSections(int contentId, int typeId)
    {
        int schemaTypeId = 0;
        if (typeId >= 1111)
            schemaTypeId = 1000;
        else
            schemaTypeId = 1001;

        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        ViewData["Schemas"] = _schemaServices.List(user.CurrentApplicationId, schemaTypeId);
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

    [Route("/{area}/{controller}/FarsiContentForm/{id}/{typeId}")]
    public async Task<IActionResult> FarsiContentForm(int id, int typeId)
    {
        var englishContent = await _contentProvider.GetContentForTranslate(id);
        if (englishContent == null)
            return NotFound();

        ViewData["TypeId"] = typeId;

        var (source, usedEnglishFallback) = GetFarsiEditSource(englishContent);
        ViewBag.FarsiInitializedFromEnglish = usedEnglishFallback;

        return View(MapToFarsiEditDto(source));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFarsiContentForm(FarsiContentEditDto model)
    {
        if (model == null || model.Id == 0)
            return Content("Failed");

        var englishContent = await _contentProvider.GetContentForTranslate(model.Id);
        if (englishContent == null)
            return NotFound();

        var (baseContent, _) = GetFarsiEditSource(englishContent);

        baseContent.Title = model.Title;
        baseContent.HeadLine = model.HeadLine;
        baseContent.Abstract = model.Abstract;
        baseContent.Description = model.Description;

        if (model.Metadata != null)
        {
            if (baseContent.Metadata == null)
                baseContent.Metadata = new ContentMetadata { ContentId = baseContent.Id };

            baseContent.Metadata.Title = model.Metadata.Title;
            baseContent.Metadata.Author = model.Metadata.Author;
            baseContent.Metadata.Keywords = model.Metadata.Keywords;
            baseContent.Metadata.Description = model.Metadata.Description;
        }

        if (model.Sections != null && baseContent.Sections != null)
        {
            foreach (var sectionEdit in model.Sections)
            {
                var section = baseContent.Sections.FirstOrDefault(s => s.Id == sectionEdit.Id);
                if (section?.Elements == null || sectionEdit.SectionElements == null)
                    continue;

                foreach (var elementEdit in sectionEdit.SectionElements)
                {
                    var element = section.Elements.FirstOrDefault(e => e.Id == elementEdit.Id);
                    if (element == null)
                        continue;

                    switch (element.ElementType)
                    {
                        case 1000:
                        case 1006:
                        case 1007:
                        case 1008:
                        case 1009:
                        case 1010:
                        case 1011:
                            element.TinyText = elementEdit.TinyText;
                            break;
                        case 1002:
                        case 1005:
                            element.EditorText = elementEdit.EditorText;
                            break;
                    }
                }
            }
        }

        var farsiJson = JsonConvert.SerializeObject(baseContent, _farsiJsonSettings);

        // UpdateTranslate now queries without AsNoTracking, so it safely resolves to the
        // already-tracked `englishContent` instance instead of conflicting with it.
        await _contentServices.UpdateTranslate(model.Id, farsiJson);

        return Content("Done");
    }

    // Returns the Content graph to use as the editable Farsi source: the previously saved
    // FarsiContent JSON when present and valid, otherwise the English content as a starting point.
    // Always returns a detached clone (never `englishContent` itself) so mutating it for Farsi
    // edits can never leak into the EF-tracked English entity, and so it can't collide with
    // `englishContent`'s own tracking identity when later saved.
    private (Content Source, bool UsedEnglishFallback) GetFarsiEditSource(Content englishContent)
    {
        if (string.IsNullOrWhiteSpace(englishContent.FarsiContent))
            return (CloneDetached(englishContent), true);

        try
        {
            var parsed = JsonConvert.DeserializeObject<Content>(englishContent.FarsiContent, _farsiJsonSettings);
            return parsed == null ? (CloneDetached(englishContent), true) : (parsed, false);
        }
        catch (JsonException)
        {
            return (CloneDetached(englishContent), true);
        }
    }

    private static Content CloneDetached(Content content)
    {
        return JsonConvert.DeserializeObject<Content>(JsonConvert.SerializeObject(content, _farsiJsonSettings), _farsiJsonSettings);
    }

    private static FarsiContentEditDto MapToFarsiEditDto(Content content)
    {
        return new FarsiContentEditDto
        {
            Id = content.Id,
            ApplicationId = content.ApplicationId,
            TypeId = content.TypeId,
            Title = content.Title,
            HeadLine = content.HeadLine,
            Abstract = content.Abstract,
            Description = content.Description,
            PublishDt = content.PublishDt,
            Metadata = new FarsiContentMetadataEditDto
            {
                Id = content.Metadata?.Id ?? 0,
                ContentId = content.Id,
                Title = content.Metadata?.Title,
                Author = content.Metadata?.Author,
                Keywords = content.Metadata?.Keywords,
                Description = content.Metadata?.Description
            },
            Sections = (content.Sections ?? new List<ContentSection>())
                .OrderBy(s => s.Priority)
                .Select(s => new FarsiSectionEditDto
                {
                    Id = s.Id,
                    ContentId = s.ContentId,
                    Priority = s.Priority,
                    SectionElements = (s.Elements ?? new List<SectionElement>())
                        .Select(e => new FarsiSectionElementEditDto
                        {
                            Id = e.Id,
                            SectionId = e.SectionId,
                            ElementType = e.ElementType,
                            TinyText = e.TinyText,
                            EditorText = e.EditorText,
                            FileNameText = e.FileNameText,
                            GalleryImages = e.GalleryImages,
                            ElementTitle = e.ElementTitle,
                            Size = e.Size
                        }).ToList()
                }).ToList()
        };
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/{area}/Content/ChangeContentActiveMode/{typeId}/{contentId}/{mode}")]
    public async Task<IActionResult> ChangeContentActiveMode(int typeId, int contentId, bool mode)
    {
        if (mode)
        {
            var content = await _contentProvider.GetContentForTranslate(contentId);
            content.FarsiContent = "";
            var result = await _contentTranslator.Translate(content);
            await _contentServices.ActivateTranslatedContent(contentId, result);
        }
        else
        {
            await _contentServices.ChangeContentActiveMode(contentId, mode);
        }

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

    [HttpDelete]
    [ValidateAntiForgeryToken]
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
                                EditorText = item.Value.Replace("<p></p>", "").Replace("<p> </p>", "").Replace("\n", ""),
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
                        case 1005:
                            await _contentServices.UpdateSectionElement(new SectionElementDto
                            {
                                Id = item.Id,
                                EditorText = item.Value,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1006:
                            await _contentServices.UpdateSectionElement(new SectionElementDto
                            {
                                Id = item.Id,
                                TinyText = item.Value,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1007:
                            await _contentServices.UpdateSectionElement(new SectionElementDto
                            {
                                Id = item.Id,
                                TinyText = item.Value,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1008:
                            await _contentServices.UpdateSectionElement(new SectionElementDto
                            {
                                Id = item.Id,
                                TinyText = item.Value,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1009:
                            await _contentServices.UpdateSectionElement(new SectionElementDto
                            {
                                Id = item.Id,
                                TinyText = item.Value,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1010:
                            await _contentServices.UpdateSectionElement(new SectionElementDto
                            {
                                Id = item.Id,
                                TinyText = item.Value,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1011:
                            await _contentServices.UpdateSectionElement(new SectionElementDto
                            {
                                Id = item.Id,
                                TinyText = item.Value,
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
                        case 1005:
                            await _contentServices.CreateSectionElement(new SectionElementDto
                            {
                                EditorText = item.Value.Replace("<p></p>", "").Replace("<p> </p>", "").Replace("\n", ""),
                                SectionId = _section.Id,
                                IsActive = true,
                                ElementType = 1005,
                                Size = item.Size,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1006:
                            await _contentServices.CreateSectionElement(new SectionElementDto
                            {
                                TinyText = item.Value,
                                SectionId = _section.Id,
                                IsActive = true,
                                ElementType = 1006,
                                Size = item.Size,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1007:
                            await _contentServices.CreateSectionElement(new SectionElementDto
                            {
                                TinyText = item.Value,
                                SectionId = _section.Id,
                                IsActive = true,
                                ElementType = 1007,
                                Size = item.Size,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1008:
                            await _contentServices.CreateSectionElement(new SectionElementDto
                            {
                                TinyText = item.Value,
                                SectionId = _section.Id,
                                IsActive = true,
                                ElementType = 1008,
                                Size = item.Size,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1009:
                            await _contentServices.CreateSectionElement(new SectionElementDto
                            {
                                TinyText = item.Value,
                                SectionId = _section.Id,
                                IsActive = true,
                                ElementType = 1009,
                                Size = item.Size,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1010:
                            await _contentServices.CreateSectionElement(new SectionElementDto
                            {
                                TinyText = item.Value,
                                SectionId = _section.Id,
                                IsActive = true,
                                ElementType = 1010,
                                Size = item.Size,
                                ElementTitle = item.Title
                            });
                            break;
                        case 1011:
                            await _contentServices.CreateSectionElement(new SectionElementDto
                            {
                                TinyText = item.Value,
                                SectionId = _section.Id,
                                IsActive = true,
                                ElementType = 1011,
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
    public async Task<IActionResult> UpdateSectionsLayoutOrder([FromBody] string sectionsOrder)
    {
        var sectionsOrderArray = sectionsOrder.Split(',');
        for (int i = 0; i < sectionsOrderArray.Length - 1; i++)
        {
            await _contentServices.UpdateSectionPriority(Convert.ToInt32(sectionsOrderArray[i]), i + 1);
        }

        return Ok("Done");
    }

    [HttpPost]
    public async Task<IActionResult> UploadBodyImage(IFormFile File, string Extension)
    {
        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Section/Images/");

        var _extension = Extension.Split('/');
        string ImageName = Guid.NewGuid().ToString();

        if (await UploadImage(File, savePath, ImageName + "." + _extension[1]))
            return Content("Done|" + ImageName + "." + _extension[1]);
        else
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
    public async Task<IActionResult> UploadBodyImageGallery()
    {
        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Section/Gallery/");

        string result = string.Empty;
        var uploadedImages = Request.Form.Files;
        foreach (var item in uploadedImages)
        {
            string FileName = Guid.NewGuid().ToString();
            var fileNameArray = item.FileName.ToString().Split(".");
            string fileExtension = fileNameArray[fileNameArray.Length - 1];
            if (await UploadImage(item, savePath, FileName + "." + fileExtension))
                result += FileName + "." + fileExtension + ",";
        }

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

        var currentImageSettings = imageSettings.Single(s => s.Id == settingId);

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);

        foreach (var item in currentImageSettings.Value.Split(","))
        {
            var sizes = item.Split("-");
            int targetWidth = Convert.ToInt32(sizes[0]);
            int targetHeight = Convert.ToInt32(sizes[1]);

            memoryStream.Position = 0;
            using var original = SKBitmap.Decode(memoryStream);
            using var resized = original.Resize(
                new SKImageInfo(targetWidth, targetHeight),
                SKSamplingOptions.Default
            );
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(GetSKEncodedImageFormat(fileExtension), 90);

            string imageName = Guid.NewGuid().ToString();
            string fullPath = Path.Combine(savePath, imageName + "." + fileExtension);

            // Use System.IO.File instead of File
            using var fileStream = System.IO.File.OpenWrite(fullPath);
            data.SaveTo(fileStream);

            await _contentServices.CreateContentImage(new ContentImageDto
            {
                ContentId = contentId,
                ImageFileName = imageName + "." + fileExtension,
                IsActive = true,
                Size = targetWidth
            });
        }

        return Content("Done," + arrImagesName);
    }

    private SKEncodedImageFormat GetSKEncodedImageFormat(string extension)
    {
        return extension.ToLower() switch
        {
            "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
            "png" => SKEncodedImageFormat.Png,
            "gif" => SKEncodedImageFormat.Gif,
            "bmp" => SKEncodedImageFormat.Bmp,
            "webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Jpeg
        };
    }

    [Route("/{area}/Content/DeleteSection/{id}")]
    [HttpDelete]
    public async Task<IActionResult> DeleteSection(int id)
    {
        //TODO: Implement Realistic Implementation
        await _contentServices.DeleteSection(id);
        return Content("Done");
    }
    #endregion
}