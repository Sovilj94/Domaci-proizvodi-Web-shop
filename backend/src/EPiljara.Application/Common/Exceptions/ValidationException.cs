using FluentValidation.Results;

namespace EPiljara.Application.Common.Exceptions;

/// <summary>Aggregates FluentValidation failures into field-keyed error details.</summary>
public class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("Provjerite unesene podatke.")
    {
        Errors = failures
            .GroupBy(f => ToCamelPath(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());
    }

    /// <summary>"Customer.Name" -> "customer.name" so keys match the frontend contract.</summary>
    private static string ToCamelPath(string property) =>
        string.Join('.', property.Split('.').Select(segment =>
            string.IsNullOrEmpty(segment)
                ? segment
                : char.ToLowerInvariant(segment[0]) + segment[1..]));
}
