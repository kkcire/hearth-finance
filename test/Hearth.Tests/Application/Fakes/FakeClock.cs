using Hearth.Application.Abstractions;

namespace Hearth.Tests.Application.Fakes;

internal sealed class FakeClock : IClock
{
    public FakeClock(DateOnly today)
    {
        Today = today;
    }

    public DateOnly Today { get; set; }
}
