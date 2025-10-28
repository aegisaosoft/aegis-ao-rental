/*
 *
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
 * Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
 * SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
 * WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
 * Alexander Orlov.
 *
 * Author: Alexander Orlov
 *
 */

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CarRental.Api.Filters;

public class SwaggerFileOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        try
        {
            var fileUploadMime = "multipart/form-data";
            
            if (operation?.RequestBody?.Content == null)
                return;

            if (operation.RequestBody.Content.ContainsKey(fileUploadMime))
            {
                var fileParams = context.MethodInfo.GetParameters()
                    .Where(p => p.ParameterType == typeof(IFormFile))
                    .ToList();

                if (!fileParams.Any())
                    return;

                var uploadFileSchema = new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>()
                };

                foreach (var param in fileParams)
                {
                    uploadFileSchema.Properties.Add(param.Name!, new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    });
                }

                operation.RequestBody.Content[fileUploadMime].Schema = uploadFileSchema;
            }
        }
        catch
        {
            // Silently ignore any errors in Swagger generation
            // This prevents the entire API from failing if Swagger has issues
        }
    }
}

