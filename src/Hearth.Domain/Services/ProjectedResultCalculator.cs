using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Services;

/// <summary>
/// Calculates the projected result of a month, by accrual: it forecasts the month regardless of
/// when the money actually moves. The real balance, by cash, is a separate calculation.
/// </summary>
public static class ProjectedResultCalculator
{
    /// <summary>
    /// Projected result = incomes (salary included) − calculated invoice − direct expenses − fixed expenses.
    /// Only items of the given account are considered.
    /// </summary>
    /// <remarks>
    /// Incomes count by their expected date, even if they are not received yet. The invoice is the
    /// one of the cycle closing in the month, calculated from the installments, never the amount
    /// charged by the bank. Direct expenses count by their date. Fixed expenses count when active in
    /// the month, at the amount of that month, whether or not they were paid.
    /// </remarks>
    public static ProjectedResult Calculate(
        Guid accountId,
        YearMonth month,
        IEnumerable<Income> incomes,
        IEnumerable<DirectExpense> directExpenses,
        IEnumerable<FixedExpense> fixedExpenses,
        IEnumerable<CreditCardPurchase> purchases)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("The account id must not be empty.", nameof(accountId));
        }

        ArgumentNullException.ThrowIfNull(month);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);

        var incomesTotal = Money.Sum(
            incomes
                .Where(income => income.AccountId == accountId && income.ExpectedMonth == month)
                .Select(income => income.Amount));

        var directExpensesTotal = Money.Sum(
            directExpenses
                .Where(expense => expense.AccountId == accountId && expense.Month == month)
                .Select(expense => expense.Amount));

        var fixedExpensesTotal = Money.Sum(
            fixedExpenses
                .Where(expense => expense.AccountId == accountId)
                .Select(expense => expense.AmountIn(month))
                .Where(amount => amount.HasValue)
                .Select(amount => amount!.Value));

        var invoice = InvoiceCalculator.Calculate(accountId, month, purchases);

        return new ProjectedResult(month, incomesTotal, invoice, directExpensesTotal, fixedExpensesTotal);
    }
}