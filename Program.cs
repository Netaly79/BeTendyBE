using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using FluentValidation;
using FluentValidation.AspNetCore;

using BeTendyBE.Helpers.Validation;
using BeTendyBE.Helpers;
using BeTendlyBE.Services;
using BeTendyBE.Data;
using BeTendyBE.Domain;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

//configurations
var configuration = builder.Configuration;

string? envConn1 = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
string? envConn2 = Environment.GetEnvironmentVariable("POSTGRESQLCONNSTR_DefaultConnection"); // <-- для Type=PostgreSQL
string? envConn3 = Environment.GetEnvironmentVariable("CUSTOMCONNSTR_DefaultConnection");     // если вдруг поменяешь Type на Custom
string? envUrl   = Environment.GetEnvironmentVariable("DATABASE_URL");               // формат postgres://user:pass@host:port/db


string BuildFromDatabaseUrl(string url)
{
  var uri = new Uri(url);
  var userInfo = uri.UserInfo.Split(':', 2);
  var builder = new NpgsqlConnectionStringBuilder
  {
    Host = uri.Host,
    Port = uri.Port > 0 ? uri.Port : 5432,
    Username = Uri.UnescapeDataString(userInfo[0]),
    Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
    Database = uri.AbsolutePath.TrimStart('/'),
    SslMode = SslMode.Require
  };
  return builder.ToString();
}

string connectionString =
    FirstNonEmpty(envConn1, envConn2, envConn3)
    ?? (!string.IsNullOrWhiteSpace(envUrl) ? BuildFromDatabaseUrl(envUrl) : null)
    ?? configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is missing.");

var csb = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
if (!string.IsNullOrEmpty(csb.Host) &&
    csb.Host.EndsWith(".postgres.database.azure.com", StringComparison.OrdinalIgnoreCase))
{
    csb.SslMode = Npgsql.SslMode.Require; // TrustServerCertificate=true — только если реально нужна
}
connectionString = csb.ToString();
builder.Services.AddDbContext<AppDbContext>(opt => opt
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention());

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
var jwtOpts = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
      o.TokenValidationParameters = new TokenValidationParameters
      {
        ValidateIssuer = true,
        ValidIssuer = jwtOpts.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOpts.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.Key)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
      };

      o.Events = new JwtBearerEvents
      {
        OnAuthenticationFailed = ctx =>
        {
          Console.WriteLine($"[JWT] Auth failed: {ctx.Exception.Message}");
          return Task.CompletedTask;
        },
        OnChallenge = ctx =>
        {
          Console.WriteLine($"[JWT] Challenge: {ctx.Error} {ctx.ErrorDescription}");
          return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
          Console.WriteLine("[JWT] Token validated");
          return Task.CompletedTask;
        }
      };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IJwtProvider, JwtProvider>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<UpdateClientProfileValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<ChangePasswordValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<UpsertMasterProfileValidator>();
builder.Services.AddScoped<IMasterService, MasterService>();

builder.Services.AddSwaggerGen(c =>
{
  c.SwaggerDoc("v1", new OpenApiInfo { Title = "BeTendlyBE API", Version = "v1" });
  c.OperationFilter<GlobalProblemDetailsExamplesFilter>();

  var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
  var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
  c.IncludeXmlComments(xmlPath);

  c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
  {
    Description = "Вставьте ТОЛЬКО JWT (без 'Bearer '). Заголовок добавится автоматически.",
    Name = "Authorization",
    In = ParameterLocation.Header,
    Type = SecuritySchemeType.Http,
    Scheme = "bearer",
    BearerFormat = "JWT"
  });

  c.EnableAnnotations();
  c.OperationFilter<BearerAuthOperationFilter>();
});




builder.Services.AddCors(o =>
{
  o.AddPolicy("AppCors", p => p
      .AllowAnyOrigin()
      .AllowAnyHeader()
      .AllowAnyMethod()
  );
});

builder.Services.AddProblemDetails(options =>
{
  options.CustomizeProblemDetails = ctx =>
           {
             ctx.ProblemDetails.Type ??= "about:blank";
             ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
           };
});

var app = builder.Build();

var fwd = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

// Health и корень (возвращают 200, не 404)
app.MapGet("/healthz", () => Results.Ok("OK"));
app.MapGet("/", () => Results.Ok("BeTendly API is running"));

using (var scope = app.Services.CreateScope())
{
  var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
  await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
  app.UseSwagger();
  app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BeTendly API v1");
        c.RoutePrefix = "docs";
    });
}


app.UseExceptionHandler(errorApp =>
{
  errorApp.Run(async context =>
          {
            var exceptionHandler = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            var ex = exceptionHandler?.Error;

            var problem = new ProblemDetails
            {
              Type = "https://api.betendy.example/errors/unexpected",
              Title = "unexpected_error",
              Detail = app.Environment.IsDevelopment() ? ex?.ToString() : "Unexpected error.",
              Status = StatusCodes.Status500InternalServerError
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = problem.Status ?? 500;
            await context.Response.WriteAsJsonAsync(problem);
          });
});



app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AppCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();


static string? FirstNonEmpty(params string?[] vals)
    => vals.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));