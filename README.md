# PerspectiveKeyboard

A Windows desktop application for sending input events to Microsoft Flight Simulator (MSFS) using SimConnect. The app features a simple WPF UI with a toggle switch to enable/disable hotkey input events, and integrates with the SimConnect SDK for communication with MSFS.

This is designed to be used with the Cirrus SR22T's GCU 479.

## Features
- WPF UI with modern toggle switch for enabling/disabling hotkey input events
- Global keyboard hook for hotkey detection
- Sends input events to MSFS via SimConnect
- Caps Lock integration for toggle state
- Exception handling for robust operation

## Requirements
- Windows 10/11 (x64)
- .NET 8.0 (WPF)
- Microsoft Flight Simulator (MSFS)
- SimConnect SDK (DLLs included in `libs/`)

## Build Instructions
1. Clone the repository:
    ```powershell
    git clone https://github.com/bstudtma/PerspectiveKeyboard.git
    cd PerspectiveKeyboard
    ```
   ```
2. Build and publish (Release, single file):
   ```powershell
   dotnet publish -c Release -r win-x64 /p:PublishSingleFile=true
   ```
3. Output will be in `bin/Release/net8.0-windows/win-x64/`

## Usage
- Run `PerspectiveKeyboard.exe`.
- Use the toggle switch to enable/disable hotkey input events.
- The app will connect to MSFS via SimConnect and send input events as configured.

## Dependencies
- `Microsoft.FlightSimulator.SimConnect.dll` (included in `libs/`)
- `SimConnect.dll` (included in `libs/`)

## Project Structure
- `App.xaml`, `App.xaml.cs`: Application entry and global exception handling
- `MainWindow.xaml`, `MainWindow.xaml.cs`: Main UI and logic
- `libs/`: SimConnect DLLs
- `PerspectiveKeyboard.csproj`: Project file

## License
MIT
