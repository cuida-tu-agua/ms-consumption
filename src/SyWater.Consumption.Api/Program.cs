using Microsoft.AspNetCore.Authorization;
using SyWater.Consumption.Api.Composition;
using SyWater.Consumption.Api.Errors;
using SyWater.Consumption.Api.Security;
using SyWater.Consumption.Infrastructure.Persistence;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ── Inbound adapters: HTTP and RabbitMQ ──────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
// The OpenAPI document says the API lives under the gateway prefix (e.g. "/places"),
// so "Try it out" in Swagger UI calls https://api.<domain>/places/...
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, context, cancellationToken) =>
{
    var serverUrl = builder.Configuration["OpenApi:ServerUrl"];
    if (!string.IsNullOrWhiteSpace(serverUrl))
        document.Servers = [new OpenApiServer { Url = serverUrl }];
    return Task.CompletedTask;
}));

// ── Hexagon + adapters ──────────────────────────────────────────────────
builder.Services.AddConsumptionApplication();
builder.Services.AddConsumptionInfrastructure(builder.Configuration);
builder.Services.AddConsumptionMessaging(builder.Configuration);

// ── Security: every endpoint requires a valid ms-iam token unless marked AllowAnonymous ──
builder.Services.AddIamJwtAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddHealthChecks().AddDbContextCheck<ConsumptionDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();

// Development: always. Other environments: only when OpenApi:Enabled=true (set by infra).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi().AllowAnonymous(); // GET /openapi/v1.json
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

/// <summary>Lets the integration tests start the app (WebApplicationFactory&lt;Program&gt;).</summary>
public partial class Program;
