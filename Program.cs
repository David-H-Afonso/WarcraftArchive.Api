using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Configuration;
using WarcraftArchive.Api.Infrastructure.Persistence;
using WarcraftArchive.Api.Endpoints;
using WarcraftArchive.Api.Common;
using WarcraftArchive.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.LoadEnvironmentFile();
builder.ApplyEnvironmentOverrides();
builder.Services.BindConfigurationSections(builder.Configuration);
builder.Services.AddWarcraftArchiveDatabase(builder.Configuration);
builder.Services.AddWarcraftArchiveAuth(builder.Configuration, builder.Environment);
builder.Services.AddWarcraftArchiveCors(builder.Configuration, builder.Environment);
builder.Services.AddWarcraftArchiveSwagger();
builder.Services.AddWarcraftArchiveServices();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var helper = new DatabaseStartupHelper(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        scope.ServiceProvider.GetRequiredService<ILogger<DatabaseStartupHelper>>());

    await helper.ApplyMigrationsAsync();

    var seedCfg = scope.ServiceProvider.GetRequiredService<IConfiguration>()
        .GetSection(SeedSettings.SectionName).Get<SeedSettings>() ?? new();

    var adminUser = await helper.SeedAdminAsync(seedCfg);

    if (seedCfg.DemoImportEnabled)
    {
        var importUser = adminUser ?? await helper.GetFirstAdminAsync();
        if (importUser != null)
            await CsvImportHelper.ImportAsync(helper.Context, seedCfg.CsvDataPath, importUser,
                scope.ServiceProvider.GetRequiredService<ILogger<DatabaseStartupHelper>>());
        else
            scope.ServiceProvider.GetRequiredService<ILogger<DatabaseStartupHelper>>()
                .LogWarning("CsvImport: no admin user found — skipping CSV import.");
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "WarcraftArchive API v1");
    c.RoutePrefix = "swagger";
});
app.UseMiddleware<ErrorHandlingMiddleware>();
if (app.Environment.IsDevelopment())
    app.UseCors("AllowAll");
else
    app.UseCors("AllowSpecificOrigins");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (AppDbContext db, ILogger<Program> logger) =>
{
    var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    string dbStatus;
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        dbStatus = pending.Count == 0 ? "ok" : $"ok ({pending.Count} pending migration(s))";
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Health check: DB connectivity failed");
        dbStatus = "error";
    }
    var overall = dbStatus.StartsWith("error") ? "degraded" : "healthy";
    var payload = new { status = overall, version, db = dbStatus, timestamp = DateTime.UtcNow };
    return overall == "healthy" ? Results.Ok(payload) : Results.Json(payload, statusCode: 503);
}).AllowAnonymous().WithTags("System");

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapCharacterEndpoints();
app.MapContentEndpoints();
app.MapTrackingEndpoints();
app.MapDashboardEndpoints();
app.MapWarbandEndpoints();
app.MapUserMotiveEndpoints();
app.MapDataEndpoints();
app.MapResetEndpoints();

app.Run();
