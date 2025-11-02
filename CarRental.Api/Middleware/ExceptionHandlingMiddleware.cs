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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using System.Text.Json;
using CarRental.Api.DTOs;

namespace CarRental.Api.Middleware;

/// <summary>
/// Middleware to handle exceptions globally and format them with stack traces
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var code = HttpStatusCode.InternalServerError; // 500 if unexpected
        var message = "An error occurred while processing your request.";

        // Set status code based on exception type
        if (exception is UnauthorizedAccessException)
        {
            code = HttpStatusCode.Forbidden;
            message = "Access denied.";
        }
        else if (exception is KeyNotFoundException)
        {
            code = HttpStatusCode.NotFound;
            message = "Resource not found.";
        }
        else if (exception is InvalidOperationException || exception is ArgumentException)
        {
            code = HttpStatusCode.BadRequest;
            message = exception.Message;
        }

        // Create standardized error response with stack trace
        var response = new ApiResponseDto
        {
            Result = new { },
            Reason = (int)code,
            Message = message,
            StackTrace = exception.StackTrace
        };

        var result = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;
        return context.Response.WriteAsync(result);
    }
}

