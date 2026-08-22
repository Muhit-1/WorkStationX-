using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using WorkStationX.Data;
using WorkStationX.Infrastructure;
using WorkStationX.Services;
using WorkStationX.ViewModels;
using WorkStationX.Views;

namespace WorkStationX;

public partial class App : Application
{
    // Two instances writing the same SQLite file corrupts it, and two schedulers
    // would double-fire every reminder. One instance only.
    private const string SingleInstanceMutexName = "Global\\WorkStationX.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "WorkStationX is already running. Check the system tray.",
                "WorkStationX",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        AppPaths.EnsureCreated();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                AppPaths.LogFile,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        BindingErrorListener.Attach();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled domain exception");

        try
        {
            _host = BuildHost();
            await _host.StartAsync();

            // Apply migrations on the user's machine, not just the dev's.
            await using (var db = await _host.Services
                             .GetRequiredService<IDbContextFactory<AppDbContext>>()
                             .CreateDbContextAsync())
            {
                await db.Database.MigrateAsync();
            }

            // Restore the saved colourway before the window is shown, so the user
            // never sees a flash of the default palette.
            var settings = _host.Services.GetRequiredService<ISettingsService>();
            _host.Services.GetRequiredService<IThemeService>().Apply(settings.Current.ThemeId);

            var shell = _host.Services.GetRequiredService<ShellViewModel>();
            var window = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();

            // Hotkeys need a live window handle, so they attach after the window exists.
            var hotkeys = _host.Services.GetRequiredService<IHotkeyService>();
            var tools = _host.Services.GetRequiredService<ToolsViewModel>();

            hotkeys.Attach(window);
            hotkeys.Triggered += (_, action) =>
            {
                if (action == HotkeyAction.ShowApp)
                {
                    window.Show();
                    window.WindowState = WindowState.Normal;
                    window.Activate();
                    return;
                }

                tools.Invoke(action);
            };
            hotkeys.Rebind(HotkeyDefaults.Load(settings.Current));

            await shell.InitializeAsync();

            Log.Information("WorkStationX started");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup failed");
            MessageBox.Show(
                $"WorkStationX failed to start.\n\n{ex.Message}\n\nSee the log at:\n{AppPaths.LogDirectory}",
                "WorkStationX",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                // A factory, not a scoped context: the view-models are long-lived
                // singletons, so injecting a scoped DbContext would capture one for
                // the life of the app. Each operation opens and disposes its own.
                services.AddDbContextFactory<AppDbContext>(options =>
                    options.UseSqlite($"Data Source={AppPaths.DatabaseFile}"));

                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IThemeService>(_ => new ThemeService(Current));
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<ILauncherService, LauncherService>();
                services.AddSingleton<IChromeProfileService, ChromeProfileService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<IWindowPinService, WindowPinService>();
                services.AddSingleton<IColorPickService, ColorPickService>();
                services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
                services.AddSingleton<IHotkeyService, HotkeyService>();
                services.AddSingleton<ToolsViewModel>();
                services.AddHostedService<ReminderHostedService>();

                services.AddSingleton<WorkspaceBayViewModel>();
                services.AddSingleton<TaskBayViewModel>();
                services.AddSingleton<TimeBankBayViewModel>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<HistoryViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<ShellViewModel>();

                services.AddSingleton<MainWindow>();
            })
            .Build();

    private static void OnDispatcherUnhandledException(
        object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");
        MessageBox.Show(
            $"Something went wrong.\n\n{e.Exception.Message}",
            "WorkStationX",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // Release every pin: a window left topmost after we exit cannot be
        // un-stuck by the user without restarting that app.
        _host?.Services.GetService<IWindowPinService>()?.UnpinAll();

        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        Log.Information("WorkStationX exited");
        await Log.CloseAndFlushAsync();

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }
}
