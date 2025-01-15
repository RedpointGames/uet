namespace Redpoint.CloudFramework.Event
{
    using System;
    using System.Threading.Tasks;
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Event.PubSub;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NodaTime;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.GoogleInfrastructure;
    using Redpoint.CloudFramework.Tracing;

    public class EventApi : IEventApi
    {
        private readonly IGlobalPrefix _globalPrefix;
        private readonly IPubSub _pubSub;
        private readonly IManagedTracer _managedTracer;
        private readonly IGoogleServices _googleServices;
        private Random _random = new Random();

        public EventApi(
            IGlobalPrefix globalPrefix,
            IPubSub pubSub,
            IManagedTracer managedTracer,
            IGoogleServices googleServices)
        {
            _globalPrefix = globalPrefix;
            _pubSub = pubSub;
            _managedTracer = managedTracer;
            _googleServices = googleServices;
        }

        public async Task Raise(string eventType, Key project, Key session, HttpRequest request, Key key, object entity, object userdata)
        {
            SerializedEvent eventObj;

            using (_managedTracer.StartSpan("event.serialize", eventType))
            {
                var generatedIdBytes = new byte[7];
#pragma warning disable CA5394 // Do not use insecure randomness
                _random.NextBytes(generatedIdBytes);
#pragma warning restore CA5394 // Do not use insecure randomness
                var generatedIdString = Convert.ToHexString(generatedIdBytes);
                var generatedId = Convert.ToInt64(generatedIdString, 16);

                var generatedKeyFactory = new KeyFactory(_googleServices.ProjectId, string.Empty, "HiveEvent");
                var generatedKey = generatedKeyFactory.CreateKey(generatedId);

                // TODO: Potentially get this from environment variables now.
                var serviceIdentifier = "hivemp:unknown";

                var data = new Event
                {
                    Id = generatedKey,
                    UtcTimestamp = SystemClock.Instance.GetCurrentInstant(),
                    EventType = eventType,
                    ServiceIdentifier = serviceIdentifier,
                    Project = project,
                    Session = session,
                    Request = EventApi.FormatRequest(request),
                    Key = key,
                    Entity = EventApi.FormatEntity(entity),
                    Userdata = EventApi.FormatUserdata(userdata)
                };

                eventObj = SerializeEvent(data);
            }

            using (_managedTracer.StartSpan("event.publish", eventType))
            {
                await _pubSub.PublishAsync(eventObj).ConfigureAwait(false);
            }
        }

        private SerializedEvent SerializeEvent(Event data)
        {
            return new SerializedEvent
            {
                Id = _globalPrefix.CreateInternal(data.Id),
                UtcTimestamp = data.UtcTimestamp.ToUnixTimeSeconds(),
                Type = data.EventType,
                Service = data.ServiceIdentifier,
                Project = data.Project != null ? _globalPrefix.CreateInternal(data.Project) : null,
                Session = data.Session != null ? _globalPrefix.CreateInternal(data.Session) : null,
                Request = data.Request,
                Key = data.Key != null ? _globalPrefix.CreateInternal(data.Key) : null,
                Entity = data.Entity,
                Userdata = data.Userdata
            };
        }

        private static JObject? FormatUserdata(object userdata)
        {
            if (userdata == null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<JObject>(
                JsonConvert.SerializeObject(userdata));
        }

        private static JObject? FormatEntity(object entity)
        {
            if (entity == null)
            {
                return null;
            }

            if (entity is JObject o)
            {
                return o;
            }

            if (entity is Model m)
            {
                // TODO
                return null;
            }

            return null;
        }

        private static JObject? FormatRequest(HttpRequest request)
        {
            if (request == null)
            {
                return null;
            }

            var headers = new JObject();
            foreach (var kv in request.Headers)
            {
                var headerArray = new JArray();
                foreach (var v in kv.Value)
                {
                    headerArray.Add(v);
                }

                headers.Add(kv.Key, headerArray);
            }

            return JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(new
            {
                headers,
                url = request.GetEncodedUrl(),
                method = request.Method
            }));
        }
    }
}
