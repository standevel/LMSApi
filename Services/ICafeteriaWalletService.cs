using System.Threading;
using System.Threading.Tasks;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface ICafeteriaWalletService
{
    Task<CafeteriaWalletBalanceResponse> GetBalanceAsync(string username, CancellationToken ct = default);
    Task<InitializeWalletTopUpResponse> InitializeTopUpAsync(InitializeWalletTopUpRequest req, CancellationToken ct = default);
    Task<VerifyWalletTopUpResponse> VerifyPaymentAsync(VerifyWalletTopUpRequest req, CancellationToken ct = default);
    Task<VerifyWalletTopUpResponse> DirectTopUpAsync(DirectWalletTopUpRequest req, CancellationToken ct = default);
    Task HandlePaystackWebhookAsync(string rawBody, string signature, CancellationToken ct = default);
    Task HandleHydrogenWebhookAsync(string rawBody, string signature, CancellationToken ct = default);
}
