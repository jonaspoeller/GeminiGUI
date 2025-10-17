using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using GeminiGUI.Services;
using GeminiGUI.ViewModels;
using GeminiGUI.Views;

namespace GeminiGUI
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Globale Ausnahmebehandlung
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Services.ExceptionHandler.HandleException(args.ExceptionObject as Exception, "Unbehandelte Ausnahme");
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                Services.ExceptionHandler.HandleException(args.Exception, "UI-Ausnahme");
                args.Handled = true;
            };

            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    // Services
                    services.AddHttpClient();
                    services.AddSingleton<ILoggerService, LoggerService>();
                    services.AddSingleton<IDatabaseService, DatabaseService>();
                    services.AddSingleton<IGeminiService, GeminiService>();
                    services.AddSingleton<IConfigurationService, ConfigurationService>();
                    services.AddSingleton<IChatService, ChatService>();

                    // ViewModels
                    services.AddTransient<MainWindowViewModel>();
                    services.AddTransient<ChatViewModel>();
                    services.AddTransient<SettingsViewModel>();
                    
                    // Views
                    services.AddTransient<MainWindow>();
                })
                .Build();

            _host.Start();

            // Logger initialisieren
            var logger = _host.Services.GetRequiredService<ILoggerService>();
            ExceptionHandler.Initialize(logger);
            logger.LogInfo("Gemini GUI starting up");

            // Datenbank initialisieren
            var databaseService = _host.Services.GetRequiredService<IDatabaseService>();
            await databaseService.InitializeAsync();

            // API-Schlüssel laden
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var geminiService = _host.Services.GetRequiredService<IGeminiService>();
            var apiKey = await configService.GetApiKeyAsync();
            if (!string.IsNullOrEmpty(apiKey))
            {
                geminiService.SetApiKey(apiKey);
                logger.LogInfo("API key loaded from configuration");
            }
            else
            {
                logger.LogWarning("No API key found in configuration");
            }

            logger.LogInfo("Gemini GUI startup completed successfully");
            
            // MainWindow über DI erstellen und anzeigen
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                var databaseService = _host.Services.GetService<IDatabaseService>();
                if (databaseService != null)
                {
                    await databaseService.CloseAsync();
                }
                
                await _host.StopAsync();
                _host.Dispose();
            }
            base.OnExit(e);
        }

        public static T GetService<T>() where T : class
        {
            return ((App)Current)._host?.Services.GetRequiredService<T>() 
                ?? throw new InvalidOperationException($"Service {typeof(T).Name} not found");
        }
    }
}

