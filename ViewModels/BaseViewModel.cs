using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GeminiGUI.ViewModels
{
    public abstract class BaseViewModel : ObservableObject
    {
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public new event PropertyChangedEventHandler? PropertyChanged;
    }
}
