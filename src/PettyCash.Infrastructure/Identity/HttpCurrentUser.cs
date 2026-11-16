using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PettyCash.Application.Abstractions;

namespace PettyCash.Infrastructure.Identity;

public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name)
        ?? accessor.HttpContext?.User.FindFirstValue("sub");

    public IReadOnlyList<string> Roles => accessor.HttpContext?.User
        .FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .ToList() ?? [];

    public bool IsInRole(string role) => Roles.Contains(role);
}
