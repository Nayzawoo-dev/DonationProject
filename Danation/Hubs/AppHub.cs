using Microsoft.AspNetCore.SignalR;

namespace Donation.Hubs;

public class AppHub : Hub
{
    public const string AdminGroup = "Admins";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("ADMIN") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinCampaign(int campaignId)
    {
        if (campaignId > 0)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Campaign_{campaignId}");
        }
    }

    public async Task LeaveCampaign(int campaignId)
    {
        if (campaignId > 0)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Campaign_{campaignId}");
        }
    }
}
