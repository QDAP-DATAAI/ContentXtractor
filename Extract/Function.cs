using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using PuppeteerSharp;

namespace ContentXtractor.Extract
{
    public class Function(ILoggerFactory loggerFactory)
    {
        [OpenApiOperation(operationId: "extract", Description = "Extracts content from a URL using (or attempting to use) Chrome's Reading Mode.", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiRequestBody("application/json", typeof(RequestBodyModel), Required = true)]
        [Function("extract")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest request)
        {
            var logger = loggerFactory.CreateLogger<Function>();

            try
            {
                var model = await JsonSerializer.DeserializeAsync<RequestBodyModel>(request.Body, jsonSerializerOptions, request.HttpContext.RequestAborted) ?? throw new ArgumentNullException(nameof(request.Body));

                var result = await ChromeExtractor.Extract(model.Url, headless: true, model.ViewPortOptions, model.DisableLinks.GetValueOrDefault(), model.ReturnRawHtml.GetValueOrDefault(), model.WaitUntil, model.ReadingModeTimeout, loggerFactory, request.HttpContext.RequestAborted);
                if (!result.Success)
                    return new BadRequestObjectResult(result);

                return new OkObjectResult(result);
            }
            catch (NavigationException ex)
            {
                logger.LogError(ex, "Navigation error");

                return new BadRequestObjectResult(new
                {
                    error = ex.Message,
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error");

                return new ObjectResult(new
                {
                    error = ex.Message,
                })
                {
                    StatusCode = 502 // Bad Gateway
                };
            }
        }

        private static readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter(),
            },
        };
    }
}