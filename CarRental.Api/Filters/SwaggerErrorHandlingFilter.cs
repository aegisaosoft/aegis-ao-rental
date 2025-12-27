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
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace CarRental.Api.Filters;

/// <summary>
/// Swagger schema filter to handle errors during schema generation gracefully
/// </summary>
public class SwaggerErrorHandlingFilter : ISchemaFilter
{
    private readonly ILogger<SwaggerErrorHandlingFilter> _logger;

    public SwaggerErrorHandlingFilter(ILogger<SwaggerErrorHandlingFilter> logger)
    {
        _logger = logger;
    }

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        try
        {
            // Handle circular references by setting additional properties
            if (schema.Properties != null)
            {
                foreach (var property in schema.Properties)
                {
                    try
                    {
                        // Ensure all properties have valid types
                        if (property.Value.Reference != null && string.IsNullOrEmpty(property.Value.Reference.Id))
                        {
                            property.Value.Reference = null;
                            property.Value.Type = "object";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing property {PropertyName} in schema {SchemaType}", 
                            property.Key, context.Type.Name);
                        // Set a fallback type
                        property.Value.Type = "object";
                        property.Value.Reference = null;
                    }
                }
            }

            // Handle nullable reference types
            if (context.Type.IsValueType == false && Nullable.GetUnderlyingType(context.Type) == null)
            {
                schema.Nullable = true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in SwaggerErrorHandlingFilter for type {TypeName}", context.Type.Name);
            // Set a safe fallback schema
            schema.Type = "object";
            schema.Properties = null;
            schema.Reference = null;
        }
    }
}

