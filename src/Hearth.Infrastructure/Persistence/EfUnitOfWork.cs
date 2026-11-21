using Hearth.Application.Abstractions;

namespace Hearth.Infrastructure.Persistence;

internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly HearthDbContext _context;

    public EfUnitOfWork(HearthDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
