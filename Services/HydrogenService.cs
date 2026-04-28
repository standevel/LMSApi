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

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/bepay/api/v1/merchant/initiate-payment")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        var url = data.GetProperty("authorizationUrl").GetString() ?? "";
        var txRef = data.GetProperty("transactionRef").GetString() ?? "";
        return (url, txRef);
    }

    /// <summary>Verify HMAC-SHA256 signature from Hydrogen webhook.</summary>
    public bool VerifySignature(string rawBody, string signature)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        return computed == signature?.ToLowerInvariant();
    }
}
