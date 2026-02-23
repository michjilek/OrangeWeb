using System.ComponentModel;

namespace OP_Pages_Library.ViewModel;
public class MVBase : INotifyPropertyChanged
{
    // Event
    public event PropertyChangedEventHandler PropertyChanged;

    // Method via which Event PropertyChanged is called
    public void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
