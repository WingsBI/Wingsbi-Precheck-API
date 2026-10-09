using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Dapper;
using Precheck.Agent;
using Precheck.Repository.Database;
using Precheck.Repository.Repository.ArchiveRepository;
using Precheck.Repository.Repository.CommonRepository;
using Precheck.Repository.Repository.DrawingNumberRepository;
using Precheck.Repository.Repository.IdentifierRepository;
using Precheck.Repository.Repository.MaterialRequisitionRepository;
using Precheck.Repository.Repository.PrecheckRepository;
using Precheck.Repository.Repository.QRCodeRepository;
using Precheck.Repository.Repository.SopRepository;
using Precheck.Repository.Repository.TestingRepository;
using Precheck.Repository.Repository.UserRepository;
using Precheck.Service.Cache;
using Precheck.Service.Helper;
using Precheck.Service.MapperSetup;
using Precheck.Service.Service.ArchiveService;
using Precheck.Service.Service.AuthService;
using Precheck.Service.Service.CommonService;
using Precheck.Service.Service.DrawingNumberService;
using Precheck.Service.Service.IdentifierService;
using Precheck.Service.Service.MaterialRequisitionService;
using Precheck.Service.Service.PrecheckService;
using Precheck.Service.Service.QRCodeService;
using Precheck.Service.Service.SopService;
using Precheck.Service.Service.TestingService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.WebJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using DinkToPdf;
using DinkToPdf.Contracts;
using Serilog;
using Serilog.Events;
using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
ConfigurationManager configuration = builder.Configuration;

// Initialize Mapster mappings
MappingSetup.Init();

// Determine environment
var isAzure = Environment.GetEnvironmentVariable("HOME") != null;

// Get log base directory
var logBaseDirectory = isAzure
    ? Path.Combine(Environment.GetEnvironmentVariable("HOME")!, "LogFiles", "Application")
    : configuration.GetValue<string>("Logging:LogDirectory") ?? "Logs";

// Read logging enabled flag
var enableLogging = configuration.GetValue<bool>("Logging:EnableLogging");

if (enableLogging)
{
    // Create timestamped folder structure
    var dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
    var hourFolder = DateTime.Now.ToString("HH");
    var fullLogPath = Path.Combine(logBaseDirectory, dateFolder, hourFolder);
    Directory.CreateDirectory(fullLogPath);

    // Configure Serilog
    Log.Logger = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Error || e.Level == LogEventLevel.Fatal)
            .WriteTo.File(
                Path.Combine(fullLogPath, "ERROR.txt"),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Infinite,
                retainedFileCountLimit: null
            )
        )
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(e => e.Level <= LogEventLevel.Information)
            .WriteTo.File(
                Path.Combine(fullLogPath, "INFO.txt"),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Infinite,
                retainedFileCountLimit: null
            )
        )
        .CreateLogger();

    builder.Host.UseSerilog();
}

// CORS policy
var MyAllowSpecificOrigins = "AllowAll";
DefaultTypeMap.MatchNamesWithUnderscores = true;

