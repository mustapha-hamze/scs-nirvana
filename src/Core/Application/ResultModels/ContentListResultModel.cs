using Domains.Entities.ContentManagement;

namespace Application.ResultModels;

public class ContentListResultModel
{
    public int PageCount { get; set; }
    public int CurrentPage { get; set; }
    public string Title { get; set; }

    public List<Content> Contents { get; set; }
}