using System.Text;
using Veriqo.Api.Data;
using Veriqo.Api.Models;
using Veriqo.Api.Services;
using Veriqo.Api.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ----
builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("Mongo"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AdminSettings>(builder.Configuration.GetSection("Admin"));
builder.Services.Configure<GoogleSettings>(builder.Configuration.GetSection("Google"));
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<RegistrySettings>(builder.Configuration.GetSection("Registry"));
builder.Services.Configure<AiSettings>(builder.Configuration.GetSection("Ai"));
builder.Services.AddSingleton<IEmailService, SmtpEmailService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
var adminSettings = builder.Configuration.GetSection("Admin").Get<AdminSettings>() ?? new AdminSettings();

// ---- Services ----
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<TokenService>();

// ---- ASP.NET Core Identity (MongoDB-backed) ----
builder.Services.AddIdentityCore<ApplicationUser>(o =>
{
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 8;
    o.Password.RequireUppercase = true;
    o.Password.RequireLowercase = false;
    o.Password.RequireDigit = true;
    o.Password.RequireNonAlphanumeric = true;
    o.Lockout.AllowedForNewUsers = true;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
.AddUserStore<MongoUserStore>()
.AddDefaultTokenProviders();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<IOcrEngine, TesseractOcrEngine>();
builder.Services.AddHttpClient<ForensicsClient>();
builder.Services.AddHttpClient<IdentityVerifier>();
builder.Services.AddScoped<DocumentAnalyzer>();

// AI support assistant — multi-agent pipeline (triage/NLP → RAG → draft → actions) on Groq,
// deterministic mock otherwise.
builder.Services.AddScoped<IKnowledgeRetriever, KeywordRetriever>();
builder.Services.AddHttpClient<GroqChatClient>();
builder.Services.AddScoped<MockAiAssistant>();
builder.Services.AddScoped<MultiAgentAssistant>();
builder.Services.AddScoped<IAiAssistant>(sp =>
{
    var cfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiSettings>>().Value;
    return cfg.Configured && cfg.Provider == "groq"
        ? sp.GetRequiredService<MultiAgentAssistant>()
        : sp.GetRequiredService<MockAiAssistant>();
});
builder.Services.AddScoped<ApplicationService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AuditService>();

builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// ---- Auth ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret))
        };
    });
builder.Services.AddAuthorization();

// ---- CORS ----
builder.Services.AddCors(options =>
    options.AddPolicy("frontend", p => p
        .WithOrigins("http://localhost:5173", "http://localhost:5174")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

// ---- Seed database ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await DbSeeder.SeedAsync(db, userManager, adminSettings);
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
