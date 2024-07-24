using System.Reflection;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace ContentXtractor
{
    public class EnumDocumentFilter : IDocumentFilter
    {
        public void Apply(IHttpRequestDataObject req, OpenApiDocument document)
        {
            foreach (var schema in document.Components.Schemas)
            {
                foreach (var property in schema.Value.Properties)
                {
                    if (property.Value.Enum.Any())
                    {
                        var schemaType = Assembly.GetExecutingAssembly().GetTypes().Single(t => t.Name == Camel(schema.Key));
                        var propertyType = schemaType.GetProperty(Camel(property.Key))!.PropertyType;
                        property.Value.Enum = Enum.GetNames(Nullable.GetUnderlyingType(propertyType) ?? propertyType)
                            .Select(name => new OpenApiString(name))
                            .Cast<IOpenApiAny>()
                            .ToList();

                        property.Value.Type = "string";
                        property.Value.Default = property.Value.Enum.First();
                        property.Value.Format = null;
                    }
                }
            }
        }

        private static string Camel(string key) => $"{char.ToUpperInvariant(key[0])}{key[1..]}";
    }
}