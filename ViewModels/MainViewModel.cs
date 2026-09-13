using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RomRebuilderUI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // Holds the currently active workflow view model[cite: 6]
        [ObservableProperty] 
        private ObservableObject _currentViewModel;

        // Instantiate our independent workflow view models[cite: 6]
        public MameScannerViewModel MameScannerVm { get; } = new();
        public MameWorkflowViewModel MameVm { get; } = new();
        public ConsoleWorkflowViewModel ConsoleVm { get; } = new();
        public InspectorViewModel InspectorVm { get; } = new();
        public MonitorViewModel MonitorVm { get; } = new();

        public MainViewModel()
        {
            // Set default landing view to MAME scanner or MAME workflow[cite: 6]
            _currentViewModel = MameScannerVm;
        }

        [RelayCommand]
        private void Navigate(string destination)
        {
            CurrentViewModel = destination switch
            {
                "MameScanner" => MameScannerVm,
                "Mame" => MameVm,
                "Console" => ConsoleVm,
                "Inspector" => InspectorVm,
                "Monitor" => MonitorVm,
                _ => MameScannerVm
            };
        }
    }
}
