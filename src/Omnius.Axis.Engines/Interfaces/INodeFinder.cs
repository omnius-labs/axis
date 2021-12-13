using Omnius.Axis.Models;

namespace Omnius.Axis.Engines;

public interface INodeFinder : IAsyncDisposable
{
    INodeFinderEvents GetEvents();

    ValueTask<NodeFinderReport> GetReportAsync(CancellationToken cancellationToken = default);

    ValueTask<NodeLocation> GetMyNodeLocationAsync(CancellationToken cancellationToken = default);

    ValueTask AddCloudNodeLocationsAsync(IEnumerable<NodeLocation> nodeLocations, CancellationToken cancellationToken = default);

    ValueTask<NodeLocation[]> FindNodeLocationsAsync(ContentClue contentClue, CancellationToken cancellationToken = default);
}