using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using backend.Data;
using backend.Services;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------
// LOGGING
// ---------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Error);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Error);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);
builder.Logging.SetMinimumLevel(LogLevel.Information);

// ---------------------
// CORS
// ---------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowFrontend",
        policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:5173",
                    "http://127.0.0.1:5000",
                    "http://10.0.2.2:5000",
                    "http://192.168.0.109:5000",
                    "https://forestbarber.site",
                    "http://forestbarber.site"
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
    );
});

// ---------------------
// CONTROLLERS + JSON
// ---------------------
builder
    .Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();

// ---------------------
// SWAGGER
// ---------------------
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Coloca tu token JWT: Bearer <token>",
        }
    );

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                },
                new string[] { }
            },
        }
    );

    // Comentado porque en Varios proyectos rompe el inicio si no existe el XML
    // var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    // var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    // if (File.Exists(xmlPath))
    //     options.IncludeXmlComments(xmlPath);
});

// ---------------------
// DATABASE
// ---------------------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MariaDbServerVersion(new Version(10, 4, 32))
    )
);

// ---------------------
// JWT AUTHENTICATION
// ---------------------
builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey =
            builder.Configuration["Jwt:Key"]
            ?? throw new ArgumentNullException("Jwt:Key no está definido");

        var key = Encoding.ASCII.GetBytes(jwtKey);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
        };
    });

builder.Services.AddAuthorization();

// ---------------------
// SERVICIOS DE NEGOCIO
// ---------------------
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICierreDiarioService, CierreDiarioService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<ITurnoService, TurnoService>();

builder.Services.AddScoped<IAtencionService>(provider =>
{
    var context = provider.GetRequiredService<ApplicationDbContext>();
    var logger = provider.GetRequiredService<ILogger<AtencionService>>();
    var stockService = provider.GetRequiredService<IStockService>();
    return new AtencionService(context, logger, stockService);
});

builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IVerificacionService, VerificacionService>();

// ⚠️ DESACTIVADO TEMPORALMENTE hasta confirmar que NO usa DbContext directamente
// builder.Services.AddHostedService<CleanupBackgroundService>();

builder.Services.AddSingleton(builder.Configuration);

// ---------------------
// APP
// ---------------------
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ⚠️ DESACTIVADO: Esto redirige a HTTPS y rompe Swagger en 5000
// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run("http://0.0.0.0:5000");

// ---------------------
// RECORD
// ---------------------
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
