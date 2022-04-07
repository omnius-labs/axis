using System.Net.Sockets;
using Omnius.Axis.Remoting;
using Omnius.Core;
using Omnius.Core.Helpers;
using Omnius.Core.Net;
using Omnius.Core.Net.Caps;
using Omnius.Core.Net.Connections.Bridge;
using Omnius.Core.Net.Connections.Multiplexer;
using Omnius.Core.Net.Connections.Multiplexer.V1;
using Omnius.Core.RocketPack.Remoting;
using Omnius.Core.Tasks;

namespace Omnius.Axis.Daemon;

public static partial class Runner
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    public static async ValueTask EventLoopAsync(string databaseDirectoryPath, OmniAddress listenAddress, CancellationToken cancellationToken = default)
    {
        await using var service = await AxisService.CreateAsync(databaseDirectoryPath, cancellationToken);
        using var tcpListenerManager = new TcpListenerManager(listenAddress, cancellationToken);

        for (; ; )
        {
            _logger.Debug("EventLoop: Start");

            try
            {
                var socket = await tcpListenerManager.AcceptSocketAsync();
                await InternalEventLoopAsync(service, socket, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("Operation Canceled");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Unexpected Exception");
            }
            finally
            {
                _logger.Debug("EventLoop: End");
            }
        }
    }

    private static async Task InternalEventLoopAsync(AxisService service, Socket socket, CancellationToken cancellationToken = default)
    {
        using var socketCap = new SocketCap(socket);

        var bytesPool = BytesPool.Shared;
        await using var batchActionDispatcher = new BatchActionDispatcher(TimeSpan.FromMilliseconds(10));

        var bridgeConnectionOptions = new BridgeConnectionOptions(int.MaxValue);
        await using var bridgeConnection = new BridgeConnection(socketCap, null, null, batchActionDispatcher, bytesPool, bridgeConnectionOptions);

        var multiplexerOption = new OmniConnectionMultiplexerOptions(OmniConnectionMultiplexerType.Accepted, TimeSpan.FromMinutes(1), 3, 1024 * 1024 * 4, 3);
        await using var multiplexer = OmniConnectionMultiplexer.CreateV1(bridgeConnection, batchActionDispatcher, bytesPool, multiplexerOption);

        await multiplexer.HandshakeAsync(cancellationToken);

        var errorMessageFactory = new DefaultErrorMessageFactory();
        var listenerFactory = new RocketRemotingListenerFactory<DefaultErrorMessage>(multiplexer, errorMessageFactory, bytesPool);

        var server = new AxisServiceRemoting.Server<DefaultErrorMessage>(service, listenerFactory, bytesPool);

        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var onCloseListenerRegister = bridgeConnection.Events.OnClosed.Listen(() => ExceptionHelper.TryCatch<ObjectDisposedException>(() => linkedCancellationTokenSource.Cancel()));

        await server.EventLoopAsync(linkedCancellationTokenSource.Token);
    }
}
