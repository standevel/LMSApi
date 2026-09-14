using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace LMS.Api.Services;

public sealed class PaystackService(HttpClient httpClient, IConfiguration config)
{
    private readonly string _secretKey = config["Paystack:SecretKey"] ?? "";

    public async Task<(string AuthorizationUrl, string Reference)> InitializeTransactionAsync(
        string email, decimal amountNaira, string reference, string callbackUrl, object? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(_secretKey))
        {
            return ($"https://checkout.paystack.com/{reference}", reference);
        }

        // Paystack expects amount in kobo (1 naira = 100 kobo)
        long amountKobo = (long)(amountNaira * 100);

        var body = new
        {
            email,
            amount = amountKobo,
            reference,
            callback_url = callbackUrl,
            metadata
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.paystack.co/transaction/initialize")
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
            throw new InvalidOperationException($"Unable to connect to Paystack gateway: {ex.Message}", ex);
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!response.IsSuccessStatusCode)
        {
            string message = $"Paystack error (HTTP {(int)response.StatusCode})";
            if (doc.RootElement.TryGetProperty("message", out var mProp) && !string.IsNullOrWhiteSpace(mProp.GetString()))
            {
                message = mProp.GetString()!;
            }
            throw new InvalidOperationException(message);
        }

        if (doc.RootElement.TryGetProperty("status", out var statusProp) && !statusProp.GetBoolean())
        {
            string message = doc.RootElement.TryGetProperty("message", out var mProp)
                ? (mProp.GetString() ?? "Paystack transaction initialization rejected.")
                : "Paystack transaction initialization rejected.";
            throw new InvalidOperationException(message);
        }

        if (!doc.RootElement.TryGetProperty("data", out var data))
        {
            throw new InvalidOperationException("Invalid response received from Paystack: missing data element.");
        }

        var url = data.GetProperty("authorization_url").GetString() ?? "";
        var ref_ = data.GetProperty("reference").GetString() ?? "";
        return (url, ref_);
    }

    /// <summary>Verify transaction status with Paystack API.</summary>
    public async Task<(bool IsSuccessful, decimal AmountNaira, string Message)> VerifyTransactionAsync(string reference)
    {
        if (string.IsNullOrWhiteSpace(_secretKey))
        {
            // Demo / Dev fallback when no secret key is set
            return (true, 0m, "Demo verification passed (No Paystack:SecretKey configured).");
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.paystack.co/transaction/verify/{Uri.EscapeDataString(reference)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);

            var response = await httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            bool status = root.TryGetProperty("status", out var s) && s.GetBoolean();
            string message = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";

            if (!status || !root.TryGetProperty("data", out var data))
            {
                return (false, 0m, string.IsNullOrWhiteSpace(message) ? "Transaction verification failed" : message);
            }

            string txStatus = data.TryGetProperty("status", out var ts) ? ts.GetString() ?? "" : "";
            long amountKobo = data.TryGetProperty("amount", out var am) ? am.GetInt64() : 0;
            decimal amountNaira = (decimal)amountKobo / 100m;

            if (txStatus.Equals("success", StringComparison.OrdinalIgnoreCase))
            {
                return (true, amountNaira, "Payment verified successfully.");
            }

            return (false, amountNaira, $"Transaction status is {txStatus}.");
        }
        catch (Exception ex)
        {
            return (false, 0m, $"Verification error: {ex.Message}");
        }
    }

    /// <summary>Verify HMAC-SHA512 signature from Paystack webhook.</summary>
    public bool VerifySignature(string rawBody, string signature)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_secretKey));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        return computed == signature?.ToLowerInvariant();
    }
}
