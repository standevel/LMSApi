using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LMS.Api.Hubs;

[Authorize]
public class CafeteriaHub : Hub
{
    public async Task JoinVendorStation(string vendorId)
    {
        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Vendor_{vendorId}");
        }
    }

    public async Task LeaveVendorStation(string vendorId)
    {
        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Vendor_{vendorId}");
        }
    }
}
