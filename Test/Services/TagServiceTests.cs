using FinanceApp.Dbo.Enums;
using FinanceApp.Dbo.Models;
using FinanceApp.Infrastructure.Dtos;
using FinanceApp.Infrastructure.Services;
using FinanceApp.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Tests.Services;

public class TagServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldBuildNestedPath_WhenParentProvided()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerId = Guid.NewGuid();
        var parent = TestDataFactory.CreateTag("Parent", TagType.Expense, ownerId, TagVisibility.Public, parentId: null, slug: "parent");
        parent.Level = 0;
        parent.Path = "parent";
        context.Tags.Add(parent);
        await context.SaveChangesAsync();

        var service = new TagService(context);
        var created = await service.CreateAsync(ownerId, new CreateTagDto
        {
            Name = "Child Tag",
            ParentId = parent.Id,
            Type = TagType.Expense,
            Visibility = TagVisibility.Private,
            Color = "#fff",
            Icon = "i"
        });

        created.ParentId.Should().Be(parent.Id);
        created.Level.Should().Be(1);
        created.Slug.Should().Be("child-tag");
        (await context.Tags.FindAsync(created.Id))!.Path.Should().Be("parent/child-tag");
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenCircularDependencyDetected()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerId = Guid.NewGuid();
        var parent = TestDataFactory.CreateTag("Parent", TagType.Expense, ownerId, parentId: null, slug: "parent");
        var child = TestDataFactory.CreateTag("Child", TagType.Expense, ownerId, parentId: parent.Id, slug: "child");
        child.Level = 1;
        child.Path = "parent/child";
        context.AddRange(parent, child);
        await context.SaveChangesAsync();
        var service = new TagService(context);

        var action = () => service.UpdateAsync(parent.Id, new UpdateTagDto
        {
            ParentId = child.Id,
            Visibility = TagVisibility.Private
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*circular dependency*");
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrow_WhenTagHasChildren()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerId = Guid.NewGuid();
        var parent = TestDataFactory.CreateTag("Parent", TagType.Expense, ownerId, parentId: null);
        var child = TestDataFactory.CreateTag("Child", TagType.Expense, ownerId, parentId: parent.Id);
        context.AddRange(parent, child);
        await context.SaveChangesAsync();
        var service = new TagService(context);

        var action = () => service.DeleteAsync(parent.Id);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*with children*");
    }

    [Fact]
    public async Task GetByTypeAsync_ShouldReturnPublicAndOwnTags_WhenUserProvided()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        context.Tags.AddRange(
            TestDataFactory.CreateTag("Public", TagType.Expense, otherUserId, TagVisibility.Public),
            TestDataFactory.CreateTag("OwnPrivate", TagType.Expense, ownerId, TagVisibility.Private),
            TestDataFactory.CreateTag("OtherPrivate", TagType.Expense, otherUserId, TagVisibility.Private),
            TestDataFactory.CreateTag("IncomeTag", TagType.Income, ownerId, TagVisibility.Public));
        await context.SaveChangesAsync();
        var service = new TagService(context);

        var result = await service.GetByTypeAsync(TagType.Expense, ownerId);

        var names = result.Select(t => t.Name).ToList();
        names.Should().Contain("Public");
        names.Should().Contain("OwnPrivate");
        names.Should().NotContain("OtherPrivate");
        names.Should().NotContain("IncomeTag");
    }

    [Fact]
    public async Task GetPopularAsync_ShouldOrderByUsageCountDescending()
    {
        await using var context = TestDbFactory.CreateContext();
        context.Tags.AddRange(
            TestDataFactory.CreateTag("T1", TagType.Expense, visibility: TagVisibility.Public, usageCount: 1),
            TestDataFactory.CreateTag("T2", TagType.Expense, visibility: TagVisibility.Public, usageCount: 10),
            TestDataFactory.CreateTag("T3", TagType.Expense, visibility: TagVisibility.Private, usageCount: 99));
        await context.SaveChangesAsync();
        var service = new TagService(context);

        var result = await service.GetPopularAsync(2);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("T2");
    }

    [Fact]
    public async Task SearchAsync_ShouldExcludePrivateForeignTags_WhenUserNotProvided()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerId = Guid.NewGuid();
        context.Tags.AddRange(
            TestDataFactory.CreateTag("Alpha", TagType.Expense, ownerId, TagVisibility.Public, slug: "alpha"),
            TestDataFactory.CreateTag("Alpha Secret", TagType.Expense, ownerId, TagVisibility.Private, slug: "alpha-secret"));
        await context.SaveChangesAsync();
        var service = new TagService(context);

        var result = await service.SearchAsync("alpha");

        result.Select(t => t.Name).Should().Contain("Alpha");
        result.Select(t => t.Name).Should().NotContain("Alpha Secret");
    }

    [Fact]
    public async Task IncrementUsageAsync_ShouldIncreaseUsageCount()
    {
        await using var context = TestDbFactory.CreateContext();
        var tag = TestDataFactory.CreateTag("Counter", TagType.Expense, usageCount: 2);
        context.Tags.Add(tag);
        await context.SaveChangesAsync();
        var service = new TagService(context);

        await service.IncrementUsageAsync(tag.Id);

        (await context.Tags.FindAsync(tag.Id))!.UsageCount.Should().Be(3);
    }

    [Fact]
    public async Task ChangeVisibilityAsync_ShouldUpdateOnlyOwnerNonSystemTag()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerId = Guid.NewGuid();
        var foreignUserId = Guid.NewGuid();
        var ownTag = TestDataFactory.CreateTag("Own", TagType.Expense, ownerId, TagVisibility.Private, isSystem: false);
        var systemTag = TestDataFactory.CreateTag("System", TagType.Expense, ownerId, TagVisibility.Private, isSystem: true);
        context.AddRange(ownTag, systemTag);
        await context.SaveChangesAsync();
        var service = new TagService(context);

        var ownResult = await service.ChangeVisibilityAsync(ownTag.Id, TagVisibility.Public, ownerId);
        var foreignResult = await service.ChangeVisibilityAsync(ownTag.Id, TagVisibility.Private, foreignUserId);
        var systemResult = await service.ChangeVisibilityAsync(systemTag.Id, TagVisibility.Public, ownerId);

        ownResult.Should().BeTrue();
        foreignResult.Should().BeFalse();
        systemResult.Should().BeFalse();
        (await context.Tags.FindAsync(ownTag.Id))!.Visibility.Should().Be(TagVisibility.Public);
    }

    [Fact]
    public async Task GetByIdAndGetBySlug_ShouldReturnMappedHierarchy()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerId = Guid.NewGuid();
        var parent = TestDataFactory.CreateTag("Parent", TagType.Expense, ownerId, TagVisibility.Public, slug: "parent");
        var child = TestDataFactory.CreateTag("Child", TagType.Expense, ownerId, TagVisibility.Public, parent.Id, slug: "child");
        var grand = TestDataFactory.CreateTag("Grand", TagType.Expense, ownerId, TagVisibility.Public, child.Id, slug: "grand");
        parent.Path = "parent";
        child.Path = "parent/child";
        grand.Path = "parent/child/grand";
        context.AddRange(parent, child, grand);
        await context.SaveChangesAsync();
        var service = new TagService(context);

        var byId = await service.GetByIdAsync(parent.Id);
        var bySlug = await service.GetBySlugAsync("parent");
        var missing = await service.GetByIdAsync(Guid.NewGuid());

        byId.Should().NotBeNull();
        byId.Children.Should().ContainSingle(c => c.Slug == "child");
        byId.Children.Single().Children.Should().ContainSingle(c => c.Slug == "grand");
        bySlug.Should().NotBeNull();
        missing.Should().BeNull();
    }

    [Fact]
    public async Task GetTreeAndGetByType_ShouldReturnOnlyPublic_WhenUserNotSpecified()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerId = Guid.NewGuid();
        var publicRoot = TestDataFactory.CreateTag("PublicRoot", TagType.Expense, ownerId, TagVisibility.Public, parentId: null);
        var privateRoot = TestDataFactory.CreateTag("PrivateRoot", TagType.Expense, ownerId, TagVisibility.Private, parentId: null);
        var incomeRoot = TestDataFactory.CreateTag("IncomeRoot", TagType.Income, ownerId, TagVisibility.Public, parentId: null);
        context.AddRange(publicRoot, privateRoot, incomeRoot);
        await context.SaveChangesAsync();
        var service = new TagService(context);

        var tree = await service.GetTreeAsync(TagType.Expense, null);
        var byType = await service.GetByTypeAsync(TagType.Expense, null);

        tree.Select(t => t.Name).Should().Contain("PublicRoot");
        tree.Select(t => t.Name).Should().NotContain("PrivateRoot");
        byType.Select(t => t.Name).Should().Contain("PublicRoot");
        byType.Select(t => t.Name).Should().NotContain("PrivateRoot");
    }

    [Fact]
    public async Task SearchAsync_ShouldIncludeOwnPrivateTags_WhenUserProvided()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerId = Guid.NewGuid();
        var anotherId = Guid.NewGuid();
        context.Tags.AddRange(
            TestDataFactory.CreateTag("Alpha Public", TagType.Expense, anotherId, TagVisibility.Public, slug: "alpha-public"),
            TestDataFactory.CreateTag("Alpha Mine", TagType.Expense, ownerId, TagVisibility.Private, slug: "alpha-mine"),
            TestDataFactory.CreateTag("Alpha Foreign", TagType.Expense, anotherId, TagVisibility.Private, slug: "alpha-foreign"));
        await context.SaveChangesAsync();
        var service = new TagService(context);

        var result = await service.SearchAsync("alpha", ownerId);

        result.Select(x => x.Name).Should().Contain("Alpha Public");
        result.Select(x => x.Name).Should().Contain("Alpha Mine");
        result.Select(x => x.Name).Should().NotContain("Alpha Foreign");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdatePathAndChildren_WhenTagRenamedAndMoved()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        var rootA = TestDataFactory.CreateTag("RootA", TagType.Expense, ownerA, TagVisibility.Public, parentId: null, slug: "root-a");
        var rootB = TestDataFactory.CreateTag("RootB", TagType.Expense, ownerA, TagVisibility.Public, parentId: null, slug: "root-b");
        rootA.Path = "root-a";
        rootB.Path = "root-b";
        var child = TestDataFactory.CreateTag("Old Child", TagType.Expense, ownerA, TagVisibility.Private, rootA.Id, slug: "old-child");
        child.Path = "root-a/old-child";
        child.Level = 1;
        var grand = TestDataFactory.CreateTag("Grand Child", TagType.Expense, ownerA, TagVisibility.Private, child.Id, slug: "grand-child");
        grand.Path = "root-a/old-child/grand-child";
        grand.Level = 2;
        context.AddRange(rootA, rootB, child, grand);
        await context.SaveChangesAsync();
        var service = new TagService(context);

        var updated = await service.UpdateAsync(child.Id, new UpdateTagDto
        {
            Name = "Moved Child",
            ParentId = rootB.Id,
            Type = TagType.Income,
            Icon = "new-icon",
            Color = "#ABCDEF",
            OwnerId = ownerB,
            Visibility = TagVisibility.Public,
            SortOrder = 10
        });

        updated.Name.Should().Be("Moved Child");
        updated.Slug.Should().Be("moved-child");
        updated.Visibility.Should().Be("Public");
        updated.ParentId.Should().Be(rootB.Id);

        var reloadedChild = await context.Tags.FindAsync(child.Id);
        var reloadedGrand = await context.Tags.FindAsync(grand.Id);
        reloadedChild!.Path.Should().Be("root-b/moved-child");
        reloadedChild.Level.Should().Be(1);
        reloadedChild.Type.Should().Be(TagType.Income);
        reloadedChild.OwnerId.Should().Be(ownerB);
        reloadedChild.SortOrder.Should().Be(10);
        reloadedGrand!.Path.Should().Be("root-b/moved-child/grand-child");
        reloadedGrand.Level.Should().Be(2);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_ForSystemTag_AndSoftDeleteRegularTag()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerId = Guid.NewGuid();
        var user = TestDataFactory.CreateUser("tag-delete-owner@test.local", id: ownerId);
        var systemTag = TestDataFactory.CreateTag("System", TagType.Expense, ownerId, TagVisibility.Public, isSystem: true);
        var regularTag = TestDataFactory.CreateTag("Regular", TagType.Expense, ownerId, TagVisibility.Public);
        var operation = TestDataFactory.CreateOperation(ownerId, OperationType.Expense, 10m, "RUB", DateTime.UtcNow);
        context.AddRange(user, systemTag, regularTag, operation);
        await context.SaveChangesAsync();
        context.OperationTags.Add(new OperationTag
        {
            Id = Guid.NewGuid(),
            OperationId = operation.Id,
            TagId = regularTag.Id
        });
        await context.SaveChangesAsync();
        var service = new TagService(context);

        var systemResult = await service.DeleteAsync(systemTag.Id);
        var regularResult = await service.DeleteAsync(regularTag.Id);

        systemResult.Should().BeFalse();
        regularResult.Should().BeTrue();
        (await context.Tags.FindAsync(regularTag.Id))!.IsActive.Should().BeFalse();
    }
}
