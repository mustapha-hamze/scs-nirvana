namespace Application.Tag;

public interface ITagProvider
{
    Task<List<Domains.Entities.General.Tag>> GetTags(int appId);
}