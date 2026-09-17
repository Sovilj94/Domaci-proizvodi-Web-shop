using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Tests;

/// <summary>ICurrentUser bez HTTP konteksta — testovi biraju ko je prijavljen.</summary>
public class FakeCurrentUser(Guid? id, UserRole role = UserRole.Customer) : ICurrentUser
{
    public Guid? Id => id;
    public string? Email => id is null ? null : $"{id}@test.local";
    public UserRole? Role => id is null ? null : role;
    public bool IsAuthenticated => id is not null;
    public bool IsInRole(UserRole r) => Role == r;
}
