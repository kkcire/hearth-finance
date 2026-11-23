using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Services;

/// <summary>
/// Calculates the cash balance of an account: each item counts on the day the money moves,
/// not on the month it refers to. The projected result, by accrual, is a separate calculation.
/// </summary>
public static class RealBalanceCalculator
{
    /// <summary>
    /// Cash balance on <paramref name="until"/> (inclusive) = initial balance + received incomes
    /// − direct expenses − paid fixed expenses − credit card installments whose invoice due date
    /// has arrived on or before <paramref name="until"/>.
    /// </summary>
    /// <remarks>
    /// Items dated before the account's initial balance date are ignored, because they are already
    /// inside the initial balance. Items dated on that day count. Only items of the given account are
    /// considered. Credit card installments settle automatically on their cycle's <see cref="BillingCycle.DueDate"/>
    /// (symmetric with <see cref="CreditCardLimitCalculator"/>, where <c>DueDate &gt; today</c> occupies limit).
    /// </remarks>
    public static RealBalance Calculate(
        Account account,
        DateOnly until,
        IEnumerable<Income> incomes,
        IEnumerable<DirectExpense> directExpenses,
        IEnumerable<FixedExpense> fixedExpenses,
        IEnumerable<CreditCardPurchase> purchases)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(directExpenses);
        ArgumentNullException.ThrowIfNull(fixedExpenses);
        ArgumentNullException.ThrowIfNull(purchases);

        var receivedIncomes = Money.Sum(
            incomes
                .Where(income => income.AccountId == account.Id
                                 && income.ReceivedOn is { } received
                                 && CountsIn(account, received, until))
                .Select(income => income.Amount));

        var directTotal = Money.Sum(
            directExpenses
                .Where(expense => expense.AccountId == account.Id && CountsIn(account, expense.Date, until))
                .Select(expense => expense.Amount));

        // A fixed expense is paid month by month: each payment carries its competence month,
        // and the cash that leaves is the amount of that month.
        var paidFixedExpenses = Money.Sum(
            fixedExpenses
                .Where(expense => expense.AccountId == account.Id)
                .SelectMany(expense => expense.Payments.Select(payment => (Expense: expense, payment)))
                .Where(item => CountsIn(account, item.payment.Value, until))
                .Select(item => item.Expense.AmountIn(item.payment.Key)
                                ?? throw new InvalidOperationException(
                                    "A paid fixed expense has no amount in the month it was paid.")));

        // Credit card invoices settle automatically on their cycle's DueDate, including purchases
        // from archived cards so historical cash balance never changes.
        var paidInvoices = Money.Sum(
            purchases
                .Where(purchase => purchase.AccountId == account.Id)
                .SelectMany(purchase => purchase.GetInstallments())
                .Where(installment => !installment.IsPreviouslyPaid
                                      && CountsIn(account, installment.Cycle.DueDate, until))
                .Select(installment => installment.Amount));

        return new RealBalance(
            until,
            account.InitialBalance,
            receivedIncomes,
            directTotal,
            paidFixedExpenses,
            paidInvoices);
    }

    private static bool CountsIn(Account account, DateOnly date, DateOnly until) =>
        account.CanRegisterOn(date) && date <= until;
}
