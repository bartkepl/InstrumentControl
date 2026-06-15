using CommunityToolkit.Mvvm.ComponentModel;

namespace InstrumentControl.App.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    protected void RunOnUi(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } disp)
            disp.BeginInvoke(action);
        else
            action();
    }
}
