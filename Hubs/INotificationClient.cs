using System.Threading.Tasks;
using LMS.Api.Contracts;

namespace LMS.Api.Hubs;

public interface INotificationClient
{
    Task ReceiveMessage(MessageDto message);
    Task ReceiveNotification(NotificationDto notification);
    Task ReceiveAnnouncement(AnnouncementDto announcement);
}
