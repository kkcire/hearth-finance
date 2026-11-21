using Hearth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hearth.Infrastructure.Persistence.Converters;

public sealed class MoneyConverter : ValueConverter<Money, decimal>
{
    public MoneyConverter()
        : base(
            money => money.Amount,
            value => Money.FromDecimal(value))
    {
    }
}
