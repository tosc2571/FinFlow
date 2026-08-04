using System.Text;
using System.Text.Json.Serialization;
using FinFlow.Api.Data;
using FinFlow.Api.Endpoints;
using FinFlow.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

// The bank CSV parsers fall back to Windows-1252 for older exports.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Development keeps the database next to the project (easy to inspect/delete while coding).
// Everywhere else (the published app users actually run) it lives in a stable, OS-appropriate
// data directory, independent of the working directory or which folder a release was unpacked
// into — see AppDataDirectory / issue #4. ConnectionStrings:Default still wins if set (e.g. a
// future Docker bind mount), which is also how scripts/smoke-test.sh keeps its runs isolated
// from a real local install.
string defaultDbPath = builder.Environment.IsDevelopment()
    ? "finflow.db"
    : Path.Combine(AppDataDirectory.Resolve(), "finflow.db");
string connectionString = builder.Configuration.GetConnectionString("Default") ?? $"Data Source={defaultDbPath}";
string resolvedDbPath = new SqliteConnectionStringBuilder(connectionString).DataSource;

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<ClassificationService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<TransferDetectionService>();
builder.Services.AddScoped(_ => new NotesService(resolvedDbPath));
builder.Services.AddHostedService(sp =>
    new PeriodicBackupService(resolvedDbPath, sp.GetRequiredService<ILogger<PeriodicBackupService>>()));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.Logger.LogInformation("Database file: {DbPath}", Path.GetFullPath(resolvedDbPath));
AppSettings settings = AppSettingsStore.Load(resolvedDbPath);
DatabaseBackup.BackupIfNeeded(resolvedDbPath, app.Logger, enabled: settings.AutoBackupEnabled);

using (IServiceScope scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve the built Angular app when it's present (scripts/publish.* copies it into
// wwwroot). In development this is a no-op — the Angular dev server + proxy is used.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapImportEndpoints();
app.MapTransactionEndpoints();
app.MapCategoryEndpoints();
app.MapRuleEndpoints();
app.MapDashboardEndpoints();
app.MapExportEndpoints();
app.MapContractEndpoints();
app.MapSettingsEndpoints(resolvedDbPath);
app.MapBankAccountEndpoints();
app.MapNoteEndpoints();

// SPA fallback: client-side routes like /dashboard resolve to index.html.
if (File.Exists(Path.Combine(app.Environment.WebRootPath ?? "", "index.html")))
    app.MapFallbackToFile("index.html");

app.Run();
