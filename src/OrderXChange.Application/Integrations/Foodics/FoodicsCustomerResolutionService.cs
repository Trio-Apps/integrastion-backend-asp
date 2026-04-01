using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Foodics;

public class FoodicsCustomerResolutionService : ITransientDependency
{
    private readonly IConfiguration _configuration;
    private readonly FoodicsCustomerClient _foodicsCustomerClient;
    private readonly ILogger<FoodicsCustomerResolutionService> _logger;

    public FoodicsCustomerResolutionService(
        IConfiguration configuration,
        FoodicsCustomerClient foodicsCustomerClient,
        ILogger<FoodicsCustomerResolutionService> logger)
    {
        _configuration = configuration;
        _foodicsCustomerClient = foodicsCustomerClient;
        _logger = logger;
    }

    public async Task<FoodicsOrderCustomerResolution> ResolveAsync(
        TalabatOrderWebhook webhook,
        string vendorCode,
        string accessToken,
        Guid? foodicsAccountId = null,
        CancellationToken cancellationToken = default)
    {
        if (ShouldUseDefaultCustomer(webhook))
        {
            var defaultCustomerId = _configuration["Foodics:DefaultCustomerId"]?.Trim();
            if (string.IsNullOrWhiteSpace(defaultCustomerId))
            {
                _logger.LogWarning(
                    "Talabat order requires default Foodics customer but Foodics:DefaultCustomerId is not configured. VendorCode={VendorCode}, OrderCode={OrderCode}",
                    vendorCode,
                    webhook.Code);

                return FoodicsOrderCustomerResolution.Empty("default_customer_missing");
            }

            return new FoodicsOrderCustomerResolution(defaultCustomerId, null, "default_customer");
        }

        if (!ShouldCreateDeliveryCustomer(webhook))
        {
            return FoodicsOrderCustomerResolution.Empty("no_customer_assignment");
        }

        var customerRequest = BuildCustomerRequest(webhook);
        var customer = await _foodicsCustomerClient.CreateCustomerAsync(
            customerRequest,
            accessToken,
            foodicsAccountId,
            cancellationToken);

        var customerId = customer?.Id?.Trim();
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new InvalidOperationException("Foodics customer creation succeeded without returning customer id.");
        }

        var addressRequest = BuildAddressRequest(webhook, customerId);
        var address = await _foodicsCustomerClient.CreateAddressAsync(
            addressRequest,
            accessToken,
            foodicsAccountId,
            cancellationToken);

        var addressId = address?.Id?.Trim();
        if (string.IsNullOrWhiteSpace(addressId))
        {
            throw new InvalidOperationException("Foodics address creation succeeded without returning address id.");
        }

        return new FoodicsOrderCustomerResolution(customerId, addressId, "vendor_delivery_customer");
    }

    private static bool ShouldUseDefaultCustomer(TalabatOrderWebhook webhook)
    {
        if (ShouldCreateDeliveryCustomer(webhook))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldCreateDeliveryCustomer(TalabatOrderWebhook webhook)
    {
        if (!string.Equals(webhook.ExpeditionType, "delivery", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (webhook.Delivery?.RiderPickupTime.HasValue == true)
        {
            return false;
        }

        return HasAddressData(webhook.Delivery?.Address);
    }

    private static bool HasAddressData(TalabatOrderAddress? address)
    {
        if (address == null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(address.Line1)
               || !string.IsNullOrWhiteSpace(address.Line2)
               || !string.IsNullOrWhiteSpace(address.Line3)
               || !string.IsNullOrWhiteSpace(address.Line4)
               || !string.IsNullOrWhiteSpace(address.Line5)
               || !string.IsNullOrWhiteSpace(address.Street)
               || !string.IsNullOrWhiteSpace(address.Number)
               || !string.IsNullOrWhiteSpace(address.City)
               || !string.IsNullOrWhiteSpace(address.District)
               || !string.IsNullOrWhiteSpace(address.Other)
               || !string.IsNullOrWhiteSpace(address.Postcode)
               || address.Latitude.HasValue
               || address.Longitude.HasValue;
    }

    private static FoodicsCustomerCreateRequest BuildCustomerRequest(TalabatOrderWebhook webhook)
    {
        var (dialCode, phone) = ExtractPhoneParts(webhook.Customer?.MobilePhone, webhook.Customer?.MobilePhoneCountryCode);
        var name = string.Join(" ", new[] { webhook.Customer?.FirstName, webhook.Customer?.LastName }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim()));

        if (string.IsNullOrWhiteSpace(name))
        {
            name = !string.IsNullOrWhiteSpace(phone)
                ? $"Talabat Customer {phone}"
                : $"Talabat Customer {webhook.Code ?? webhook.Token ?? Guid.NewGuid().ToString("N")}";
        }

        return new FoodicsCustomerCreateRequest
        {
            Name = name,
            DialCode = dialCode,
            Phone = phone,
            Email = NormalizeEmptyToNull(webhook.Customer?.Email)
        };
    }

    private static FoodicsCustomerAddressCreateRequest BuildAddressRequest(TalabatOrderWebhook webhook, string customerId)
    {
        var address = webhook.Delivery?.Address;
        var city = NormalizeEmptyToNull(address?.City);
        var label = city ?? "Talabat Address";
        var description = BuildAddressDescription(address);

        return new FoodicsCustomerAddressCreateRequest
        {
            Name = label,
            Description = description,
            Latitude = address?.Latitude?.ToString(CultureInfo.InvariantCulture),
            Longitude = address?.Longitude?.ToString(CultureInfo.InvariantCulture),
            CustomerId = customerId
        };
    }

    private static string BuildAddressDescription(TalabatOrderAddress? address)
    {
        if (address == null)
        {
            return "Talabat delivery address";
        }

        var parts = new[]
        {
            address.Line1,
            address.Line2,
            address.Line3,
            address.Line4,
            address.Line5,
            address.Building,
            address.FlatNumber,
            address.Floor,
            address.Street,
            address.Number,
            address.District,
            address.City,
            address.Postcode,
            address.DeliveryInstructions,
            address.Other
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x!.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        if (parts.Count == 0)
        {
            return "Talabat delivery address";
        }

        var builder = new StringBuilder();
        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(parts[i]);
        }

        return builder.ToString();
    }

    private static (int? DialCode, string? Phone) ExtractPhoneParts(string? mobilePhone, string? mobilePhoneCountryCode)
    {
        var dialCodeDigits = DigitsOnly(mobilePhoneCountryCode);
        var phoneDigits = DigitsOnly(mobilePhone);

        if (string.IsNullOrWhiteSpace(phoneDigits))
        {
            return (TryParseNullableInt(dialCodeDigits), null);
        }

        if (!string.IsNullOrWhiteSpace(dialCodeDigits) && phoneDigits.StartsWith(dialCodeDigits, StringComparison.Ordinal))
        {
            var localPhone = phoneDigits[dialCodeDigits.Length..];
            return (TryParseNullableInt(dialCodeDigits), string.IsNullOrWhiteSpace(localPhone) ? phoneDigits : localPhone);
        }

        return (TryParseNullableInt(dialCodeDigits), phoneDigits);
    }

    private static int? TryParseNullableInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? DigitsOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private static string? NormalizeEmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record FoodicsOrderCustomerResolution(
    string? CustomerId,
    string? CustomerAddressId,
    string Mode)
{
    public static FoodicsOrderCustomerResolution Empty(string mode)
    {
        return new FoodicsOrderCustomerResolution(null, null, mode);
    }
}
