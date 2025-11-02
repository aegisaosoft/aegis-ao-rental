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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using CarRental.Api.DTOs;

namespace CarRental.Api.Filters;

/// <summary>
/// Action filter that wraps all API responses in a standardized format
/// </summary>
public class StandardizedResponseFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Nothing to do before action executes
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Skip if result is null
        if (context.Result == null)
        {
            return;
        }

        // Handle all result types
        int statusCode = 200;
        object? data = null;

        if (context.Result is ObjectResult objectResult)
        {
            statusCode = objectResult.StatusCode ?? 200;
            data = objectResult.Value;

            // Skip if already wrapped in ApiResponseDto or LoginResponseDto
            if (data is ApiResponseDto || data is LoginResponseDto)
            {
                return;
            }

            // Skip Swagger/OpenAPI responses
            if (data?.GetType().FullName?.Contains("OpenApi") == true)
            {
                return;
            }
        }
        else if (context.Result is OkResult okResult)
        {
            statusCode = okResult.StatusCode;
            data = new { success = true };
        }
        else if (context.Result is CreatedResult createdResult)
        {
            statusCode = createdResult.StatusCode ?? 201;
            data = createdResult.Value ?? new { success = true };
        }
        else if (context.Result is CreatedAtActionResult createdAtActionResult)
        {
            statusCode = createdAtActionResult.StatusCode ?? 201;
            data = createdAtActionResult.Value ?? new { success = true };
        }
        else if (context.Result is NotFoundResult notFoundResult)
        {
            statusCode = notFoundResult.StatusCode;
        }
        else if (context.Result is BadRequestResult badRequestResult)
        {
            statusCode = badRequestResult.StatusCode;
        }
        else if (context.Result is UnauthorizedResult unauthorizedResult)
        {
            statusCode = unauthorizedResult.StatusCode;
        }
        else if (context.Result is ForbidResult)
        {
            statusCode = 403;
        }
        else if (context.Result is StatusCodeResult statusCodeResult)
        {
            statusCode = statusCodeResult.StatusCode;
        }
        else if (context.Result is NotFoundObjectResult notFoundObjectResult)
        {
            statusCode = notFoundObjectResult.StatusCode ?? 404;
            data = notFoundObjectResult.Value;
        }
        else if (context.Result is BadRequestObjectResult badRequestObjectResult)
        {
            statusCode = badRequestObjectResult.StatusCode ?? 400;
            data = badRequestObjectResult.Value;
        }
        else if (context.Result is UnauthorizedObjectResult unauthorizedObjectResult)
        {
            statusCode = unauthorizedObjectResult.StatusCode ?? 401;
            data = unauthorizedObjectResult.Value;
        }
        else if (context.Result is ConflictObjectResult conflictObjectResult)
        {
            statusCode = conflictObjectResult.StatusCode ?? 409;
            data = conflictObjectResult.Value;
        }
        else
        {
            // Unknown result type, skip wrapping
            return;
        }

        // Wrap all responses in standardized format
        var response = new ApiResponseDto
        {
            Result = data ?? (statusCode >= 200 && statusCode < 300 ? new { success = true } : new { }),
            Reason = 0,
            Message = null,
            StackTrace = null
        };

        // Extract error message and stack trace from data if it's an error response
        if (statusCode >= 400 && data != null)
        {
            // Try to extract message from various possible structures
            var dataType = data.GetType();
            var messageProp = dataType.GetProperty("message");
            if (messageProp != null)
            {
                response.Message = messageProp.GetValue(data)?.ToString();
            }
            else
            {
                var errorProp = dataType.GetProperty("error");
                if (errorProp != null)
                {
                    response.Message = errorProp.GetValue(data)?.ToString();
                }
                else if (data is string stringMessage)
                {
                    response.Message = stringMessage;
                }
            }

            // Try to extract stack trace if available
            var stackTraceProp = dataType.GetProperty("stackTrace");
            if (stackTraceProp != null)
            {
                response.StackTrace = stackTraceProp.GetValue(data)?.ToString();
            }
            else
            {
                // Try alternative property names
                var traceProp = dataType.GetProperty("trace");
                if (traceProp != null)
                {
                    response.StackTrace = traceProp.GetValue(data)?.ToString();
                }
            }

            // Set reason to status code for error responses
            response.Reason = statusCode;

            // For errors, result should be minimal or empty
            response.Result = new { };
        }

        context.Result = new ObjectResult(response)
        {
            StatusCode = statusCode
        };
    }
}

