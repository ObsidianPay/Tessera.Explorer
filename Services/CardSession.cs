using System.Diagnostics;
using Tessera.Nxpsc;
using Tessera.Nxpsc.Mock;

namespace Tessera.Explorer.Services;

public sealed record SessionState(
    bool Connected, bool IsMock, string Transport, NxpscCardType CardType, bool Authenticated, NxpscChannel Channel,
    NxpscCmdSet CmdSet, uint SelectedAid, byte LastStatus, bool SessionLost, bool IsoChaining)
{
    public static readonly SessionState None = new(false, false, "", NxpscCardType.Unknown, false, NxpscChannel.Auto,
        NxpscCmdSet.NativeIso, 0, 0, false, false);
}

/// <summary>Outcome of one card call: whether it succeeded and what it returned.</summary>
public readonly record struct Result<T>(bool Ok, T? Value);

/// <summary>
/// The one card the explorer talks to. Every call goes through <see cref="Run{T}"/>, which serialises access
/// (libnxpsc wants one thread at a time), times the call, logs it and reports failures with everything the
/// exception carries: code, card status byte and whether the PICC dropped the secure session.
/// </summary>
public sealed class CardSession
{
    public static CardSession Instance { get; } = new();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private NxpscCard? _card;
    private ICardTransport? _transport;

    public MockCard? Mock { get; private set; }
    public SessionState State { get; private set; } = SessionState.None;
    public bool IsConnected => _card is not null;
    public bool IsMock => Mock is not null;

    /// <summary>True once any mock card exists in this process: libnxpsc's RNG is then fixed and real cards are refused.</summary>
    public static bool MockRngActive { get; private set; }

    public event Action? StateChanged;

    // ---- connect / disconnect ------------------------------------------------------------------

    public async Task ConnectReaderAsync(string reader, TimeSpan timeout, NxpscCmdSet cmdSet, CancellationToken ct)
    {
        Disconnect();
        OutputLog.Info("connect", $"Waiting for a card on '{reader}' ({timeout.TotalSeconds:0} s)...");
        var transport = await Task.Run(() => PcscTransport.WaitForCard(reader, timeout, ct), ct);
        await OpenAsync(new TracingUidTransport(transport), null, cmdSet);
    }

    public async Task ConnectMockAsync(NxpscCardType type, byte[] uid, NxpscCmdSet cmdSet)
    {
        Disconnect();
        var mock = new MockCard(type, uid);
        MockRngActive = true;
        OutputLog.Info("connect", $"Mock card created: {NxpscLibrary.CardTypeName(type)}, UID {Hex.Format(uid)}.");
        await OpenAsync(new TracingTransport(mock), mock, cmdSet);
    }

    private async Task OpenAsync(ICardTransport transport, MockCard? mock, NxpscCmdSet cmdSet)
    {
        try
        {
            await _gate.WaitAsync();
            try
            {
                _transport = transport;
                Mock = mock;
                _card = NxpscCard.Open(transport, cmdSet);
            }
            finally { _gate.Release(); }
            OutputLog.Ok("open", $"Session open over {transport.Description}, command set {cmdSet}.");
            // libnxpsc frames later commands by card type, so identify straight away
            await Run("GetVersion", c => c.GetVersion(), Describe.Version);
            await Run("Identify", c => c.Identify(), t => NxpscLibrary.CardTypeName(t));
        }
        catch
        {
            Disconnect();
            throw;
        }
        RaiseChanged();
    }

    public void Disconnect()
    {
        var had = _card is not null;
        _gate.Wait();
        try
        {
            _card?.Dispose();
            _card = null;
            _transport?.Dispose();
            _transport = null;
            Mock = null;
            State = SessionState.None;
        }
        finally { _gate.Release(); }
        if (had) OutputLog.Info("disconnect", "Session closed.");
        RaiseChanged();
    }

    // ---- running operations --------------------------------------------------------------------------

    /// <summary>Runs <paramref name="call"/> on the card and logs it. <paramref name="format"/> renders the result for the log.</summary>
    public async Task<Result<T>> Run<T>(string op, Func<NxpscCard, T> call, Func<T, string?>? format = null, bool quiet = false)
    {
        if (_card is not { } card)
        {
            OutputLog.Error(op, "Not connected.");
            return new(false, default);
        }
        await _gate.WaitAsync();
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await Task.Run(() => call(card));
            sw.Stop();
            if (!quiet)
            {
                OutputLog.Ok(op, $"ok  {sw.ElapsedMilliseconds} ms");
                var text = format is not null ? format(result) : Describe.Auto(result);
                if (!string.IsNullOrEmpty(text)) OutputLog.Data(op, text);
            }
            return new(true, result);
        }
        catch (NxpscException ex)
        {
            sw.Stop();
            var status = ex.CardStatus is { } s ? $", status=0x{s:X2}" : "";
            OutputLog.Error(op, $"{ex.Message}  [code={ex.Code} ({(int)ex.Code}){status}, {sw.ElapsedMilliseconds} ms]");
            if (ex.SessionLost)
                OutputLog.Warn(op, "The PICC dropped the secure session. Authenticate again before any in-session command.");
            if (ex.IsTransport)
                OutputLog.Warn(op, "Transport failure: card removed, reader gone, or the mock was torn.");
            return new(false, default);
        }
        catch (ObjectDisposedException)
        {
            OutputLog.Error(op, "The session is closed.");
            return new(false, default);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            OutputLog.Error(op, ex.Message);
            return new(false, default);
        }
        finally
        {
            _gate.Release();
            Refresh();
        }
    }

    /// <summary>For calls that return nothing. <paramref name="okText"/> goes to the log as data when given.</summary>
    public async Task<bool> Run(string op, Action<NxpscCard> call, string? okText = null)
    {
        var r = await Run<string?>(op, c => { call(c); return okText; }, s => s);
        return r.Ok;
    }

    /// <summary>Operations on the mock itself (not the card) run under the same gate.</summary>
    public async Task<bool> RunMock(string op, Action<MockCard> call, string? okText = null)
    {
        if (Mock is not { } mock)
        {
            OutputLog.Error(op, "No mock card is connected.");
            return false;
        }
        await _gate.WaitAsync();
        try
        {
            call(mock);
            OutputLog.Ok(op, okText ?? "done");
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
        {
            OutputLog.Error(op, ex.Message);
            return false;
        }
        finally
        {
            _gate.Release();
            Refresh();
        }
    }

    /// <summary>Plain property writes on the card (command set, comm mode, ISO chaining, channel reset).</summary>
    public void Configure(Action<NxpscCard> change)
    {
        if (_card is not { } card) return;
        _gate.Wait();
        try { change(card); }
        finally { _gate.Release(); }
        Refresh();
    }

    // ---- state -----------------------------------------------------------------------------------------------

    public void Refresh()
    {
        if (_card is not { } card) return;
        try
        {
            State = new SessionState(true, IsMock, _transport?.Description ?? "", card.CardType, card.IsAuthenticated,
                card.ActiveChannel, card.CmdSet, card.SelectedAid, card.LastStatus, card.SessionLost, card.IsoChaining);
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) StateChanged?.Invoke();
        else d.BeginInvoke(() => StateChanged?.Invoke());
    }
}
