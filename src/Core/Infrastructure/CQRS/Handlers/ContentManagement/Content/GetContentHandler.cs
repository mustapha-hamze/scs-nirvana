using Infrastructure.CQRS.Queries.ContentManagement.Content;
using Infrastructure.Dto.CMSDtos;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using StackExchange.Redis;

namespace Infrastructure.CQRS.Handlers.ContentManagement.Content
{
    public class GetContentHandler : IRequestHandler<GetContentQuery, ContentDto>
    {
        private readonly IConnectionMultiplexer _redis;
        public GetContentHandler(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }
        public Task<ContentDto> Handle(GetContentQuery request, CancellationToken cancellationToken)
        {
            var db = _redis.GetDatabase();
            var hashKey = $"{request.ApplicationId}-{request.TypeId}-{request.Id}";
            var _event = db.HashGet("hash_CMS_Contents", hashKey);

            if (!string.IsNullOrEmpty(_event))
            {
                JsonSerializerSettings jsonSerializerSettings = new()
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                };
                var deserializedEvent = JsonConvert.DeserializeObject<ContentDto>(_event, jsonSerializerSettings);
                return Task.FromResult(deserializedEvent);
            }

            return Task.FromResult(new ContentDto());
        }
    }
}