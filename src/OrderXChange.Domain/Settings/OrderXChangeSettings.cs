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
    /// Also used as the tenant's local timezone for the daily failed-orders report.
    /// </summary>
    public const string AvailabilityTimeZone = Prefix + ".Availability.TimeZone";

    /// <summary>
    /// Email address that receives the end-of-day report of orders that stayed Failed after
    /// all retries. Empty disables the report for the tenant.
    /// </summary>
    public const string FailedOrdersReportEmail = Prefix + ".Reports.FailedOrdersEmail";

    /// <summary>Local time of day ("HH:mm") the daily failed-orders report is sent.</summary>
    public const string FailedOrdersReportTime = Prefix + ".Reports.FailedOrdersTime";

    /// <summary>Internal: business date ("yyyy-MM-dd") the report was last sent, to avoid duplicates.</summary>
    public const string FailedOrdersReportLastSentDate = Prefix + ".Reports.FailedOrdersLastSentDate";
}
