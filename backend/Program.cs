using Amazon.Bedrock;
using Amazon.BedrockRuntime;
using Amazon.Polly;
using Amazon.S3;
using Chatbot.Api.Data;
using Chatbot.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- AWS clients ----
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());

// Bedrock can run in a different region than S3/Polly (image models vary by region).
var bedrockOptions = builder.Configuration.GetAWSOptions();
var bedrockRegion = builder.Configuration["Bedrock:Region"];
if (!string.IsNullOrWhiteSpace(bedrockRegion))
    bedrockOptions.Region = Amazon.RegionEndpoint.GetBySystemName(bedrockRegion);

builder.Services.AddAWSService<IAmazonBedrock>(bedrockOptions);
builder.Services.AddAWSService<IAmazonBedrockRuntime>(bedrockOptions);
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonPolly>();

// ---- App services ----
builder.Services.AddScoped<IBedrockService, BedrockService>();
builder.Services.AddScoped<IStorageService, S3StorageService>();
builder.Services.AddScoped<ISpeechService, PollySpeechService>();
builder.Services.AddScoped<IDocumentParser, OpenXmlDocumentParser>();

// ---- Data ----
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// ---- Auth (Cognito JWT). Disabled in Development via DisableAuth flag. ----
var disableAuth = builder.Configuration.GetValue<bool>("DisableAuth");
if (!disableAuth)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Cognito:Authority"];
            options.Audience = builder.Configuration["Cognito:Audience"];
            options.TokenValidationParameters.ValidateAudience = false;
        });
}
builder.Services.AddAuthorization();

// ---- Web ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Apply pending migrations at startup (skip if the DB is unreachable in local dev).
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database migration skipped (database may be unavailable).");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

if (!disableAuth)
{
    app.UseAuthentication();
}

// When auth is disabled for local dev, allow [Authorize] endpoints through.
app.Use(async (ctx, next) =>
{
    if (disableAuth && ctx.User.Identity is { IsAuthenticated: false })
    {
        var identity = new System.Security.Claims.ClaimsIdentity("dev");
        identity.AddClaim(new System.Security.Claims.Claim("sub", "dev-user"));
        ctx.User = new System.Security.Claims.ClaimsPrincipal(identity);
    }
    await next();
});

app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
