using System.Text;
using System.Text.Json.Serialization;
using FinFlow.Api.Data;
using FinFlow.Api.Endpoints;
using FinFlow.Api.Services;
using Microsoft.EntityFrameworkCore;

// The bank CSV parsers fall back to Windows-1252 for older exports.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=finflow.db"));
builder.Services.AddScoped<ClassificationService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

// SPA fallback: client-side routes like /dashboard resolve to index.html.
if (File.Exists(Path.Combine(app.Environment.WebRootPath ?? "", "index.html")))
    app.MapFallbackToFile("index.html");

app.Run();
