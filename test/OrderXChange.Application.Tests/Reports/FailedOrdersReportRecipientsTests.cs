using OrderXChange.Reports;
using Xunit;

namespace OrderXChange.Application.Reports;

public class FailedOrdersReportRecipientsTests
{
    [Theory]
    [InlineData("a@x.com, b@y.com")]
    [InlineData("a@x.com,b@y.com")]
    [InlineData("a@x.com; b@y.com")]
    [InlineData(" a@x.com \n b@y.com ")]
    [InlineData("a@x.com, b@y.com, A@X.com")]
    public void Splits_on_commas_semicolons_and_whitespace_and_drops_duplicates(string value)
    {
        Assert.Equal(new[] { "a@x.com", "b@y.com" }, FailedOrdersReportService.SplitRecipients(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ; ")]
    public void Empty_value_has_no_recipients(string? value)
    {
        Assert.Empty(FailedOrdersReportService.SplitRecipients(value));
    }
}
