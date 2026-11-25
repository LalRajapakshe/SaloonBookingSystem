using SalonBooking.Domain.Enums;

namespace SalonBooking.Domain.Billing;

public static class Money
{
    public static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal RequireScale(decimal value, string name)
    {
        var rounded = Round(value);
        if (rounded != value)
        {
            throw new ArgumentOutOfRangeException(name, "Use at most two decimal places.");
        }

        return rounded;
    }

    public static decimal LineNet(int quantity, decimal unitPrice, decimal discount)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (unitPrice < 0 || discount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice));
        }

        var net = Round((quantity * unitPrice) - discount);
        if (net < 0)
        {
            throw new InvalidOperationException("A line total cannot be negative.");
        }

        return net;
    }

    public static decimal TaxAmount(decimal net, decimal ratePercent)
    {
        if (ratePercent < 0 || ratePercent > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(ratePercent));
        }

        return Round(net * ratePercent / 100m);
    }

    public static PaymentStatus PaymentState(decimal total, decimal netCollected, bool finalized)
    {
        if (!finalized)
        {
            return PaymentStatus.Unpaid;
        }

        if (netCollected < 0)
        {
            throw new InvalidOperationException("Collected amount cannot be negative.");
        }

        if (total <= 0)
        {
            return netCollected == 0 ? PaymentStatus.Paid : PaymentStatus.Unpaid;
        }

        if (netCollected == 0)
        {
            return PaymentStatus.Unpaid;
        }

        if (netCollected < total)
        {
            return PaymentStatus.PartiallyPaid;
        }

        return PaymentStatus.Paid;
    }

    public static decimal Outstanding(decimal total, decimal netCollected)
    {
        var balance = Round(total - netCollected);
        return balance < 0 ? 0 : balance;
    }
}
