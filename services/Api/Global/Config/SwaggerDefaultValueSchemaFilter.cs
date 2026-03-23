using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel;
using System.Reflection;

namespace Api.Global.Config
{
    public class SwaggerDefaultValueSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema.Properties == null) return;

            foreach (var property in schema.Properties)
            {
                var propInfo = context.Type.GetProperty(property.Key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (propInfo != null)
                {
                    var defaultValue = propInfo.GetCustomAttribute<DefaultValueAttribute>()?.Value;
                    if (defaultValue != null)
                    {
                        property.Value.Example = new OpenApiString(defaultValue.ToString());
                    }
                }
            }
        }
    }
}
