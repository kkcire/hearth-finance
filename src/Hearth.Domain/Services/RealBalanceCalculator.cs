using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;

namespace Hearth.Domain.Services;

/// <summary>
/// Calculates the cash balance of an account: each item counts on the day the money moves,
/// not on the month it refers to. Always starts from the account's single manual opening balance
/// (<see cref="Account.InitialBalance"/>) on <see cref="Account.TrackingStartDate"/> (Day 1)
/// and applies every realized cash movement from Day 1 through <c>until</c>.
/// </summary>
public static class RealBalanceCalculator
{
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
                .Where(expense => expense.AccountId == account.Id
                                  && CountsIn(account, expense.Date, until))
                .Select(expense => expense.Amount));

        // A fixed expense is paid month by month: each payment carries its competence month,
        // and the cash that leaves is the amount of that month.
        var paidFixedExpenses = Money.Sum(
            fixedExpenses
                .Where(expense => expense.AccountId == account.Id)
                .SelectMany(expense => expense.GetPaymentEntries().Select(payment => (Expense: expense, Payment: payment)))
                .Where(item => CountsIn(account, item.Payment.PaidOn, until))
                .Select(item => item.Expense.AmountIn(item.Payment.Month)
                                ?? throw new InvalidOperationException(
                                    "A paid fixed expense has no amount in the month it was paid.")));

        // Credit card invoices settle automatically on their cycle's DueDate, including purchases
        // from archived cards so historical cash balance never changes.
        var paidInvoices = Money.Sum(
            purchases
                .Where(purchase => purchase.AccountId == account.Id)
                .SelectMany(purchase => purchase.GetTrackedInstallments(account.TrackingStartDate))
                .Where(installment => CountsIn(account, installment.Cycle.DueDate, until))
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
