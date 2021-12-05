using System.Reactive.Disposables;
using Avalonia.Controls;
using Omnius.Core;
using Omnius.Core.Avalonia;
using Omnius.Xeus.Ui.Desktop.Configuration;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;

namespace Omnius.Xeus.Ui.Desktop.Windows;

public class TextWindowViewModel : AsyncDisposableBase
{
    private readonly UiStatus _uiState;
    private readonly IClipboardService _clipboardService;

    private readonly CompositeDisposable _disposable = new();

    public TextWindowViewModel(UiStatus uiState, IClipboardService clipboardService)
    {
        _uiState = uiState;
        _clipboardService = clipboardService;

        this.Text = new ReactivePropertySlim<string>().AddTo(_disposable);
        this.OkCommand = new ReactiveCommand().AddTo(_disposable);
        this.OkCommand.Subscribe((state) => this.Ok(state)).AddTo(_disposable);
        this.CancelCommand = new ReactiveCommand().AddTo(_disposable);
        this.CancelCommand.Subscribe((state) => this.Cancel(state)).AddTo(_disposable);

        this.Initialize();
    }

    public async void Initialize()
    {
        this.Text.Value = await _clipboardService.GetTextAsync();
    }

    protected override async ValueTask OnDisposeAsync()
    {
        _disposable.Dispose();
    }

    public TextWindowStatus Status => _uiState.TextWindow ??= new TextWindowStatus();

    public ReactivePropertySlim<string> Text { get; }

    public ReactiveCommand OkCommand { get; }

    public ReactiveCommand CancelCommand { get; }

    public string GetResult() => this.Text.Value;

    private async void Ok(object state)
    {
        var window = (Window)state;
        window.Close();
    }

    private async void Cancel(object state)
    {
        this.Text.Value = string.Empty;

        var window = (Window)state;
        window.Close();
    }
}
