namespace OrderXChange.Settings;

public static class OrderXChangeSettings
{
    private const string Prefix = "OrderXChange";

    public const string TalabatActivePaymentMethodId = Prefix + ".Talabat.ActivePaymentMethodId";
    public const string TalabatActiveDeliveryChargeId = Prefix + ".Talabat.ActiveDeliveryChargeId";

    /// <summary>
    /// Local time of day ("HH:mm") at which the business day ends / a new day begins.
    /// "Out of stock for a day" items are auto-restored at the next occurrence of this time.
    /// </summary>
    public const string AvailabilityDayEndTime = Prefix + ".Availability.DayEndTime";

    /// <summary>
    /// Timezone id (e.g. "Asia/Kuwait") used to interpret <see cref="AvailabilityDayEndTime"/>.
    /// </summary>
    public const string AvailabilityTimeZone = Prefix + ".Availability.TimeZone";
}
