using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinanceApp.Dbo.Enums;
using FinanceApp.Dbo.Models;
using FinanceApp.Infrastructure.Dtos;

namespace FinanceApp.Tests.TestInfrastructure;

internal static class TestDataFactory
{
    public static Role CreateRole(
        string code,
        IEnumerable<string>? permissions = null,
        bool isSystem = false,
        string? name = null)
    {
        return new Role
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name ?? code,
            Description = $"{code} role",
            Permissions = JsonSerializer.Serialize((permissions ?? new[] { "read" }).ToList()),
            IsSystem = isSystem,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static User CreateUser(
        string email = "user@test.local",
        string password = "password123",
        bool isActive = true,
        bool isVerified = false,
        Guid? id = null)
    {
        return new User
        {
            Id = id ?? Guid.NewGuid(),
            Login = email,
            Email = email,
            PasswordHash = HashPassword(password),
            FullName = "Test User",
            Phone = "+70000000000",
            AvatarUrl = string.Empty,
            IsActive = isActive,
            IsVerified = isVerified,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Settings = JsonSerializer.Serialize(new UserSettingsDto())
        };
    }

    public static UserRole CreateUserRole(Guid userId, Guid roleId, Guid? assignedBy = null)
    {
        return new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = assignedBy ?? userId
        };
    }

    public static Tag CreateTag(
        string name,
        TagType type,
        Guid? ownerId = null,
        TagVisibility visibility = TagVisibility.Public,
        Guid? parentId = null,
        int usageCount = 0,
        bool isSystem = false,
        bool isActive = true,
        string? slug = null)
    {
        var computedSlug = slug ?? name.ToLowerInvariant().Replace(" ", "-");
        return new Tag
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = computedSlug,
            Type = type,
            Icon = "icon",
            Color = "#000000",
            ParentId = parentId,
            Level = parentId.HasValue ? 1 : 0,
            Path = parentId.HasValue ? $"parent/{computedSlug}" : computedSlug,
            IsActive = isActive,
            IsSystem = isSystem,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SortOrder = 0,
            UsageCount = usageCount,
            Visibility = visibility
        };
    }

    public static FinancialOperation CreateOperation(
        Guid ownerId,
        OperationType type,
        decimal amount,
        string currency,
        DateTime operationDateUtc,
        string description = "operation")
    {
        return new FinancialOperation
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            CreatedBy = ownerId,
            Type = type,
            Description = description,
            Notes = string.Empty,
            PaymentMethod = PaymentMethod.Card,
            OperationDateTime = operationDateUtc,
            Money = new Money(amount, currency),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            OperationTags = new List<OperationTag>()
        };
    }

    public static RefreshToken CreateRefreshToken(Guid userId, string token, DateTime expiresAtUtc, DateTime? revokedAtUtc = null)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAtUtc,
            CreatedAt = DateTime.UtcNow,
            RevokedAt = revokedAtUtc
        };
    }

    private static string HashPassword(string password)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    }
}
