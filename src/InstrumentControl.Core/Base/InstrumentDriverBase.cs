using System.Windows;
using InstrumentControl.Core.Interfaces;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Base;

public abstract class InstrumentDriverBase : IInstrumentDriver
{
    protected IConnectionProvider? Connection;
    private bool _disposed;

    // Keeps every measurement raised by this driver (single-shot or continuous)
    // so UI such as the Live window can show the full history since connection,
    // not just what arrived while that window happened to be open.
    private const int MaxHistoryEntries = 5000;
    private readonly object _historyLock = new();
    private readonly List<MeasurementResult> _measurementHistory = new();

    public abstract string DriverName { get; }
    public abstract string Manufacturer { get; }
    public abstract string Model { get; }
    public abstract string Description { get; }
    public abstract string[] SupportedResourcePatterns { get; }

    public bool IsConnected => Connection?.IsOpen ?? false;
    public InstrumentInfo? InstrumentInfo { get; protected set; }

    public event EventHandler<MeasurementResult>? MeasurementReceived;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<Exception>? ErrorOccurred;

    public virtual async Task ConnectAsync(IConnectionProvider connection)
    {
        Connection = connection;
        if (!connection.IsOpen) await connection.OpenAsync();
        var idn = await GetIdentificationAsync();
        InstrumentInfo = new InstrumentInfo
        {
            ResourceName = connection.ResourceName,
            DriverName = DriverName,
            Manufacturer = Manufacturer,
            Model = Model,
            ConnectionType = connection.ConnectionType,
            Status = ConnectionStatus.Connected,
            ConnectedAt = DateTime.Now,
            FirmwareVersion = ParseFirmwareFromIdn(idn),
            SerialNumber = ParseSerialFromIdn(idn)
        };
        RaiseStatus($"Połączono: {idn}");
    }

    public virtual async Task DisconnectAsync()
    {
        if (Connection != null)
        {
            await Connection.CloseAsync();
            if (InstrumentInfo != null) InstrumentInfo.Status = ConnectionStatus.Disconnected;
        }
        RaiseStatus("Rozłączono");
    }

    public virtual async Task ReconnectAsync()
    {
        if (Connection == null) throw new InvalidOperationException("Brak połączenia");
        if (!Connection.IsOpen) await Connection.OpenAsync();
        var idn = await GetIdentificationAsync();
        if (InstrumentInfo != null) InstrumentInfo.Status = ConnectionStatus.Connected;
        RaiseStatus($"Ponownie połączono: {idn}");
    }

    public virtual async Task<string> GetIdentificationAsync()
    {
        if (Connection == null) throw new InvalidOperationException("Brak połączenia");
        return await Connection.QueryAsync("*IDN?");
    }

    public virtual async Task ResetAsync()
    {
        if (Connection == null) throw new InvalidOperationException("Brak połączenia");
        await Connection.WriteAsync("*RST");
        await Task.Delay(200);
        await Connection.WriteAsync("*CLS");
    }

    protected async Task<string> Query(string command)
    {
        if (Connection == null) throw new InvalidOperationException("Brak połączenia");
        return await Connection.QueryAsync(command);
    }

    protected async Task Write(string command)
    {
        if (Connection == null) throw new InvalidOperationException("Brak połączenia");
        await Connection.WriteAsync(command);
    }

    protected async Task<double> QueryDouble(string command)
    {
        var s = await Query(command);
        return double.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
    }

    /// <summary>All measurements recorded so far (single-shot and continuous), oldest first.</summary>
    public IReadOnlyList<MeasurementResult> MeasurementHistory
    {
        get { lock (_historyLock) return _measurementHistory.ToArray(); }
    }

    public void ClearMeasurementHistory()
    {
        lock (_historyLock) _measurementHistory.Clear();
    }

    protected void RaiseMeasurement(MeasurementResult result)
    {
        lock (_historyLock)
        {
            _measurementHistory.Add(result);
            if (_measurementHistory.Count > MaxHistoryEntries)
                _measurementHistory.RemoveAt(0);
        }
        // Fire-and-forget: the CSV write (disk flush) must never block the caller —
        // MeasureXAsync() call chains resume on the UI thread, and a slow/AV-scanned
        // disk shouldn't be able to stall a fast continuous-measurement loop.
        _ = Task.Run(() => MeasurementLogService.Log(result));
        MeasurementReceived?.Invoke(this, result);
    }

    protected void RaiseStatus(string msg) =>
        StatusChanged?.Invoke(this, msg);

    protected void RaiseError(Exception ex) =>
        ErrorOccurred?.Invoke(this, ex);

    private static string ParseFirmwareFromIdn(string idn)
    {
        var parts = idn.Split(',');
        return parts.Length >= 4 ? parts[3].Trim() : string.Empty;
    }

    private static string ParseSerialFromIdn(string idn)
    {
        var parts = idn.Split(',');
        return parts.Length >= 3 ? parts[2].Trim() : string.Empty;
    }

    public abstract FrameworkElement CreateFrontPanel();
    public abstract IEnumerable<ISequenceBlock> GetAvailableBlocks();

    public void Dispose()
    {
        if (!_disposed)
        {
            Connection?.Dispose();
            _disposed = true;
        }
    }
}
