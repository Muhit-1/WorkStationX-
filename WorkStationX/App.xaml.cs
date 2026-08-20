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

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled domain exception");

        try
        {
            _host = BuildHost();
            await _host.StartAsync();

            // Apply migrations on the user's machine, not just the dev's.
            using (var scope = _host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
            }

            var shell = _host.Services.GetRequiredService<ShellViewModel>();
            var window = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();

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
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite($"Data Source={AppPaths.DatabaseFile}"));

                services.AddSingleton<INavigationService, NavigationService>();

                services.AddSingleton<WorkspaceViewModel>();
                services.AddSingleton<TaskViewModel>();
                services.AddSingleton<ToolsViewModel>();
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
