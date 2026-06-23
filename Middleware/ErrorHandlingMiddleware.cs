using System.Net;
using Microsoft.EntityFrameworkCore;
using PharmacyFinder.API.Helpers;

namespace PharmacyFinder.API.Middleware;

public class ErrorHandlingMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            await ApiErrorWriter.WriteAsync(context, ex.StatusCode, ex.Code, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            await ApiErrorWriter.WriteAsync(context, HttpStatusCode.NotFound, ApiErrorCodes.NotFound, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            await ApiErrorWriter.WriteAsync(context, HttpStatusCode.Forbidden, ApiErrorCodes.Forbidden, ex.Message);
        }
        catch (ArgumentException ex)
        {
            await ApiErrorWriter.WriteAsync(context, HttpStatusCode.BadRequest, ApiErrorCodes.BadRequest, ex.Message);
        }
        catch (BadHttpRequestException ex)
        {
            await ApiErrorWriter.WriteAsync(context, HttpStatusCode.BadRequest, ApiErrorCodes.BadRequest, ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            await ApiErrorWriter.WriteAsync(context, HttpStatusCode.Conflict, ApiErrorCodes.Conflict,
                "This prescription was already updated. Please upload again.");
        }
        catch (Exception ex)
        {
            var message = environment.IsDevelopment()
                ? GetDevelopmentMessage(ex)
                : "An unexpected error occurred.";
            await ApiErrorWriter.WriteAsync(context, HttpStatusCode.InternalServerError, ApiErrorCodes.InternalError, message);
        }
    }

    private static string GetDevelopmentMessage(Exception ex)
    {
        var root = ex;
        while (root.InnerException is not null)
            root = root.InnerException;

        return root == ex ? ex.Message : $"{ex.Message} -> {root.Message}";
    }
}
