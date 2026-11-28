namespace Hearth.Application.FixedIncomes.ListFixedIncomes;

public sealed record ListFixedIncomesQuery(
    Guid AccountId,
    int? Year = null,
    int? Month = null);
