

using BeTendyBE.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;


var builder = WebApplication.CreateBuilder(args);

//configurations
var configuration = builder.Configuration;

string? envUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string? envConn = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

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
        SslMode = SslMode.Disable
    };
    return builder.ToString();
}

string connectionString =
    !string.IsNullOrWhiteSpace(envConn) ? envConn :
    !string.IsNullOrWhiteSpace(envUrl) ? BuildFromDatabaseUrl(envUrl) :
    configuration.GetConnectionString("Default")!;

    builder.Services.AddDbContext<AppDbContext>(opt => opt
        .UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o =>
{
    o.AddPolicy("AppCors", p => p
        .WithOrigins()
        .AllowAnyHeader()
        .AllowAnyMethod()
    );
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AppCors");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
