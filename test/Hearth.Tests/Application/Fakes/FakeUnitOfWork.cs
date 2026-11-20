using Hearth.Application.Abstractions;

namespace Hearth.Tests.Application.Fakes;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}
