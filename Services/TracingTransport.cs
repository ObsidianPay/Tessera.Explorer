using Tessera.Nxpsc;

namespace Tessera.Explorer.Services;

/// <summary>
/// Wraps a transport so every frame can be shown in the output. Frames carry challenges and enciphered key
/// material, which is why tracing is off until the developer turns it on.
/// </summary>
internal class TracingTransport(ICardTransport inner) : ICardTransport
{
    public static volatile bool Enabled;
    protected readonly ICardTransport Inner = inner;

    public string Description => Inner.Description;
    public bool IsMock => Inner.IsMock;

    public bool TryTransceive(ReadOnlySpan<byte> command, Span<byte> response, out int responseLength)
    {
        var tx = Enabled ? Hex.Spaced(command) : null;
        var ok = Inner.TryTransceive(command, response, out responseLength);
        if (tx is not null)
        {
            try
            {
                OutputLog.Write(LogLevel.Trace, "tx", tx);
                OutputLog.Write(LogLevel.Trace, ok ? "rx" : "rx !", ok ? Hex.Spaced(response[..responseLength]) : "transport failure");
            }
            catch
            {
                // tracing must never break the native callback
            }
        }
        return ok;
    }

    public void Dispose() => Inner.Dispose();
}

internal sealed class TracingUidTransport(ICardTransport inner) : TracingTransport(inner), IUidTransport
{
    public bool TryGetUid(Span<byte> uid, out int length)
    {
        if (Inner is IUidTransport u) return u.TryGetUid(uid, out length);
        length = 0;
        return false;
    }
}
