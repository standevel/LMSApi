using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace LMS.Api.Services;

public sealed class HydrogenService(HttpClient httpClient, IConfiguration config)
{
    private readonly string _secretKey = config["Hydrogen:SecretKey"] ?? "";
    private readonly string _baseUrl = config["Hydrogen:BaseUrl"] ?? "https://api.hydrogenpay.com";

    public async Task<(string AuthorizationUrl, string TransactionRef)> InitiatePaymentAsync(
        string email, string customerName, decimal amountNaira, string description, string callbackUrl, object? meta = null)
    {
        var body = new
        {
            amount = amountNaira,
            email,
            customerName,
            currency = "NGN",
            description,
            meta,
            redirectUrl = callbackUrl
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/bepayv1/merchant/initiate-payment")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unable to connect to Hydrogen gateway: {ex.Message}", ex);
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!response.IsSuccessStatusCode)
        {
            string message = $"Hydrogen error (HTTP {(int)response.StatusCode})";
            if (doc.RootElement.TryGetProperty("message", out var mProp) && !string.IsNullOrWhiteSpace(mProp.GetString()))
            {
                message = mProp.GetString()!;
            }
            throw new InvalidOperationException(message);
        }

        if (!doc.RootElement.TryGetProperty("data", out var data))
        {
            throw new InvalidOperationException("Invalid response received from Hydrogen: missing data element.");
        }

        var url = data.GetProperty("authorizationUrl").GetString() ?? "";
        var txRef = data.GetProperty("transactionRef").GetString() ?? "";
        return (url, txRef);
    }

    /// <summary>Verify transaction status with Hydrogen API.</summary>
    public async Task<(bool IsSuccessful, decimal AmountNaira, string Message)> VerifyPaymentAsync(string transactionRef)
    {
        if (string.IsNullOrWhiteSpace(_secretKey))
        {
            return (true, 0m, "Demo verification passed (No Hydrogen:SecretKey configured).");
        }

        try
        {
            var body = new { transactionRef };
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/bepayv1/merchant/verify-payment")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);

            var response = await httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            string message = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";

            if (!status.Equals("success", StringComparison.OrdinalIgnoreCase) &&
                !status.Equals("Paid", StringComparison.OrdinalIgnoreCase) &&
                !root.TryGetProperty("data", out _))
            {
                return (false, 0m, string.IsNullOrWhiteSpace(message) ? "Hydrogen transaction verification failed." : message);
            }

            decimal amountNaira = 0m;
            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("amount", out var am))
                {
                    amountNaira = am.GetDecimal();
                }
                string paymentStatus = data.TryGetProperty("status", out var ps) ? ps.GetString() ?? "" : "";
                if (paymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase) || paymentStatus.Equals("success", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, amountNaira, "Hydrogen payment verified successfully.");
                }
                return (false, amountNaira, $"Hydrogen payment status is {paymentStatus}.");
            }

            return (true, amountNaira, "Hydrogen payment verified.");
        }
        catch (Exception ex)
        {
            return (false, 0m, $"Hydrogen verification error: {ex.Message}");
        }
    }

    /// <summary>Verify HMAC-SHA256 signature from Hydrogen webhook.</summary>
    public bool VerifySignature(string rawBody, string signature)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        return computed == signature?.ToLowerInvariant();
    }
}
