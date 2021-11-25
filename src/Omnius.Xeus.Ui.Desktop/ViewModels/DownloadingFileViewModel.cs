using Generator.Equals;
using Omnius.Core.Avalonia;
using Omnius.Xeus.Intaractors.Models;

namespace Omnius.Xeus.Ui.Desktop.ViewModels;

[Equatable]
public partial class DownloadingFileViewModel : BindableBase, ICollectionViewModel<DownloadingFileViewModel, DownloadingFileReport>
{
    private DownloadingFileReport? _model;

    public DownloadingFileViewModel() { }

    public DownloadingFileViewModel(DownloadingFileReport? model)
    {
        this.Model = model;
    }

    public void Update(DownloadingFileReport? model)
    {
        this.Model = model;
    }

    public DownloadingFileReport? Model
    {
        get => _model;
        set
        {
            this.SetProperty(ref _model, value);
            this.RaisePropertyChanged(null);
        }
    }

    public string Name => this.Model?.Seed.Name ?? "";

    public DateTime CreationTime => this.Model?.CreationTime ?? DateTime.MinValue;

    public DownloadingFileState State => this.Model?.Status.State ?? DownloadingFileState.Unknown;

    public double Depth => this.Model?.Status?.CurrentDepth ?? -1;

    public double Rate => Math.Round(((double)(this.Model?.Status?.DownloadedBlockCount ?? 0) / this.Model?.Status?.TotalBlockCount ?? 1) * 100 * 100) / 100;
}
