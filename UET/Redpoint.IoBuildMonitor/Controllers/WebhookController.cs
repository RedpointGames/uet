namespace Io.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Io.Database;
    using System.Text.Json.Serialization;
    using Newtonsoft.Json.Linq;
    using System.Threading.Tasks;
    using System.Linq;
    using System.IO;
    using System.Text;
    using System.Net.Http;
    using System;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Configuration;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.AspNetCore.SignalR;
    using Io.Readers;
    using Io.Json.GitLab;
    using Io.Mappers;
    using Io.Processors;
    using System.Text.Json;
    using Redpoint.IoBuildMonitor.Mappers;

    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly IoDbContext _db;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            IoDbContext db,
            ILogger<WebhookController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpPost]
        [Route("/webhook/gitlab")]
        public async Task<IActionResult> HandleGitLabWebhook()
        {
            string json;
            using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                json = await reader.ReadToEndAsync();
            }

            _logger.LogInformation("Received webhook message from GitLab");

            var obj = JObject.Parse(json);
            var objectKind = obj["object_kind"]?.Value<string>();
            _logger.LogInformation($"Received object is '{objectKind}'");
            if (objectKind == "pipeline")
            {
                PipelineWebhookJson? ev = null;
                try
                {
                    ev = JsonSerializer.Deserialize(
                        json,
                        IoJsonSerializerContext.Default.PipelineWebhookJson);
                }
                catch
                {
                }

                if (ev?.Project?.Id == null)
                {
                    _logger.LogError($"Ignoring webhook event for pipeline #{ev?.ObjectAttributes?.Id} because it does not have a project ID.");
                    return BadRequest();
                }

                var webhookEvent = new WebhookEventEntity
                {
                    ProjectId = ev.Project.Id,
                    ObjectKind = objectKind,
                    Data = JsonSerializer.Serialize(ev, IoJsonSerializerContext.Default.PipelineWebhookJson),
                    Done = false,
                };
                _db.WebhookEvents.Add(webhookEvent);
                await _db.SaveChangesAsync();
            }
            else if (objectKind == "build")
            {
                BuildWebhookJson? ev = null;
                try
                {
                    ev = JsonSerializer.Deserialize(
                        json,
                        IoJsonSerializerContext.Default.BuildWebhookJson);
                }
                catch
                {
                }

                if (ev?.ProjectId == null)
                {
                    _logger.LogError($"Ignoring webhook event for build #{ev?.Id} because it does not have a project ID.");
                    return BadRequest();
                }

                var webhookEvent = new WebhookEventEntity
                {
                    ProjectId = ev.ProjectId,
                    ObjectKind = objectKind,
                    Data = JsonSerializer.Serialize(ev, IoJsonSerializerContext.Default.BuildWebhookJson),
                    Done = false,
                };
                _db.WebhookEvents.Add(webhookEvent);
                await _db.SaveChangesAsync();
            }

            return Ok();
        }

        private void HandleDeserializationError(object sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
        {
            _logger.LogError($"Failed to deserialize at JSON path '{e.ErrorContext.Path}'", e.ErrorContext.Error.Message);
        }
    }
}
