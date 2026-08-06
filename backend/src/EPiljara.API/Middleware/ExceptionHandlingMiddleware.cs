using EPiljara.Application.Common.Models;
using EPiljara.Domain.Common;
using ValidationException = EPiljara.Application.Common.Exceptions.ValidationException;

namespace EPiljara.API.Middleware;

/// <summary>Translates exceptions into the { data, error, meta } envelope with the right status code.</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            await WriteAsync(context, 400, "validation_error", ex.Message,
                ex.Errors.ToDictionary(kv => kv.Key, kv => kv.Value));
        }
        catch (NotFoundException ex)
        {
            await WriteAsync(context, 404, ex.Code, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await WriteAsync(context, 403, ex.Code, ex.Message);
        }
        catch (UnauthorizedException ex)
        {
            await WriteAsync(context, 401, ex.Code, ex.Message);
        }
        catch (ConflictException ex)
        {
            await WriteAsync(context, 409, ex.Code, ex.Message);
        }
        catch (DomainException ex)
        {
            await WriteAsync(context, 400, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteAsync(context, 500, "server_error",
                "Došlo je do neočekivane greške.");
        }
    }

    private static Task WriteAsync(HttpContext context, int status, string code, string message,
        IDictionary<string, string[]>? details = null)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var body = ApiResponse<object>.Fail(code, message, details);
        return context.Response.WriteAsJsonAsync(body);
    }
}
