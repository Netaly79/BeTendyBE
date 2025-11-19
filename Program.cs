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
using Swashbuckle.AspNetCore.Filters;
using System.Text.Json;
using Azure.Communication.Email;
using Azure;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

//configurations
var configuration = builder.Configuration;

string? envConn1 = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
string? envConn2 = Environment.GetEnvironmentVariable("POSTGRESQLCONNSTR_DefaultConnection");
string? envConn3 = Environment.GetEnvironmentVariable("CUSTOMCONNSTR_DefaultConnection");
string? envUrl = Environment.GetEnvironmentVariable("DATABASE_URL");


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
  csb.SslMode = Npgsql.SslMode.Require;
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
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddHostedService<DailyBookingCleanupService>();

builder.Services.AddSwaggerGen(c =>
{
  c.SwaggerDoc("v1", new OpenApiInfo { Title = "BeTendlyBE API", Version = "v1" });
  c.OperationFilter<GlobalProblemDetailsExamplesFilter>();
  c.ExampleFilters();

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

builder.Services.AddSwaggerExamplesFromAssemblyOf<Program>();

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

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
      o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
      o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

var env = builder.Environment.EnvironmentName;

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var azureCommConnection = builder.Configuration["AzureCommunication:ConnectionString"];
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connStr = config["AzureCommunication:ConnectionString"];

    if (string.IsNullOrWhiteSpace(connStr))
        throw new InvalidOperationException("AzureCommunicationConnectionString is not configured.");

    return new EmailClient(connStr);
});

var app = builder.Build();

var fwd = new ForwardedHeadersOptions
{
  ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

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
        var feature = context.Features.Get<IExceptionHandlerFeature>();

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
        {
            // В DEV/STAGING показываем реальную ошибку
            await context.Response.WriteAsJsonAsync(new
            {
                type = "error",
                title = feature?.Error.GetType().Name,
                detail = feature?.Error.Message,
                stack = feature?.Error.StackTrace
            });
        }
        else
        {
            // В PROD скрываем детали
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://api.betendy.example/errors/unexpected",
                title = "unexpected_error",
                status = 500,
                detail = "Unexpected error."
            });
        }
    });
});

app.MapGet("/debug/email-config", (IConfiguration config) =>
{
  var value = config["AzureCommunication:ConnectionString"];
  return new
  {
    hasValue = !string.IsNullOrWhiteSpace(value),
    length = value?.Length ?? 0
    // сам connection string не показываем!
  };
});

app.MapPost("/debug/email-test", async (EmailClient emailClient) =>
{
  try
  {
    // подставь СВОЙ адрес почты, на который можно послать тест
    var to = "netaly79@gmail.com";

    var subject = "BeTendly debug email";
    var body = "If you see this, Azure Communication Email works in Azure App Service.";

    var message = new EmailMessage(
        senderAddress: "DoNotReply@9ad31b68-e067-4e3f-a51b-5f14a0366fad.azurecomm.net",
        content: new EmailContent(subject)
        {
          PlainText = body
        },
        recipients: new EmailRecipients(new[]
        {
                new EmailAddress(to)
        })
    );

    var operation = await emailClient.SendAsync(
        Azure.WaitUntil.Completed,
        message
    );

    return Results.Ok(new
    {
      status = "ok",
      operationId = operation.Id
    });
  }
  catch (Exception ex)
  {
    return Results.Problem(
        title: ex.GetType().FullName,
        detail: ex.Message,
        statusCode: 500
    );
  }
});



app.MapPost("/debug/email-test-raw", async (EmailClient emailClient) =>
{
    try
    {
        var to = "netaly79@gmail.com";

        var message = new EmailMessage(
            senderAddress: "DoNotReply@9ad31b68-e067-4e3f-a51b-5f14a0366fad.azurecomm.net",
            content: new EmailContent("BeTendly debug email")
            {
                PlainText = "If you see this, Azure Communication Email works in Azure App Service."
            },
            recipients: new EmailRecipients(new[]
            {
                new EmailAddress(to)
            })
        );

        var operation = await emailClient.SendAsync(WaitUntil.Completed, message);

        return Results.Text(
            $"OK\nOperationId: {operation.Id}",
            "text/plain"
        );
    }
    catch (Exception ex)
    {
        // ВАЖНО: возвращаем как plain text, не кидаем дальше
        return Results.Text(
            ex.ToString(),
            "text/plain"
        );
    }
});


app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AppCors");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();


static string? FirstNonEmpty(params string?[] vals)
    => vals.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));