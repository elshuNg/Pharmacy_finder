using System.Net;

namespace PharmacyFinder.API.Helpers;

public class ApiException : Exception
{
    public string Code { get; }
    public HttpStatusCode StatusCode { get; }

    public ApiException(string code, string message, HttpStatusCode statusCode) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public static ApiException BadRequest(string code, string message) =>
        new(code, message, HttpStatusCode.BadRequest);

    public static ApiException NotFound(string code, string message) =>
        new(code, message, HttpStatusCode.NotFound);

    public static ApiException Forbidden(string code, string message) =>
        new(code, message, HttpStatusCode.Forbidden);

    public static ApiException Unauthorized(string code, string message) =>
        new(code, message, HttpStatusCode.Unauthorized);

    public static ApiException Conflict(string code, string message) =>
        new(code, message, HttpStatusCode.Conflict);

    public static ApiException Internal(string message) =>
        new(ApiErrorCodes.InternalError, message, HttpStatusCode.InternalServerError);
}
