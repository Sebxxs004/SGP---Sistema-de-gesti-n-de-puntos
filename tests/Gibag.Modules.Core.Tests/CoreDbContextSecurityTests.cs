using FluentAssertions;
using Gibag.Modules.Core.Domain;
using Gibag.Modules.Core.Infrastructure;
using Gibag.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gibag.Modules.Core.Tests.Infrastructure;

public class CoreDbContextSecurityTests
{
    [Fact]
    public async Task GlobalQueryFilter_ShouldOnlyReturnUsers_ForCurrentTenant()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        // Setup mock to simulate context of Tenant 1
        var tenantServiceMock = new Mock<ITenantService>();
        tenantServiceMock.Setup(x => x.CurrentTenantId).Returns(tenant1Id);

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Seed data for both tenants
        using (var seedContext = new CoreDbContext(options, tenantServiceMock.Object))
        {
            // Create role required for user
            var roleId1 = Guid.NewGuid();
            var roleId2 = Guid.NewGuid();
            
            // We use standard context saving here, TenantId is auto-assigned via interceptor based on CurrentTenantId
            // Or we can manually construct and add to memory
            var tenant1Role = new Role(tenant1Id, "Admin", "[]");
            // To seed tenant 2 bypassing the interceptor requirement of "current context", we temporarily change the mock
            
            tenantServiceMock.Setup(x => x.CurrentTenantId).Returns(tenant1Id);
            seedContext.Roles.Add(tenant1Role);
            seedContext.Users.Add(new User(tenant1Id, tenant1Role.Id, "t1@test.com", "hash", "John", "Doe"));
            seedContext.SaveChanges();

            tenantServiceMock.Setup(x => x.CurrentTenantId).Returns(tenant2Id);
            var tenant2Role = new Role(tenant2Id, "Admin", "[]");
            seedContext.Roles.Add(tenant2Role);
            seedContext.Users.Add(new User(tenant2Id, tenant2Role.Id, "t2@test.com", "hash", "Jane", "Smith"));
            seedContext.SaveChanges();
        }

        // Act - Simulate request under Tenant 1 mapping
        using (var actContext = new CoreDbContext(options, tenantServiceMock.Object))
        {
            // Set context to Tenant 1
            tenantServiceMock.Setup(x => x.CurrentTenantId).Returns(tenant1Id);
            
            var usersTenant1 = await actContext.Users.ToListAsync();
            
            // Assert
            usersTenant1.Should().HaveCount(1);
            usersTenant1.First().Email.Should().Be("t1@test.com");
            usersTenant1.First().TenantId.Should().Be(tenant1Id);
            
            // Change context to Tenant 2
            tenantServiceMock.Setup(x => x.CurrentTenantId).Returns(tenant2Id);
            
            // Re-instantiate context so filter catches the new ITenantService value evaluation correctly if scoped
            using var actContext2 = new CoreDbContext(options, tenantServiceMock.Object);
            var usersTenant2 = await actContext2.Users.ToListAsync();

            // Assert
            usersTenant2.Should().HaveCount(1);
            usersTenant2.First().Email.Should().Be("t2@test.com");
            usersTenant2.First().TenantId.Should().Be(tenant2Id);
        }
    }
}
