using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Users.Config;

public class SwaggerDefaultValueSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema openApiSchema || openApiSchema.Properties is null) return;

        foreach (var property in openApiSchema.Properties)
        {
            var propInfo = context.Type.GetProperty(property.Key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (propInfo is null) continue;

            var defaultValue = propInfo.GetCustomAttribute<DefaultValueAttribute>()?.Value;
            if (defaultValue is null) continue;

            if (property.Value is OpenApiSchema propertySchema)
                propertySchema.Example = JsonValue.Create(defaultValue.ToString());
        }
    }
}
