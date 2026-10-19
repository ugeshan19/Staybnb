namespace Staybnb.Services;

public static class BookingPricing
{
    public const decimal ServiceFeeRate = 0.12m;

    public record Quote(int Nights, decimal NightlyRate, decimal Subtotal, decimal CleaningFee, decimal ServiceFee, decimal Total);

    // total = nightly rate * nights + cleaning fee + 12% service fee (on the nightly subtotal only)
    public static Quote Calculate(decimal pricePerNight, decimal cleaningFee, DateTime checkIn, DateTime checkOut)
    {
        var nights = Math.Max(0, (checkOut.Date - checkIn.Date).Days);
        var subtotal = pricePerNight * nights;
        var serviceFee = Math.Round(subtotal * ServiceFeeRate, 2);

        return new Quote(nights, pricePerNight, subtotal, cleaningFee, serviceFee, subtotal + cleaningFee + serviceFee);
    }
}
