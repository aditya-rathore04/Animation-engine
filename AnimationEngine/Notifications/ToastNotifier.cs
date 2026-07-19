using System;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;

namespace AnimationEngine.Notifications;

public static class ToastNotifier
{
    /// <summary>
    /// Fires a native Windows Toast notification to the Action Center.
    /// Fails silently and logs to Console/Debug if OS notifications are blocked.
    /// </summary>
    public static void ShowToast(string message)
    {
        try
        {
            // ToastText02 is a template with a bold title (line 1) and a body (line 2)
            ToastTemplateType templateType = ToastTemplateType.ToastText02;
            XmlDocument xmlContent = ToastNotificationManager.GetTemplateContent(templateType);
            
            XmlNodeList textNodes = xmlContent.GetElementsByTagName("text");
            if (textNodes != null && textNodes.Length >= 2)
            {
                textNodes[0].AppendChild(xmlContent.CreateTextNode("Animation Engine"));
                textNodes[1].AppendChild(xmlContent.CreateTextNode(message));
            }
            
            ToastNotification notification = new ToastNotification(xmlContent);
            
            // Show toast using AppUserModelID "AnimationEngine"
            var notifier = ToastNotificationManager.CreateToastNotifier("AnimationEngine");
            notifier.Show(notification);
        }
        catch (Exception ex)
        {
            // Fail silently as required in the build guide to prevent crash if disabled globally
            Console.WriteLine($"[ToastNotifier] Failed to send toast notification: {ex.Message}");
        }
    }
}
