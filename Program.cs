using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using backend.Data;
using backend.Services;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine(">>> JWT KEY USADA: " + builder.Configuration["Jwt:Key"]);
Console.WriteLine(">>> JWT KEY LENGTH: " + (builder.Configuration["Jwt:Key"]?.Length ?? 0));

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
                    "http://192.168.0.108:5000",
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
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            string DisplayField(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    return "campo";
                var last = raw.Split('.').Last();
                return System.Text.RegularExpressions.Regex.Replace(last, @"\[\d+\]", "");
            }

            string Translate(string field, string original)
            {
                if (string.IsNullOrWhiteSpace(original))
                    return $"Error de validación en el campo {field}.";

                var o = original.ToLowerInvariant();

                if (
                    o.Contains("required")
                    || o.Contains("es obligatorio")
                    || o.Contains("no puede estar vacío")
                )
                    return $"El campo {field} es obligatorio.";

                var m = System.Text.RegularExpressions.Regex.Match(
                    original,
                    @"minimum length of '(\d+)'",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                if (m.Success)
                {
                    var n = m.Groups[1].Value;
                    return $"El campo {field} debe tener al menos {n} caracteres.";
                }

                if (o.Contains("minimum length") || o.Contains("mínimo") || o.Contains("minimum"))
                {
                    var mm = System.Text.RegularExpressions.Regex.Match(original, "'(\\d+)'");
                    if (mm.Success)
                        return $"El campo {field} debe tener al menos {mm.Groups[1].Value} caracteres.";
                    return $"El campo {field} tiene un tamaño inválido.";
                }

                if (
                    o.Contains("invalid")
                    || o.Contains("not valid")
                    || o.Contains("no válido")
                    || o.Contains("is not a valid")
                )
                    return $"El campo {field} tiene un valor inválido.";

                return $"Error de validación en el campo {field}.";
            }

            // Aseguramos que ModelState NO sea null
            var modelState =
                context.ModelState
                ?? new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();

            // --- FIX Warnings CS8602 ---
            var errors = modelState
                .Where(kvp => kvp.Value?.Errors?.Count > 0)
                .SelectMany(kvp =>
                    kvp.Value!.Errors.Select(err => new
                    {
                        Field = DisplayField(kvp.Key),
                        Message = Translate(
                            DisplayField(kvp.Key),
                            err.ErrorMessage ?? string.Empty
                        ),
                    })
                )
                .ToList();
            // ---------------------------

            var pd = new ValidationProblemDetails(modelState)
            {
                Title = "Errores de validación.",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Revisá los campos e intentá nuevamente.",
            };

            pd.Errors.Clear();
            foreach (var e in errors)
            {
                if (pd.Errors.ContainsKey(e.Field))
                {
                    var list = pd.Errors[e.Field].ToList();
                    list.Add(e.Message);
                    pd.Errors[e.Field] = list.ToArray();
                }
                else
                {
                    pd.Errors.Add(e.Field, new[] { e.Message });
                }
            }

            return new BadRequestObjectResult(pd) { ContentTypes = { "application/problem+json" } };
        };
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

Console.WriteLine(">>> JWT KEY USADA: " + builder.Configuration["Jwt:Key"]);
Console.WriteLine(">>> JWT KEY LENGTH: " + (builder.Configuration["Jwt:Key"]?.Length ?? 0));

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
builder.Services.AddScoped<IClienteDashboardService, ClienteDashboardService>();
builder.Services.AddScoped<IBarberoDashboardService, BarberoDashboardService>();

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
builder.Services.AddScoped<ITurnoStateService, TurnoStateService>();

builder.Services.AddSingleton(builder.Configuration);

// ---------------------
// APP
// ---------------------
Console.WriteLine("=== CONFIG LOADED ===");
foreach (var kvp in builder.Configuration.AsEnumerable())
{
    Console.WriteLine($"{kvp.Key} = {kvp.Value}");
}
Console.WriteLine("======================");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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
