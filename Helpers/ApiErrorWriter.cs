using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PharmacyFinder.API.DTOs;

namespace PharmacyFinder.API.Helpers;

public static class ApiErrorWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task WriteAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string code,
        string message,
        Dictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new ApiErrorResponse
        {
            Code = code,
            Message = message,
            StatusCode = (int)statusCode,
            Errors = errors
        }, JsonOptions));
    }

    public static IActionResult ValidationProblem(ModelStateDictionary modelState)
    {
        var errors = modelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => e.Key,
                e => e.Value!.Errors.Select(x => string.IsNullOrWhiteSpace(x.ErrorMessage) ? "Invalid value." : x.ErrorMessage).ToArray());

        var response = new ApiErrorResponse
        {
            Code = ApiErrorCodes.ValidationFailed,
            Message = "One or more validation errors occurred.",
            StatusCode = StatusCodes.Status400BadRequest,
            Errors = errors
        };

        return new BadRequestObjectResult(response);
    }
}