var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Rate limiting - throttles brute-force attempts on auth endpoints
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static string ClientKey(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.AddPolicy("auth-login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    options.AddPolicy("auth-sensitive", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 3, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.User.FindFirst("id")?.Value ?? ClientKey(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 200, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Honour X-Forwarded-For from App Service / reverse proxy so rate limiting sees the real client IP
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IApplicationDbContext, ApplicationDbContext>();

// Backup Database context
builder.Services.AddDbContext<BackupDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("BackupConnection")));
builder.Services.AddScoped<IBackupDbContext, BackupDbContext>();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// Register Repositories
builder.Services.AddScoped<ICommonRepository, CommonRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IQRCodeRepository, QRCodeRepository>();
builder.Services.AddScoped<IIdentifierRepository, IdentifierRepository>();
builder.Services.AddScoped<IPrecheckRepository, PrecheckRepository>();
builder.Services.AddScoped<ISopRepository, SopRepository>();
builder.Services.AddScoped<IDrawingNumberRepository, DrawingNumberRepository>();
builder.Services.AddScoped<IMaterialRequisitionRepository, MaterialRequisitionRepository>();
builder.Services.AddScoped<ITestingRepository, TestingRepository>();
// Register Backup Archive Repository (only archive service needed)
builder.Services.AddScoped<IBackupArchiveRepository, BackupArchiveRepository>();

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICommonService, CommonService>();
builder.Services.AddScoped<IQRCodeService, QRCodeService>();
builder.Services.AddScoped<IIdentifierService, IdentifierService>();
builder.Services.AddScoped<IPrecheckService, PrecheckService>();
builder.Services.AddScoped<ISopService, SopService>();
builder.Services.AddScoped<IDrawingNumberService, DrawingNumberService>();
builder.Services.AddScoped<IMaterialRequisitionService, MaterialRequisitionService>();
builder.Services.AddScoped<ITestingService, TestingService>();
builder.Services.AddScoped<IHelperService, HelperService>();
builder.Services.AddScoped<Precheck.Repository.Repository.AnalyticsRepository.IAnalyticsRepository, Precheck.Repository.Repository.AnalyticsRepository.AnalyticsRepository>();
builder.Services.AddScoped<Precheck.Service.Service.AnalyticsService.IAnalyticsService, Precheck.Service.Service.AnalyticsService.AnalyticsService>();
// Register Backup Archive Service (only archive service needed)
builder.Services.AddScoped<IBackupArchiveService, BackupArchiveService>();
// Production Order Import
builder.Services.AddScoped<Precheck.Repository.Repository.ProductionOrderRepository.IProductionOrderRepository, Precheck.Repository.Repository.ProductionOrderRepository.ProductionOrderRepository>();
builder.Services.AddScoped<Precheck.Service.Service.ProductionOrderService.IProductionOrderService, Precheck.Service.Service.ProductionOrderService.ProductionOrderService>();
// Chatbot Import
builder.Services.AddScoped<Precheck.Repository.Repository.ChatbotRepository.IChatbotRepository, Precheck.Repository.Repository.ChatbotRepository.ChatbotRepository>();
builder.Services.AddScoped<Precheck.Service.Service.ChatbotService.IChatbotService, Precheck.Service.Service.ChatbotService.ChatbotService>();
builder.Services.AddScoped<Precheck.Host.Agents.ChatbotTools>();
builder.Services.AddScoped<Precheck.Host.Agents.ChatbotAgentFactory>();

// AI agent history, turn saving and follow-up questions - see the Precheck.Agent project
builder.Services.AddCopilotAgent<Precheck.Host.Agents.SuggestedQuestionsProvider>();

// CopilotKit / AG-UI: MapAGUIServer needs one agent instance at startup, so it gets a RequestScopedAgent
// that builds the real (per-user) chatbot agent from the current request on every call.
builder.Services.AddAGUIServer();

// Cache
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICacheService, CacheService>();

// DinkToPdf - Singleton because wkhtmltopdf native library is not thread-safe
builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));

// Swagger & API
// MaxDepth raised from the default (32) because GetSop's response nests one GetSopResponseDto.Children
// list per BOM level, and a real, deeply-nested assembly can legitimately exceed 32 levels - System.Text.Json
// was throwing "A possible object cycle was detected" partway through serialization once it did, which sent
// a 200 status (headers already flushed) but then cut the response body off mid-stream, making the call
// look like it hung rather than surfacing a clear error.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.MaxDepth = 256;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v9.5", new OpenApiInfo
    {
        Title = "Precheck API",
        Version = "v9.5"
    });
    
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (enableLogging)
{
    app.UseSerilogRequestLogging();
}


app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    if (allowedOrigins.Length == 0)
    {
        app.Logger.LogWarning("Cors:AllowedOrigins is empty - all cross-origin browser requests will be blocked.");
    }
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors(MyAllowSpecificOrigins);

app.UseRateLimiter();

if (app.Environment.IsDevelopment() || configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v9.5/swagger.json", "Precheck API v9.5");
        c.DisplayRequestDuration();
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapAGUIServer("/api/aiAssistant", new Precheck.Host.Agents.RequestScopedAgent(
    app.Services.GetRequiredService<IHttpContextAccessor>(), "precheck-chatbot")).RequireAuthorization();

app.Run();
