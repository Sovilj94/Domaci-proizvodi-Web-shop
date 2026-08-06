namespace EPiljara.Domain.Common;

/// <summary>A business-rule violation. Surfaces as HTTP 400 with an error envelope.</summary>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string message, string code = "domain_error") : base(message)
    {
        Code = code;
    }
}

/// <summary>A requested resource does not exist. Surfaces as HTTP 404.</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message, "not_found") { }

    public NotFoundException(string entity, object key)
        : base($"{entity} ({key}) nije pronađen.", "not_found") { }
}

/// <summary>The caller is authenticated but not allowed to access the resource. Surfaces as HTTP 403.</summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "Nemate pristup ovom resursu.")
        : base(message, "forbidden") { }
}

/// <summary>A uniqueness/state conflict (e.g. email already registered). Surfaces as HTTP 409.</summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message, string code = "conflict") : base(message, code) { }
}

/// <summary>Authentication failed (bad credentials / missing token). Surfaces as HTTP 401.</summary>
public sealed class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message, string code = "unauthorized") : base(message, code) { }
}
