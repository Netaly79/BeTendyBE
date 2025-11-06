using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public sealed class GlobalProblemDetailsExamplesFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var (statusCode, response) in operation.Responses)
        {
            // ищем ответы, у которых схема = ProblemDetails или ValidationProblemDetails
            if (!TryGetMedia(response, out var media)) continue;

            var schemaRef = media.Schema?.Reference?.Id;
            if (schemaRef is null) continue;

            if (schemaRef == nameof(ProblemDetails))
            {
                media.Example = ExampleForProblem(statusCode);
            }
            else if (schemaRef == nameof(ValidationProblemDetails))
            {
                media.Example = ExampleForValidationProblem(statusCode);
            }
        }
    }

    private static bool TryGetMedia(OpenApiResponse response, out OpenApiMediaType media)
    {
        // покрываем оба варианта контента
#pragma warning disable CS8601 // Possible null reference assignment.
        if (response.Content.TryGetValue("application/problem+json", out media)) return true;
    if (response.Content.TryGetValue("application/json", out media)) return true;
#pragma warning restore CS8601 // Possible null reference assignment.
    media = null!;
        return false;
    }

    private static IOpenApiAny ExampleForProblem(string statusCode)
    {
        var code = int.TryParse(statusCode, out var c) ? c : StatusCodes.Status500InternalServerError;

        var example = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{code}",
            Title = code switch
            {
                StatusCodes.Status401Unauthorized => "Unauthorized",
                StatusCodes.Status403Forbidden => "Forbidden",
                StatusCodes.Status404NotFound => "Not Found",
                _ => "Error"
            },
            Status = code,
            Detail = code switch
            {
                StatusCodes.Status401Unauthorized => "Missing or invalid access token.",
                StatusCodes.Status403Forbidden => "You do not have permission to access this resource.",
                StatusCodes.Status404NotFound => "Resource was not found.",
                _ => "An error occurred."
            },
            Instance = "/api/master/profile"
        };

        return new OpenApiString(JsonSerializer.Serialize(example));
    }

    private static IOpenApiAny ExampleForValidationProblem(string statusCode)
    {
        var example = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["name"] = new[] { "The name field is required." },
            ["phone"] = new[] { "Invalid phone number format." }
        })
        {
            Type = $"https://httpstatuses.io/400",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Detail = "See the errors property for details.",
            Instance = "/api/master/profile"
        };

        return new OpenApiString(JsonSerializer.Serialize(example));
    }
}
