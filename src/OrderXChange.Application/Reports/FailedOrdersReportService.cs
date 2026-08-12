using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Domain.Staging;
using OrderXChange.Emailing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace OrderXChange.Reports;

/// <summary>
/// Builds and sends the end-of-day report of orders that stayed Failed after all retries
/// (Status = "Failed" and Attempts &gt;= the max of 3). Tenant-scoped: it queries the current
/// tenant context, so callers set the tenant before invoking it.
/// </summary>
public class FailedOrdersReportService : ITransientDependency
{
    public const int TerminalAttempts = 3;

    private readonly IRepository<TalabatOrderSyncLog, Guid> _orderRepo;
    private readonly ISmtpMailSender _mailSender;
    private readonly IClock _clock;
    private readonly ILogger<FailedOrdersReportService> _logger;

    public FailedOrdersReportService(
        IRepository<TalabatOrderSyncLog, Guid> orderRepo,
        ISmtpMailSender mailSender,
        IClock clock,
        ILogger<FailedOrdersReportService> logger)
    {
        _orderRepo = orderRepo;
        _mailSender = mailSender;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Sends the report for "today" (the tenant-local day up to now) to <paramref name="email"/>.
    /// When there are no failed orders and <paramref name="sendIfEmpty"/> is false, nothing is sent.
    /// Returns the number of failed orders included.
    /// </summary>
    public async Task<int> SendAsync(string email, TimeZoneInfo timeZone, bool sendIfEmpty)
    {
        var nowUtc = NowUtc();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var localStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(nowLocal.Date, DateTimeKind.Unspecified), timeZone);

        var query = await _orderRepo.GetQueryableAsync();
        var orders = await query.AsNoTracking()
            .Where(x => x.Status == "Failed"
                        && x.Attempts >= TerminalAttempts
                        && x.ReceivedAt >= localStartUtc
                        && x.ReceivedAt <= nowUtc)
            .OrderByDescending(x => x.ReceivedAt)
            .ToListAsync();

        if (orders.Count == 0 && !sendIfEmpty)
        {
            return 0;
        }

        var dateLabel = nowLocal.ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture);
        var subject = $"Failed orders report — {dateLabel} ({orders.Count})";
        var html = BuildHtml(orders, timeZone, dateLabel);

        await _mailSender.SendAsync(email, subject, html);

        _logger.LogInformation(
            "Failed-orders report sent to {Email}. Date={Date}, FailedCount={Count}.",
            email, dateLabel, orders.Count);

        return orders.Count;
    }

    private string BuildHtml(IReadOnlyList<TalabatOrderSyncLog> orders, TimeZoneInfo timeZone, string dateLabel)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;color:#1f2937;\">");
        sb.Append($"<h2 style=\"color:#276D64;margin:0 0 4px;\">Failed orders report</h2>");
        sb.Append($"<div style=\"color:#6b7280;font-size:13px;margin-bottom:16px;\">{Enc(dateLabel)}</div>");

        if (orders.Count == 0)
        {
            sb.Append("<p style=\"font-size:14px;\">No orders remained failed after retries today. ✅</p>");
            sb.Append("</div>");
            return sb.ToString();
        }

        sb.Append($"<p style=\"font-size:14px;\">{orders.Count} order(s) stayed <strong>Failed</strong> after {TerminalAttempts} attempts:</p>");
        sb.Append("<table style=\"border-collapse:collapse;width:100%;font-size:13px;\">");
        sb.Append("<thead><tr style=\"background:#276D64;color:#fff;text-align:left;\">");
        foreach (var h in new[] { "Order ID", "Short Code", "Vendor", "Customer", "Phone", "Received", "Attempts", "Last Error" })
        {
            sb.Append($"<th style=\"padding:8px 10px;border:1px solid #e5e7eb;\">{h}</th>");
        }
        sb.Append("</tr></thead><tbody>");

        foreach (var o in orders)
        {
            var received = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(o.ReceivedAt, DateTimeKind.Utc), timeZone)
                .ToString("dd MMM, HH:mm", CultureInfo.InvariantCulture);

            sb.Append("<tr>");
            Cell(sb, o.OrderCode ?? o.ShortCode ?? o.OrderToken ?? "-");
            Cell(sb, o.ShortCode ?? "-");
            Cell(sb, o.VendorCode ?? "-");
            Cell(sb, o.CustomerName ?? "-");
            Cell(sb, o.CustomerPhone ?? "-");
            Cell(sb, received);
            Cell(sb, o.Attempts.ToString(CultureInfo.InvariantCulture));
            Cell(sb, Truncate(o.ErrorMessage, 160));
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table>");
        sb.Append("<p style=\"color:#9ca3af;font-size:12px;margin-top:16px;\">Automated report from OrderXChange.</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static void Cell(StringBuilder sb, string? value)
    {
        sb.Append($"<td style=\"padding:7px 10px;border:1px solid #e5e7eb;\">{Enc(value)}</td>");
    }

    private static string Enc(string? value)
    {
        return System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        var v = value.Trim();
        return v.Length <= max ? v : v[..max] + "…";
    }

    private DateTime NowUtc()
    {
        var now = _clock.Now;
        return now.Kind switch
        {
            DateTimeKind.Utc => now,
            DateTimeKind.Local => now.ToUniversalTime(),
            _ => DateTime.SpecifyKind(now, DateTimeKind.Utc)
        };
    }

    public static TimeZoneInfo ResolveTimeZone(string? id)
    {
        var tzId = string.IsNullOrWhiteSpace(id) ? "Asia/Kuwait" : id.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch (Exception)
        {
            if (string.Equals(tzId, "Asia/Kuwait", StringComparison.OrdinalIgnoreCase))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time"); }
                catch (Exception) { /* fall through */ }
            }

            return TimeZoneInfo.Utc;
        }
    }

    public static bool TryParseTime(string? value, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value) || !value.Contains(':'))
        {
            return false;
        }

        if (!TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var ts)
            || ts < TimeSpan.Zero || ts >= TimeSpan.FromDays(1))
        {
            return false;
        }

        time = ts;
        return true;
    }
}
