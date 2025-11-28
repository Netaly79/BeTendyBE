using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BeTendlyBE.Helpers;
public sealed class BearerAuthOperationFilter : IOperationFilter
{
  public void Apply(OpenApiOperation operation, OperationFilterContext context)
  {
    bool hasAuthorize =
        context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() ||
        (context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() ?? false) ||
        context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<AuthorizeAttribute>().Any();

    bool allowAnonymous =
        context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any() ||
        context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any();

    if (hasAuthorize && !allowAnonymous)
    {
      operation.Security ??= new List<OpenApiSecurityRequirement>();
      operation.Security.Add(new OpenApiSecurityRequirement
      {
        [new OpenApiSecurityScheme
        {
          Reference = new OpenApiReference
          {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
          }
        }] = new List<string>()
      });
    }
  }
}
