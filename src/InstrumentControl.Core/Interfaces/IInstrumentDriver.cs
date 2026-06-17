using System.Windows;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Interfaces;

public interface IInstrumentDriver : IDisposable
{
    string DriverName { get; }
    string Manufacturer { get; }
    string Model { get; }
    string Description { get; }
    string[] SupportedResourcePatterns { get; }
    bool IsConnected { get; }
    InstrumentInfo? InstrumentInfo { get; }

    Task ConnectAsync(IConnectionProvider connection);
    Task DisconnectAsync();
    Task ReconnectAsync();
    Task<string> GetIdentificationAsync();
    Task ResetAsync();

    FrameworkElement CreateFrontPanel();
    IEnumerable<ISequenceBlock> GetAvailableBlocks();

    event EventHandler<MeasurementResult>? MeasurementReceived;
    event EventHandler<string>? StatusChanged;
    event EventHandler<Exception>? ErrorOccurred;
}
