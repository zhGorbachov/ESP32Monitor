using ESP32Monitor.Data;
using ESP32Monitor.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Razor / Blazor ────────────────────────────────────────────────────────────
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// ── MVC controllers (REST API) ────────────────────────────────────────────────
builder.Services.AddControllers();

// ── EF Core + SQLite ──────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=esp32monitor.db"));

// ── HTTP client for ESP32 ─────────────────────────────────────────────────────
builder.Services.AddHttpClient("esp32", client =>
{
    var baseUrl = builder.Configuration["Esp32:BaseUrl"] ?? "http://192.168.1.100";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddSingleton<Esp32Client>();

// Named HttpClient used by Blazor Server pages to call the local REST API
builder.Services.AddHttpClient("self", (sp, client) =>
{
    // Resolved at runtime inside pages via IHttpClientFactory
    client.BaseAddress = new Uri(builder.Configuration["App:BaseUrl"] ?? "http://localhost:5000");
});

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddSingleton<DeviceStateHolder>();
builder.Services.AddHostedService<PollingService>();

// ── Swagger (dev) ─────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Create database on startup ────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Guarantee the table exists even if EnsureCreated skipped it
    // (happens when the .db file already exists but is empty/incomplete)
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "ParameterLogs" (
            "Id"            INTEGER PRIMARY KEY AUTOINCREMENT,
            "Timestamp"     TEXT    NOT NULL,
            "ParameterName" TEXT    NOT NULL,
            "OldValue"      TEXT,
            "NewValue"      TEXT,
            "Source"        TEXT    NOT NULL
        )
    """);
    db.Database.ExecuteSqlRaw("""
        CREATE INDEX IF NOT EXISTS "IX_ParameterLogs_Timestamp"
        ON "ParameterLogs" ("Timestamp")
    """);
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
