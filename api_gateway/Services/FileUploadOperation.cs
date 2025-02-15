using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace api_gateway.Services {
  public class FileUploadOperation : IOperationFilter {
    public void Apply(OpenApiOperation operation, OperationFilterContext context) {
      var fileParams = context.ApiDescription.ParameterDescriptions
          .Where(p => p.Type == typeof(IFormFile) || p.Type == typeof(IEnumerable<IFormFile>));

      if(!fileParams.Any())
        return;

      operation.RequestBody = new OpenApiRequestBody {
        Content = new Dictionary<string, OpenApiMediaType> {
          ["multipart/form-data"] = new OpenApiMediaType {
            Schema = new OpenApiSchema {
              Type = "object",
              Properties = fileParams.ToDictionary(
                          p => p.Name,
                          p => new OpenApiSchema { Type = "string", Format = "binary" }
                      ),
              Required = fileParams.Select(p => p.Name).ToHashSet()
            }
          }
        }
      };
    }
  }
}
