namespace EPiljara.Application.Common.Models;

/// <summary>Error part of the response envelope.</summary>
public class ApiError
{
    public string Code { get; set; } = "error";
    public string Message { get; set; } = string.Empty;
    public IDictionary<string, string[]>? Details { get; set; }
}

/// <summary>
/// The consistent response envelope returned by every endpoint: { data, error, meta }.
/// </summary>
public class ApiResponse<T>
{
    public T? Data { get; set; }
    public ApiError? Error { get; set; }
    public object? Meta { get; set; }

    public static ApiResponse<T> Ok(T data, object? meta = null) =>
        new() { Data = data, Meta = meta };

    public static ApiResponse<T> Fail(string code, string message, IDictionary<string, string[]>? details = null) =>
        new() { Error = new ApiError { Code = code, Message = message, Details = details } };
}

/// <summary>Non-generic factory for error envelopes when the payload type is irrelevant.</summary>
public static class ApiResponse
{
    public static ApiResponse<object> Error(string code, string message, IDictionary<string, string[]>? details = null) =>
        new() { Error = new ApiError { Code = code, Message = message, Details = details } };
}
