using Omnius.Axus.Messages;
using Omnius.Core;
using Omnius.Core.Cryptography;
using Omnius.Core.Net;
using Omnius.Core.Net.Connections;

namespace Omnius.Axus.Engines.Internal;

internal class Session : AsyncDisposableBase, ISession
{
    public Session(IConnection connection, OmniAddress address, SessionHandshakeType handshakeType, OmniSignature signature, string scheme)
    {
        this.Connection = connection;
        this.Address = address;
        this.HandshakeType = handshakeType;
        this.Signature = signature;
        this.Scheme = scheme;
    }

    protected override async ValueTask OnDisposeAsync()
    {
        await this.Connection.DisposeAsync();
    }

    public IConnection Connection { get; }
    public OmniAddress Address { get; }
    public SessionHandshakeType HandshakeType { get; }
    public OmniSignature Signature { get; }
    public string Scheme { get; }
}
