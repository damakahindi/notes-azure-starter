using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.Identity.Web;
using Notes.Api.Notes;

var builder = WebApplication.CreateBuilder(args);

// Logging: code uses ILogger<T>; the destination is decided here, once.
// With a connection string (set by Bicep) logs, traces and metrics go to Application Insights.
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

// Authentication: sign-in is delegated to Microsoft Entra ID (SSO). We never store passwords.
var authEnabled = builder.Configuration.GetValue<bool>("Auth:Enabled");
if (authEnabled)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
    builder.Services.AddAuthorization();
}

// Data: Cosmos DB (NoSQL) through the app's managed identity (no keys). Falls back to memory locally.
var cosmosEndpoint = builder.Configuration["Cosmos:Endpoint"];
if (string.IsNullOrWhiteSpace(cosmosEndpoint))
{
    builder.Services.AddSingleton<INotesRepository, InMemoryNotesRepository>();
}
else
{
    builder.Services.AddSingleton<INotesRepository>(_ =>
    {
        var clientId = builder.Configuration["AZURE_CLIENT_ID"];
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = clientId });
        var client = new CosmosClient(cosmosEndpoint, credential, new CosmosClientOptions
        {
            UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }
        });
        var container = client.GetContainer(
            builder.Configuration["Cosmos:Database"] ?? "notes",
            builder.Configuration["Cosmos:Container"] ?? "notes");
        return new CosmosNotesRepository(container);
    });
}

// The browser app is served from another origin, so allow only the configured UI origins.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (allowedOrigins.Length > 0)
    {
        p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
}));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors();
if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

// Health endpoint for probes and deployment slot warm-up.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapNotes(requireAuth: authEnabled);

app.Run();

public partial class Program;
