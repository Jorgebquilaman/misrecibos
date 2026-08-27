using System.Security.Claims;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PortalIUPA.Api.Middlewares;
using PortalIUPA.Api.Seed;
using PortalIUPA.Application;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.UseCases.Auth;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Infrastructure;
using PortalIUPA.Infrastructure.Auth;
using PortalIUPA.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

var frontendBaseUrl = builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Portal del Empleado IUPA", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pegue el token JWT."
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var frontendOrigins = builder.Configuration.GetSection("Frontend:Origins").Get<string[]>();
var allowedOrigins = frontendOrigins is { Length: > 0 }
    ? frontendOrigins
    : new[] { frontendBaseUrl };

builder.Services.AddCors(o => o.AddPolicy("Frontend", p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Falta la sección de configuración 'Jwt'.");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddCookie(o => o.ExpireTimeSpan = TimeSpan.FromMinutes(15))
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Emisor,
            ValidateAudience = true,
            ValidAudience = jwt.Audiencia,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = "rol",
            NameClaimType = ClaimTypes.Name
        };
    });

var googleClientId = builder.Configuration["Google:ClientId"];
var googleClientSecret = builder.Configuration["Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
    .AddGoogle(o =>
    {
        o.ClientId = googleClientId;
        o.ClientSecret = googleClientSecret;
        o.CallbackPath = "/api/auth/google/callback";
        o.Events = new OAuthEvents
        {
            OnCreatingTicket = context =>
            {
                var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
                var hd = context.Principal?.FindFirstValue("hd");
                if (email is null ||
                    (!email.EndsWith("@iupa.edu.ar", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(hd, "iupa.edu.ar", StringComparison.OrdinalIgnoreCase)))
                    context.Fail("El dominio del correo no está habilitado para el portal.");
                return Task.CompletedTask;
            },
            OnTicketReceived = async context =>
            {
                var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
                if (email is null)
                {
                    context.Fail("El correo no fue provisto por Google.");
                    return;
                }

                var services = context.HttpContext.RequestServices;
                var mediator = services.GetRequiredService<IMediator>();
                var logger = services.GetRequiredService<ILogger<Program>>();
                var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString();
                var dispositivo = context.HttpContext.Request.Headers.UserAgent.ToString();

                try
                {
                    var sesion = await mediator.Send(new AutenticarEmpleadoCommand(email, ip, dispositivo));
                    context.Response.Redirect($"{frontendBaseUrl}/auth/callback#token={sesion.Token}");
                }
                catch (AccesoDenegadoException ex)
                {
                    logger.LogWarning("Login denegado para {Correo}: {Mensaje}", email, ex.Message);
                    context.Response.Redirect($"{frontendBaseUrl}/login?error={Uri.EscapeDataString(ex.Message)}");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error autenticando a {Correo}", email);
                    context.Response.Redirect($"{frontendBaseUrl}/login?error=error_interno");
                }

                context.HandleResponse();
            },
            OnRemoteFailure = context =>
            {
                context.Response.Redirect($"{frontendBaseUrl}/login?error=dominio_no_permitido");
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
    });
}

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Responsable", p => p.RequireRole(Rol.Responsable.ToString(), Rol.Rrhh.ToString(),
        Rol.Administrador.ToString(), Rol.Direccion.ToString()));
    o.AddPolicy("Rrhh", p => p.RequireRole(Rol.Rrhh.ToString(), Rol.Administrador.ToString()));
    o.AddPolicy("Administrador", p => p.RequireRole(Rol.Administrador.ToString()));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownProxies.Add(System.Net.IPAddress.Parse("172.16.0.10"));
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseMiddleware<GlobalExceptionMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
        if (app.Environment.IsDevelopment())
            await SeedData.EjecutarAsync(db, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "No se pudo aplicar la base de datos.");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    if (context.Request.Headers.ContainsKey("Access-Control-Request-Private-Network"))
        context.Response.Headers.Append("Access-Control-Allow-Private-Network", "true");
    await next();
});
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/healthz", () => Results.Ok(new { estado = "ok" }));

app.Run();

public partial class Program;