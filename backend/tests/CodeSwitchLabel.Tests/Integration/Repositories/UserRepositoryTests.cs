using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;

namespace CodeSwitchLabel.Tests.Integration.Repositories;

/// <summary>Integration tests for UserRepository.</summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public class UserRepositoryTests : IntegrationTestBase
{
    public UserRepositoryTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsUser_WhenUserExists()
    {
        // Act
        var user = await Users.GetByEmailAsync("admin@test.local", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(user);
        Assert.Equal("admin@test.local", user.Email);
        Assert.Equal(RoleName.Admin, user.Role.RoleName);
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        // Act
        var user = await Users.GetByEmailAsync("nonexistent@test.local", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task GetForUpdateAsync_ReturnsUserWithRole_WhenUserExists()
    {
        // Act
        var user = await Users.GetForUpdateAsync(AdminUserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(user);
        Assert.NotNull(user.Role);
        Assert.Equal(RoleName.Admin, user.Role.RoleName);
    }

    [Fact]
    public async Task CreateUser_PersistsUser()
    {
        // Arrange
        var email = NewUniqueEmail("newuser");

        // Act
        var user = await CreateUserAsync(email, RoleName.Speaker, "New User");

        // Assert
        Assert.NotEqual(0, user.UserId);
        Assert.Equal(email, user.Email);

        var fetched = await Users.GetByEmailAsync(email, TestContext.Current.CancellationToken);
        Assert.NotNull(fetched);
        Assert.Equal(email, fetched.Email);
    }

    [Fact]
    public async Task CreateUser_WithBuilder_PersistsUser()
    {
        // Arrange + Act — cùng ca trên nhưng qua TestDataBuilders để khóa quy ước dùng builder.
        var email = NewUniqueEmail("builder");
        var user = await CreateUserWithBuilderAsync(RoleName.Speaker, email, "Builder User");

        // Assert
        Assert.NotEqual(0, user.UserId);
        var fetched = await Users.GetByEmailAsync(email, TestContext.Current.CancellationToken);
        Assert.NotNull(fetched);
    }

    [Fact]
    public async Task SearchAsync_ReturnsUsers_WhenUsersExist()
    {
        // Act
        var (items, total) = await Users.SearchAsync(null, null, null, 1, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(total >= 4); // 4 seeded users
    }

    [Fact]
    public async Task GetByRoleAsync_ReturnsUsersWithRole()
    {
        // Act
        var reviewers = await Users.GetByRoleAsync(RoleName.Reviewer, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(reviewers);
        Assert.All(reviewers, u => Assert.Equal(RoleName.Reviewer, u.Role.RoleName));
    }

    [Fact]
    public async Task GetOpenTasksAsync_ReturnsEmpty_WhenNoTasks()
    {
        // Act
        var tasks = await Users.GetOpenTasksAsync(SpeakerUserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(tasks);
        Assert.Empty(tasks);
    }
}
