namespace Hearth.Domain.ValueObjects;

/// <summary>
/// Budget execution of one budgeted category in a month (accrual basis).
/// Created only by <c>BudgetStatusCalculator</c>.
/// </summary>
public sealed record BudgetStatusItem
{
    internal BudgetStatusItem(Guid categoryId, Money budgetAmount, Money spentAmount)
    {
        CategoryId = categoryId;
        BudgetAmount = budgetAmount;
        SpentAmount = spentAmount;
    }

    public Guid CategoryId { get; }

    /// <summary>Monthly budget target active in the month. Always greater than zero.</summary>
    public Money BudgetAmount { get; }

    /// <summary>Actual accrual spending in the category during the month (zero if no spending).</summary>
    public Money SpentAmount { get; }

    /// <summary>Remaining budget. Negative when spending exceeded the target.</summary>
    public Money RemainingAmount => BudgetAmount - SpentAmount;

    public bool IsExceeded => SpentAmount > BudgetAmount;

    /// <summary>Percentage of the monthly target consumed, rounded to two decimal places.</summary>
    public decimal UsagePercentage =>
        Math.Round((SpentAmount.Amount / BudgetAmount.Amount) * 100m, 2, MidpointRounding.AwayFromZero);
}
