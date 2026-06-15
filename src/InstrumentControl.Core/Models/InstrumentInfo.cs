namespace InstrumentControl.Core.Models;

public enum ConnectionStatus { Disconnected, Connecting, Connected, Error }

public class InstrumentInfo
{
    public string ResourceName { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string UserLabel { get; set; } = string.Empty;
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Disconnected;
    public DateTime ConnectedAt { get; set; }
    public string ConnectionType { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrEmpty(UserLabel)
        ? $"{Manufacturer} {Model}"
        : UserLabel;
}
