namespace Io.Controllers
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.IdentityModel.Tokens;
    using System;
    using System.IdentityModel.Tokens.Jwt;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Io.Json.Api;
    using Io.Database;
    using Io.Mappers;
    using Io.Database.Entities;
    using Microsoft.EntityFrameworkCore;
    using Io.Redis;
    using System.Globalization;
    using System.Diagnostics.CodeAnalysis;

    [ApiController]
    public class ApiController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiController> _logger;
        private readonly IoDbContext _db;
        private readonly IMapper<TestJsonWithValidatedContext, TestEntity[]> _testMapper;
        private readonly INotificationHub _notificationHub;
        private JsonWebKeySet? _jwks;

        public ApiController(
            IConfiguration configuration,
            ILogger<ApiController> logger,
            IoDbContext db,
            IMapper<TestJsonWithValidatedContext, TestEntity[]> testMapper,
            INotificationHub notificationHub)
        {
            _configuration = configuration;
            _logger = logger;
            _db = db;
            _testMapper = testMapper;
            _notificationHub = notificationHub;
        }

        [SuppressMessage("Security", "CA5404:Do not disable token validation checks", Justification = "v1 JWTs from GitLab do not support audience.")]
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            if (Request.Headers.Authorization.Count == 0)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var authorizationHeader = Request.Headers.Authorization[0];
            if (authorizationHeader == null ||
                !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new ForbidResult();
                return;
            }

            var token = authorizationHeader.Substring("Bearer ".Length);

            if (_jwks == null)
            {
                using (var client = new HttpClient())
                {
                    _jwks = new JsonWebKeySet(await client.GetStringAsync(new Uri(_configuration["GitLab:JwksUrl"]!)));
                }
            }

            // Validate bearer for CI_JOB_JWT_V1.
            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var validationResult = await tokenHandler.ValidateTokenAsync(token, new TokenValidationParameters
                {
                    IssuerSigningKeys = _jwks.GetSigningKeys(),
                    ValidateAudience = false,
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["GitLab:BoundIssuer"],
                });
                if (!validationResult.IsValid)
                {
                    _logger.LogError($"Failed to validate incoming request: Provided token was invalid.");
                    context.Result = new ForbidResult();
                    return;
                }

                Request.HttpContext.Items["namespace_id"] = validationResult.Claims["namespace_id"];
                Request.HttpContext.Items["project_id"] = validationResult.Claims["project_id"];
                Request.HttpContext.Items["user_id"] = validationResult.Claims["user_id"];
                Request.HttpContext.Items["pipeline_id"] = validationResult.Claims["pipeline_id"];
                Request.HttpContext.Items["job_id"] = validationResult.Claims["job_id"];

                await next();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to validate incoming request: {ex.Message}");
                context.Result = new ForbidResult();
                return;
            }
        }

        [Route("api/registered")]
        [HttpGet]
        public async Task<IActionResult> IsRegistered()
        {
            long buildId = long.Parse((string)Request.HttpContext.Items["job_id"]!, CultureInfo.InvariantCulture);
            long pipelineId = long.Parse((string)Request.HttpContext.Items["pipeline_id"]!, CultureInfo.InvariantCulture);
            long projectId = long.Parse((string)Request.HttpContext.Items["project_id"]!, CultureInfo.InvariantCulture);

            if (await _db.Projects.AnyAsync(x => x.Id == projectId))
            {
                return Json(new
                {
                    status = "ok",
                });
            }

            return NotFound();
        }

        [Route("api/submit/tests")]
        [HttpPut]
        public async Task<IActionResult> SubmitTests([FromBody] TestJson[] testData)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            long buildId = long.Parse((string)Request.HttpContext.Items["job_id"]!, CultureInfo.InvariantCulture);
            long pipelineId = long.Parse((string)Request.HttpContext.Items["pipeline_id"]!, CultureInfo.InvariantCulture);
            long projectId = long.Parse((string)Request.HttpContext.Items["project_id"]!, CultureInfo.InvariantCulture);
            long namespaceId = long.Parse((string)Request.HttpContext.Items["namespace_id"]!, CultureInfo.InvariantCulture);

            var entities = await _testMapper.Map(new TestJsonWithValidatedContext
            {
                Tests = testData,
                BuildId = buildId,
                PipelineId = pipelineId,
                ProjectId = projectId,
                NamespaceId = namespaceId,
            }, new MapperContext());

            if (entities == null)
            {
                return BadRequest();
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation($"Ingested {entities.Length} tests for build #{buildId}.");

            await _notificationHub.NotifyAsync(NotificationType.DashboardUpdated);
            // TODO: Do we ever need to do this? I don't think so as we shouldn't be receiving
            // test results after the build is finished anyway.
            // await _notificationHub.NotifyAsync(NotificationType.HistoryUpdated);

            return Json(new
            {
                status = "ok",
                testsWritten = entities.Length,
            });
        }
    }
}
