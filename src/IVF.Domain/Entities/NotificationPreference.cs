using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Notification preferences per user — controls which security events trigger notifications.
/// </summary>
public class NotificationPreference : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Channel { get; private set; } = "in_app"; // in_app, email, sms, push
    public string EventTypes { get; private set; } = "[]"; // JSON: ["new_device_login","password_changed","mfa_changed","failed_login_attempts"]
    public bool IsEnabled { get; private set; } = true;

    private NotificationPreference() { }

    public static NotificationPreference Create(
        Guid userId,
        string channel,
        string eventTypes)
    {
        return new NotificationPreference
        {
            UserId = userId,
            Channel = channel,
            EventTypes = eventTypes
        };
    }

    public void Update(string eventTypes, bool isEnabled)
    {
        EventTypes = eventTypes;
        IsEnabled = isEnabled;
        SetUpdated();
    }
}
