# RomRebuilderUI (ROManager)

A cross-platform desktop application for scanning, managing, and rebuilding ROM collections. Built with Avalonia UI and .NET 10, RomRebuilderUI provides a small MVVM-driven interface to scan directories for ROM files, manage preferences, and rebuild or repack ROM sets.

## Features

- Scan directories for ROM files and present them in the UI
- Rebuild/repack ROM sets using configurable options (service logic in Services/RomRebuilderService.cs)
- Persistent application preferences (Models/AppPreferences.cs, SettingsManager.cs)
- MVVM architecture with ViewModels and Views (Views/MainWindow.axaml, ViewModels/MainViewModel.cs)
- Cross-platform UI using Avalonia

## Repository structure

- App.axaml / App.axaml.cs — Avalonia application definitions
- Views/ — UI definitions and code-behind (MainWindow.axaml, MainWindow.axaml.cs)
- ViewModels/ — View model classes (MainViewModel.cs, ViewModelBase.cs)
- Models/ — Domain models and preferences (RomModels.cs, AppPreferences.cs)
- Services/ — Core logic (RomScanner.cs, RomRebuilderService.cs)
- SettingsManager.cs — Preferences persistence
- Assets/ — Icons and resources

## Requirements

- .NET 10 SDK
- Native runtime dependencies for Avalonia (handled by Avalonia runtime packages)

## Build & run

From the repository root:

```bash
# restore dependencies
dotnet restore

# build
dotnet build

# run (from repo root)
dotnet run --project RomRebuilderUI.csproj
```

The application uses Avalonia platform detection and should run on Windows, macOS, and Linux where .NET 10 and native runtimes are available.


## Usage

1. Launch the app.
2. Configure preferences (locations for ROM files) in the app preferences UI.
3. Use the scanner to find ROM files.
4. Select sets and run the rebuild/repack operations.





---


