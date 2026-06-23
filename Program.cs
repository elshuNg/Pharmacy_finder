using System.Text;
using System.Text.Json.Serialization;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PharmacyFinder.API.Data;
using PharmacyFinder.API.Helpers;
using PharmacyFinder.API.Middleware;
using PharmacyFinder.API.Services;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("Cloudinary"));
builder.Services.Configure<BootstrapAdminSettings>(builder.Configuration.GetSection("BootstrapAdmin"));
builder.Services.Configure<PrescriptionSettings>(builder.Configuration.GetSection("Prescription"));
builder.Services.Configure<TesseractSettings>(builder.Configuration.GetSection("Tesseract"));

var jwtSection = builder.Configuration.GetSection("Jwt");
var secret = jwtSection["Secret"] ?? throw new InvalidOperationException("JWT Secret is missing.");
if (secret.Length < 32)
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, NpgsqlEnumConfiguration.ConfigureEnums));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await ApiErrorWriter.WriteAsync(
                    context.HttpContext,
                    System.Net.HttpStatusCode.Unauthorized,
                    ApiErrorCodes.Unauthorized,
                    "Authentication is required.");
            },
            OnAuthenticationFailed = async context =>
            {
                context.NoResult();
                await ApiErrorWriter.WriteAsync(
                    context.HttpContext,
                    System.Net.HttpStatusCode.Unauthorized,
                    ApiErrorCodes.AuthInvalidToken,
                    "Invalid or expired authentication token.");
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationResultHandler>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IJwtHelper, JwtHelper>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPharmacyService, PharmacyService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IMedicineService, MedicineService>();
builder.Services.AddScoped<ISearchService, SearchService>();

var storageProvider = builder.Configuration["Storage:Provider"] ?? "Local";
if (storageProvider.Equals("Cloudinary", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<IFileStorageService, CloudinaryFileStorageService>();
else
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

builder.Services.AddScoped<IOcrService, TesseractOcrService>();
builder.Services.AddScoped<IPrescriptionTextParser, PrescriptionTextParser>();
builder.Services.AddScoped<IPrescriptionService, PrescriptionService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddHostedService<PrescriptionCleanupService>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
            ApiErrorWriter.ValidationProblem(context.ModelState);
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "PharmacyFinder API", Version = "v1" });

    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste JWT only (no 'Bearer ' prefix needed in newer Swagger UI)."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("Cors"));

var corsSettings = builder.Configuration.GetSection("Cors").Get<CorsSettings>() ?? new CorsSettings();
var allowAll = corsSettings.AllowAll || builder.Environment.IsDevelopment();
var allowedOrigins = corsSettings.AllowedOrigins
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (allowedOrigins.Length == 0)
    allowedOrigins = ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowAll)
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

var tesseractSettings = app.Configuration.GetSection("Tesseract").Get<TesseractSettings>() ?? new TesseractSettings();
var tessDataDir = Path.IsPathRooted(tesseractSettings.DataPath)
    ? tesseractSettings.DataPath
    : Path.Combine(app.Environment.ContentRootPath, tesseractSettings.DataPath);
var trainedDataFile = Path.Combine(tessDataDir, $"{tesseractSettings.Language}.traineddata");
var x64Leptonica = Path.Combine(app.Environment.ContentRootPath, "x64", "libleptonica-1.82.0.so");
app.Logger.LogInformation(
    "OCR readiness: tessdata={TessDataReady}, x64_native={X64Ready}",
    File.Exists(trainedDataFile),
    File.Exists(x64Leptonica));

await DbInitializer.InitializeAsync(app.Services, app.Logger);

if (!storageProvider.Equals("Cloudinary", StringComparison.OrdinalIgnoreCase))
{
    var storageSettings = app.Configuration.GetSection("Storage").Get<StorageSettings>() ?? new StorageSettings();
    var prescriptionUploadDir = Path.Combine(app.Environment.ContentRootPath, storageSettings.PrescriptionUploadPath);
    Directory.CreateDirectory(prescriptionUploadDir);

    var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "uploads");
    Directory.CreateDirectory(uploadsRoot);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsRoot),
        RequestPath = "/uploads"
    });
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.UseMiddleware<ErrorHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapControllers();
app.Run();
