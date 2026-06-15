using System.Threading.Tasks;

namespace InstrumentControl.Core.Interfaces;

public interface IConnectionProvider : IDisposable
{
    string ResourceName { get; }
    string ConnectionType { get; }
    bool IsOpen { get; }

    Task OpenAsync();
    Task CloseAsync();
    Task WriteAsync(string command);
    Task<string> QueryAsync(string command, int timeoutMs = 5000);
    Task<string> ReadAsync(int timeoutMs = 5000);
    Task WriteRawAsync(byte[] data);
    Task<byte[]> ReadRawAsync(int count);
}
