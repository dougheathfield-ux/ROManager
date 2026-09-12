using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RomRebuilderUI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // Holds the currently active workflow view model
        [ObservableProperty] 
        private ObservableObject _currentViewModel;

        // Instantiate our independent workflow view models (stubs to be created next)
        public MameWorkflowViewModel MameVm { get; } = new();
        public ConsoleWorkflowViewModel ConsoleVm { get; } = new();
        public InspectorViewModel InspectorVm { get; } = new();
        public MonitorViewModel MonitorVm { get; } = new();

        public MainViewModel()
        {
            // Set default landing view to MAME workflow
            _currentViewModel = MameVm;
        }

        [RelayCommand]
        private void Navigate(string destination)
        {
            CurrentViewModel = destination switch
            {
                "Mame" => MameVm,
                "Console" => ConsoleVm,
                "Inspector" => InspectorVm,
                "Monitor" => MonitorVm,
                _ => MameVm
            };
        }
    }
}
