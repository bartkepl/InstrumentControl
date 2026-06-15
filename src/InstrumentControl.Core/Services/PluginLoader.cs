using System.Composition;
using System.Composition.Hosting;
using System.IO;
using System.Reflection;
using InstrumentControl.Core.Interfaces;

namespace InstrumentControl.Core.Services;

[AttributeUsage(AttributeTargets.Class)]
public class InstrumentDriverAttribute : ExportAttribute
{
    public InstrumentDriverAttribute() : base(typeof(IInstrumentDriver)) { }
}

public class PluginLoader
{
    private readonly List<IInstrumentDriver> _loadedDrivers = new();

    public IReadOnlyList<IInstrumentDriver> LoadedDrivers => _loadedDrivers;

    public void LoadFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        var dlls = Directory.GetFiles(directoryPath, "*.dll", SearchOption.TopDirectoryOnly);
        foreach (var dll in dlls)
        {
            try { LoadFromAssembly(dll); }
            catch { /* skip malformed DLLs */ }
        }
    }

    public void LoadFromAssembly(string assemblyPath)
    {
        var asm = Assembly.LoadFrom(assemblyPath);
        LoadFromAssembly(asm);
    }

    public void LoadFromAssembly(Assembly assembly)
    {
        var driverTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(IInstrumentDriver).IsAssignableFrom(t));

        foreach (var type in driverTypes)
        {
            try
            {
                if (Activator.CreateInstance(type) is IInstrumentDriver driver)
                    _loadedDrivers.Add(driver);
            }
            catch { }
        }
    }

    public IInstrumentDriver? CreateDriver(string driverName)
    {
        var template = _loadedDrivers.FirstOrDefault(d =>
            d.DriverName.Equals(driverName, StringComparison.OrdinalIgnoreCase));
        if (template == null) return null;

        return (IInstrumentDriver?)Activator.CreateInstance(template.GetType());
    }

    public IEnumerable<(string Name, string Manufacturer, string Model, string Description)> GetAvailableDrivers()
    {
        return _loadedDrivers.Select(d => (d.DriverName, d.Manufacturer, d.Model, d.Description));
    }
}
