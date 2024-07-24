using System.Reflection;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using PuppeteerSharp;

namespace ContentXtractor.Extract
{
    [OpenApiExample(typeof(SampleRequestModelExample))]
    public class RequestBodyModel
    {
        public required string Url { get; init; }

        public ViewPortOptions? ViewPortOptions { get; init; }

        public bool? DisableLinks { get; init; }

        public bool? ReturnRawHtml { get; init; }

        [JsonConverter(typeof(StringEnumConverter))]
        public WaitUntilNavigation? WaitUntil { get; init; }

        public int? ReadingModeTimeout { get; init; }
    }

    public class SampleRequestModelExample : OpenApiExample<RequestBodyModel>
    {
        public override IOpenApiExample<RequestBodyModel> Build(NamingStrategy? namingStrategy = null)
        {
            var sample = new RequestBodyModel
            {
                Url = "https://www.housing.qld.gov.au/",
                ViewPortOptions = new ViewPortOptions
                {
                    Width = 1920,
                    Height = 1080,
                },
                DisableLinks = false,
                ReturnRawHtml = false,
                WaitUntil = WaitUntilNavigation.DOMContentLoaded,
                ReadingModeTimeout = null
            };

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CustomContractResolver
                {
                    NamingStrategy = namingStrategy ?? new CamelCaseNamingStrategy()
                },
                NullValueHandling = NullValueHandling.Ignore
            };

            Examples.Add("sample", new OpenApiExample
            {
                Value = OpenApiExampleFactory.CreateInstance(sample, settings)
            });

            return this;
        }

        private class CustomContractResolver : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                var members = base.GetSerializableMembers(objectType);
                if (objectType == typeof(ViewPortOptions))
                {
                    return members.Where(m => m.Name == nameof(ViewPortOptions.Width) || m.Name == nameof(ViewPortOptions.Height)).ToList();
                }

                return members;
            }
        }
    }
}