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

## Development notes

- The repository currently contains build outputs (bin/ and obj/). Add a `.gitignore` and remove those folders from the repo to keep the source tree clean.
- Assemblies observed in the build output include Avalonia, CommunityToolkit.Mvvm, SharpCompress, SkiaSharp, and HarfBuzzSharp — these indicate UI, MVVM patterns, and archive/graphics handling.
- There's a runtime log `rom_rebuilder_debug.log` in the repo root for debugging.

## Usage

1. Launch the app.
2. Configure preferences (locations for ROM files) in the app preferences UI.
3. Use the scanner to find ROM files.
4. Select sets and run the rebuild/repack operations.

Add a short how-to with screenshots or example workflows when convenient to help new users.

## Suggestions / next steps

- Add this README (done) and include screenshots to clarify UI and workflows.
- Remove committed build artifacts (bin/ and obj/) and add a `.gitignore`.
- Document any rebuild options and formats the application supports.
- Consider packaging/making platform-specific installers for distribution.

## Contributing

Contributions are welcome. Please open issues or pull requests with bug reports, feature requests, or improvements. Include clear reproduction steps and target platform information.

## License

Add a license file (e.g., `LICENSE`) to indicate the project license. If you want, I can add an MIT or other license template for you.

---

If you'd like I can:
- Add a .gitignore and remove current bin/obj files
- Add an example preferences file or sample ROM directory structure
- Create a short contributor guide and issue templates

