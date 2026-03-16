using FluentAssertions;
using Gibag.Modules.Core.Application.Auth.Login;
using Gibag.Modules.Core.Application.Interfaces;
using Gibag.Modules.Core.Domain;
using Gibag.Modules.Core.Infrastructure;
using Gibag.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gibag.Modules.Core.Tests.Application;

public class LoginCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithInvalidPassword_ShouldReturnFailureResult()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantServiceMock = new Mock<ITenantService>();
        tenantServiceMock.Setup(x => x.CurrentTenantId).Returns(tenantId);

        var jwtProviderMock = new Mock<IJwtProvider>();

        // We need a valid hashed password to test failure against a wrong plain password
        string correctPasswordPlain = "ValidPassword123!";
        string correctPasswordHash = BCrypt.Net.BCrypt.HashPassword(correctPasswordPlain);

        using (var context = new CoreDbContext(options, tenantServiceMock.Object))
        {
            var branch = new Branch(tenantId, "Central", "Main Street", "America/Bogota");
            context.Branches.Add(branch);

            var role = new Role(tenantId, "Admin", "[]");
            context.Roles.Add(role);
            var user = new User(tenantId, role.Id, "test@test.com", correctPasswordHash, "Test", "User");
            context.Users.Add(user);

            var userBranch = new UserBranch(tenantId, user.Id, branch.Id, true);
            context.UserBranches.Add(userBranch);

            await context.SaveChangesAsync();
        }

        using (var context = new CoreDbContext(options, tenantServiceMock.Object))
        {
            var handler = new LoginCommandHandler(context, jwtProviderMock.Object);
            var command = new LoginCommand("test@test.com", "WrongPassword456!");

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.ErrorCode.Should().Be("Auth.InvalidCredentials");
        }
    }
}
