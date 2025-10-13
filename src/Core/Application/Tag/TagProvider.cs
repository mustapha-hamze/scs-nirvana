using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using StackExchange.Redis;

namespace Application.Tag;

public class TagProvider : ITagProvider
{
    private readonly ApplicationDbContext _dbContext;
    public TagProvider(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Domains.Entities.General.Tag>> GetTags(int appId)
    {
        return await _dbContext.Tags.Where(t => t.ApplicationId == appId && !t.IsDeleted).ToListAsync();
        // var db = _redis.GetDatabase();
        // var tags = db.HashGet("hash_GNR_Tags", appId);

        // if (!string.IsNullOrEmpty(tags))
        // {
        //     JsonSerializerSettings jsonSerializerSettings = new()
        //     {
        //         NullValueHandling = NullValueHandling.Ignore,
        //         ContractResolver = new CamelCasePropertyNamesContractResolver(),
        //     };
        //     var deserializedTags = JsonConvert.DeserializeObject<List<Domains.Entities.General.Tag>>(tags, jsonSerializerSettings);
        //     return Task.FromResult(deserializedTags);
        // }

        // return Task.FromResult(new List<Domains.Entities.General.Tag>());

    }
}