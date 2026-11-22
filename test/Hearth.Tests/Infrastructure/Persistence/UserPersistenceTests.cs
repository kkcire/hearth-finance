using Hearth.Application.Abstractions;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hearth.Tests.Infrastructure.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class UserPersistenceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public UserPersistenceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UserRepository_PersistsQueriesUpdatesAndEnforcesUniqueEmail()
    {
        var userId = Guid.CreateVersion7();
        var createdAt = new DateTimeOffset(2026, 11, 22, 14, 30, 0, TimeSpan.Zero);

        await using (var scope = _fixture.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var user = User.Create(userId, "Erick", "Erick@Hearth.App", hasher.Hash("segredo123"), createdAt);
            await users.AddAsync(user);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            Assert.True(await users.ExistsWithEmailAsync("erick@hearth.app"));
            Assert.False(await users.ExistsWithEmailAsync("outro@hearth.app"));

            var byEmail = await users.GetByEmailAsync("erick@hearth.app");
            Assert.NotNull(byEmail);
            Assert.Equal(userId, byEmail.Id);
            Assert.Equal("Erick", byEmail.Name);
            Assert.Equal(createdAt, byEmail.CreatedAtUtc);
            Assert.Equal(PasswordVerificationOutcome.Success, hasher.Verify(byEmail.PasswordHash, "segredo123"));

            byEmail.UpdatePasswordHash(hasher.Hash("novasenha456"));
            await users.UpdateAsync(byEmail);
            await uow.SaveChangesAsync();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var reloaded = await users.GetByIdAsync(userId);
            Assert.NotNull(reloaded);
            Assert.Equal(PasswordVerificationOutcome.Success, hasher.Verify(reloaded.PasswordHash, "novasenha456"));
        }

        await using (var scope = _fixture.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await users.AddAsync(User.Create(Guid.CreateVersion7(), "Outro", "ERICK@HEARTH.APP", "hash", createdAt));
            await Assert.ThrowsAsync<DbUpdateException>(() => uow.SaveChangesAsync());
        }
    }
}
