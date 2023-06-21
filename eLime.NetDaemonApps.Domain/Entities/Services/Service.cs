using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.Entities.Services;

public class Service
{
    private readonly IHaContext _haContext;
    public Service(IHaContext haContext)
    {
        _haContext = haContext;
    }

    /// <summary>Sends a notification message using the eugene service.</summary>
    /// <param name="message">Message body of the notification. eg: The garage door has been open for 10 minutes.</param>
    /// <param name="phone">which phone or group of phones to notify</param>
    /// <param name="title">Title for your notification. eg: Your Garage Door Friend</param>
    /// <param name="target">An array of targets to send the notification to. Optional depending on the platform. eg: platform specific</param>
    /// <param name="data">Extended information for notification. Optional depending on the platform. eg: platform specific</param>
    public void NotifyPhone(string phone, string @message, string? @title = null, object? @target = null, NotifyPhoneData? @data = null)
    {
        _haContext.CallService("notify", phone, null, new NotifyPhoneParameters { Message = @message, Title = @title, Target = @target, Data = @data });
    }
    public void NotifyPhone(string phone, string message, string? title, string channel)
    {
        var data = new NotifyPhoneData
        {
            Channel = channel,
            Importance = "high",
            Visibility = "public",
            Ttl = 0,
            Priority = "high"
        };

        var parameters = new NotifyPhoneParameters { Message = @message, Title = @title, Target = null, Data = data };
        _haContext.CallService("notify", phone, null, parameters);
    }
    public void NotifyPhone(string phone, NotifyPhoneParameters parameters)
    {
        _haContext.CallService("notify", phone, null, parameters);
    }
}