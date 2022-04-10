using System.Reactive.Disposables;
using Omnius.Core.Cryptography;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;

namespace Omnius.Axis.Ui.Desktop.Windows.Settings;

public class SignaturesViewDesignViewModel : SignaturesViewViewModelBase
{
    private readonly CompositeDisposable _disposable = new();

    public SignaturesViewDesignViewModel()
    {
        this.AddCommand = new AsyncReactiveCommand().AddTo(_disposable);
        this.AddCommand.Subscribe(async () => await this.RegisterAsync()).AddTo(_disposable);

        this.Signatures = new();
        this.SelectedSignatures = new();

        this.ItemDeleteCommand = new AsyncReactiveCommand().AddTo(_disposable);
        this.ItemDeleteCommand.Subscribe(async () => await this.ItemDeleteAsync()).AddTo(_disposable);

        this.ItemCopySignatureCommand = new AsyncReactiveCommand().AddTo(_disposable);
        this.ItemCopySignatureCommand.Subscribe(async () => await this.ItemCopySeedAsync()).AddTo(_disposable);
    }

    protected override async ValueTask OnDisposeAsync()
    {
        _disposable.Dispose();
    }

    public override IEnumerable<OmniSignature> GetSignatures()
    {
        return Array.Empty<OmniSignature>();
    }

    public override void SetSignatures(IEnumerable<OmniSignature> signatures)
    {
    }

    private async Task RegisterAsync()
    {
    }

    private async Task ItemDeleteAsync()
    {
    }

    private async Task ItemCopySeedAsync()
    {
    }
}
