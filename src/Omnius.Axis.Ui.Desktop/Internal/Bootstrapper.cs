using Microsoft.Extensions.DependencyInjection;
using Omnius.Axis.Ui.Desktop.Configuration;
using Omnius.Axis.Ui.Desktop.Views.Dialogs;
using Omnius.Axis.Ui.Desktop.Views.Main;
using Omnius.Axis.Ui.Desktop.Views.Settings;
using Omnius.Core;
using Omnius.Core.Avalonia;
using Omnius.Core.Net;

namespace Omnius.Axis.Ui.Desktop.Internal;

public partial class Bootstrapper : AsyncDisposableBase
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    private string? _databaseDirectoryPath;
    private OmniAddress? _listenAddress;

    private UiStatus? _uiState;
    private IntaractorProvider? _intaractorProvider;

    private ServiceProvider? _serviceProvider;

    public static Bootstrapper Instance { get; } = new Bootstrapper();

    private const string UI_STATUS_FILE_NAME = "ui_status.json";

    private Bootstrapper()
    {
    }

    public async ValueTask BuildAsync(string databaseDirectoryPath, OmniAddress listenAddress, CancellationToken cancellationToken = default)
    {
        _databaseDirectoryPath = databaseDirectoryPath;
        _listenAddress = listenAddress;

        ArgumentNullException.ThrowIfNull(_databaseDirectoryPath);
        ArgumentNullException.ThrowIfNull(_listenAddress);

        try
        {
            _uiState = await UiStatus.LoadAsync(Path.Combine(_databaseDirectoryPath, UI_STATUS_FILE_NAME));

            var bytesPool = BytesPool.Shared;
            _intaractorProvider = new IntaractorProvider(_databaseDirectoryPath, _listenAddress, bytesPool);

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddSingleton(_uiState);

            serviceCollection.AddSingleton<IBytesPool>(bytesPool);
            serviceCollection.AddSingleton<IIntaractorProvider>(_intaractorProvider);

            serviceCollection.AddSingleton<IApplicationDispatcher, ApplicationDispatcher>();
            serviceCollection.AddSingleton<IMainWindowProvider, MainWindowProvider>();
            serviceCollection.AddSingleton<IClipboardService, ClipboardService>();
            serviceCollection.AddSingleton<IDialogService, DialogService>();
            serviceCollection.AddSingleton<INodesFetcher, NodesFetcher>();

            serviceCollection.AddTransient<MainWindowViewModel>();
            serviceCollection.AddTransient<SettingsWindowViewModel>();
            serviceCollection.AddTransient<MultiLineTextInputWindowViewModel>();
            serviceCollection.AddTransient<StatusControlViewModel>();
            serviceCollection.AddTransient<PeersControlViewModel>();
            serviceCollection.AddTransient<DownloadControlViewModel>();
            serviceCollection.AddTransient<UploadControlViewModel>();
            serviceCollection.AddTransient<SignaturesControlViewModel>();

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }
        catch (OperationCanceledException e)
        {
            _logger.Debug(e);

            throw;
        }
        catch (Exception e)
        {
            _logger.Error(e);

            throw;
        }
    }

    protected override async ValueTask OnDisposeAsync()
    {
        if (_databaseDirectoryPath is not null && _uiState is not null) await _uiState.SaveAsync(Path.Combine(_databaseDirectoryPath, UI_STATUS_FILE_NAME));
    }

    public ServiceProvider GetServiceProvider()
    {
        return _serviceProvider ?? throw new NullReferenceException();
    }
}
