using System.IO;
using System.Media;
using System.Windows;
using Serilog;

namespace WorkStationX.Services;

public interface INotificationService
{
    void Show(string title, string message);
}

/// <summary>
/// Shows a reminder.
///
/// Deliberately a WPF window rather than a Windows toast: an unpackaged Win32 app
/// cannot raise a real toast without registering an AppUserModelID and a Start Menu
/// shortcut, and the notification then renders in the OS style rather than the
/// instrument-panel one. A custom window also means the reminder still looks like
/// WorkStationX and follows the user's chosen colourway.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly Func<Window?> _ownerProvider;

    public NotificationService() : this(() => Application.Current?.MainWindow)
    {
    }

    public NotificationService(Func<Window?> ownerProvider) => _ownerProvider = ownerProvider;

    public void Show(string title, string message)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        app.Dispatcher.Invoke(() =>
        {
            try
            {
                PlaySound();

                var window = new Views.ReminderWindow
                {
                    DataContext = new { Title = title, Message = message }
                };

                // No Owner: a reminder must be able to appear while the main window is
                // minimised to the tray, and an owned window cannot outlive that.
                window.Show();
                window.Activate();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not show reminder");
            }
        });
    }

    private static void PlaySound()
    {
        try
        {
            // Windows' own notification chime, so it sits alongside every other alert
            // the user already knows rather than shipping a louder custom one.
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Media", "Windows Notify System Generic.wav");

            if (File.Exists(path))
            {
                using var player = new SoundPlayer(path);
                player.Play();
            }
            else
            {
                SystemSounds.Asterisk.Play();
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not play reminder sound");
        }
    }
}
