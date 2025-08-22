using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input; // For Keyboard and Key
using System.Windows.Interop;

namespace PerspectiveKeyboard
{
    public partial class MainWindow : Window
    {
    // Value/payload to send with the input event if required by the specific event.
    private const double INPUT_EVENT_VALUE = 0.0; // adjust per event needs

        // SimConnect constants / fields
        private const int WM_USER_SIMCONNECT = 0x0402; // WM_USER + 2
        private SimConnect? _simConnect;
        private HwndSource? _source;
    private volatile bool _connected = false;
    private volatile bool _enabled = false;
    private volatile bool _enumerationRequested = false;

    // Low-level keyboard hook
        private static IntPtr _hookId = IntPtr.Zero;
        private static LowLevelKeyboardProc? _proc;
    private CancellationTokenSource? _hookCts;

        // Dictionary to store input events by name
        private readonly Dictionary<string, SIMCONNECT_INPUT_EVENT_DESCRIPTOR> _inputEventDict
            = new Dictionary<string, SIMCONNECT_INPUT_EVENT_DESCRIPTOR>(StringComparer.OrdinalIgnoreCase);

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;

            // Initialize toggle switch state based on Caps Lock
            PowerSwitch.IsChecked = Keyboard.IsKeyToggled(Key.CapsLock);
            PowerSwitch.Checked += PowerSwitch_Checked;
            PowerSwitch.Unchecked += PowerSwitch_Unchecked;
        }

        private void PowerSwitch_Checked(object sender, RoutedEventArgs e)
        {
            // Enable Caps Lock when toggle is switched on
            if (!Keyboard.IsKeyToggled(Key.CapsLock))
            {
                ToggleCapsLock();
            }

            // Ensure _enabled is true when toggle is enabled
            _enabled = true;
            Debug.WriteLine($"[UI] Enabled = {_enabled}");
            TryEnumerateInputEvents();
        }

        private void PowerSwitch_Unchecked(object sender, RoutedEventArgs e)
        {
            // Disable Caps Lock when toggle is switched off
            if (Keyboard.IsKeyToggled(Key.CapsLock))
            {
                ToggleCapsLock();
            }

            // Ensure _enabled is false when toggle is disabled
            _enabled = false;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Hook window message loop for SimConnect
            var helper = new WindowInteropHelper(this);
            _source = HwndSource.FromHwnd(helper.Handle);
            _source?.AddHook(WndProc);

            ConnectSim();

            InstallKeyboardHook();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            UninstallKeyboardHook();
            DisconnectSim();
        }

        #region SimConnect
        private void ConnectSim()
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                _simConnect = new SimConnect("SimInputHotkey", helper.Handle, WM_USER_SIMCONNECT, null, 0);
                _simConnect.OnRecvQuit += SimConnect_OnRecvQuit;
                _simConnect.OnRecvException += SimConnect_OnRecvException;
                _simConnect.OnRecvEnumerateInputEvents += SimConnect_OnRecvEnumerateInputEvents;

                _connected = true;
                Debug.WriteLine("[Sim] Connected.");
            }
            catch (Exception ex)
            {
                _connected = false;
                Debug.WriteLine("[Sim] Connect failed: " + ex);
                // non-fatal: we’ll lazy-retry on key press
            }
        }

        private void DisconnectSim()
        {
            try
            {
                if (_simConnect != null)
                {
                    _simConnect.OnRecvQuit -= SimConnect_OnRecvQuit;
                    _simConnect.OnRecvException -= SimConnect_OnRecvException;
                    _simConnect.OnRecvEnumerateInputEvents -= SimConnect_OnRecvEnumerateInputEvents;
                    _simConnect.Dispose();
                    _simConnect = null;
                }
            }
            finally
            {
                _connected = false;
                Debug.WriteLine("[Sim] Disconnected.");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_USER_SIMCONNECT && _simConnect != null)
            {
                _simConnect.ReceiveMessage();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void SimConnect_OnRecvEnumerateInputEvents(SimConnect sender, SIMCONNECT_RECV_ENUMERATE_INPUT_EVENTS data)
        {
            try
            {
                // Cache descriptors for quick lookup
                if (data?.rgData != null)
                {
                    foreach (var inputEvent in data.rgData)
                    {
                        if (inputEvent is SIMCONNECT_INPUT_EVENT_DESCRIPTOR desc)
                        {
                            if (!_inputEventDict.ContainsKey(desc.Name))
                            {
                                _inputEventDict[desc.Name] = desc;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[Sim] Error caching input events: {e}");
            }
        }

        private void SimConnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Debug.WriteLine("[Sim] Received QUIT");
            DisconnectSim();
        }

        private void SimConnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            Debug.WriteLine($"[Sim] Exception: {data.dwException} at sendID={data.dwSendID} index={data.dwIndex}");
        }

        private async Task EnsureConnectedAsync()
        {
            if (_connected && _simConnect != null) return;
            await Task.Run(() => ConnectSim());
        }
        #endregion

        #region Fire Input Event
        private async Task FireInputEventAsync(int vkCode)
        {
            if (!_enabled) return;

            await EnsureConnectedAsync();
            if (_simConnect == null || !_connected) return;

            // Map vkCode to input event name
            string inputEventName = vkCode switch
            {
                0x30 => "AS1000_CONTROLPAD_0", // '0'
                0x31 => "AS1000_CONTROLPAD_1", // '1'
                0x32 => "AS1000_CONTROLPAD_2", // '2'
                0x33 => "AS1000_CONTROLPAD_3", // '3'
                0x34 => "AS1000_CONTROLPAD_4", // '4'
                0x35 => "AS1000_CONTROLPAD_5", // '5'
                0x36 => "AS1000_CONTROLPAD_6", // '6'
                0x37 => "AS1000_CONTROLPAD_7", // '7'
                0x38 => "AS1000_CONTROLPAD_8", // '8'
                0x39 => "AS1000_CONTROLPAD_9", // '9'
                0x0D => "AS1000_CONTROLPAD_1_ENTER", // Enter
                0x08 => "AS1000_CONTROLPAD_CLEAR_2", // Backspace
                0xDC => "AS1000_CONTROLPAD_PUSH_SWAP", // Backslash
                >= 0x41 and <= 0x5A => $"AS1000_CONTROLPAD_{(char)vkCode}", // A-Z
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(inputEventName)) return;

            // If we don't have the descriptor yet, request enumeration once
            if (!_inputEventDict.ContainsKey(inputEventName))
            {
                TryEnumerateInputEvents();
                return;
            }

            try
            {
                if (_inputEventDict.TryGetValue(inputEventName, out var descriptor))
                {
                    Debug.WriteLine($"[Sim] Firing input event: {descriptor.Name} (Hash: {descriptor.Hash})");
                    if (!TryInvokeSetInputEvent(descriptor.Hash, INPUT_EVENT_VALUE))
                    {
                        Debug.WriteLine("[Sim] Unable to locate a compatible SetInputEvent method on SimConnect.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Sim] SetInputEvent failed: " + ex);
            }
        }

        private void TryEnumerateInputEvents()
        {
            try
            {
                if (_simConnect != null && _connected && !_enumerationRequested)
                {
                    _enumerationRequested = true;
                    _simConnect.EnumerateInputEvents(MyTestEnum.None);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sim] EnumerateInputEvents failed: {ex}");
            }
        }

        // Support both historical signatures: (ulong,double) and (ulong,string)
        private bool TryInvokeSetInputEvent(ulong hash, double value)
        {
            if (_simConnect == null) return false;
            try
            {
                var t = _simConnect.GetType();
                var miDouble = t.GetMethod("SetInputEvent", new[] { typeof(ulong), typeof(double) });
                if (miDouble != null)
                {
                    miDouble.Invoke(_simConnect, new object[] { hash, value });
                    return true;
                }

                var miString = t.GetMethod("SetInputEvent", new[] { typeof(ulong), typeof(string) });
                if (miString != null)
                {
                    miString.Invoke(_simConnect, new object[] { hash, value.ToString(CultureInfo.InvariantCulture) });
                    return true;
                }
            }
            catch (TargetInvocationException tie)
            {
                Debug.WriteLine($"[Sim] SetInputEvent invocation error: {tie.InnerException ?? tie}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sim] SetInputEvent reflection error: {ex}");
            }
            return false;
        }
        #endregion

        #region Toggle
        private void PowerSwitch_Click(object sender, RoutedEventArgs e)
        {
            _enabled = PowerSwitch.IsChecked == true;
            if (_enabled)
            {
                // Proactively populate events when enabling
                TryEnumerateInputEvents();
            }
        }
        #endregion

        #region Global Keyboard Hook (WH_KEYBOARD_LL)
        private void InstallKeyboardHook()
        {
            if (_hookId != IntPtr.Zero) return;
            _proc = HookCallback;
            // For low-level hooks, hMod can be IntPtr.Zero in managed apps; fall back to current module handle
            IntPtr hMod = GetModuleHandle(null);
            if (hMod == IntPtr.Zero)
            {
                using var curProcess = Process.GetCurrentProcess();
                using var curModule = curProcess.MainModule!;
                hMod = GetModuleHandle(curModule.ModuleName);
            }
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hMod, 0);
            if (_hookId == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[Hook] Failed to install. Win32Error={err}");
            }

            // Monitor Caps Lock key state changes
            _hookCts = new CancellationTokenSource();
            _ = Task.Run(() => MonitorCapsLockState(_hookCts.Token));
        }

        private void UninstallKeyboardHook()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            try
            {
                _hookCts?.Cancel();
            }
            catch { /* ignored */ }
            finally
            {
                _hookCts?.Dispose();
                _hookCts = null;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            const int WM_KEYDOWN = 0x0100;
            const int WM_SYSKEYDOWN = 0x0104;

            try
            {
                if (_enabled && nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    bool handled = false;

                    // Numbers 0-9
                    if (vkCode is >= 0x30 and <= 0x39)
                    {
                        _ = Task.Run(() => FireInputEventAsync(vkCode));
                        handled = true;
                    }
                    // Enter
                    else if (vkCode == 0x0D)
                    {
                        _ = Task.Run(() => FireInputEventAsync(vkCode));
                        handled = true;
                    }
                    // Backslash
                    else if (vkCode == 0xDC)
                    {
                        _ = Task.Run(() => FireInputEventAsync(vkCode));
                        handled = true;
                    }
                    // Backspace
                    else if (vkCode == 0x08)
                    {
                        _ = Task.Run(() => FireInputEventAsync(vkCode));
                        handled = true;
                    }
                    // Letters A-Z
                    else if (vkCode is >= 0x41 and <= 0x5A)
                    {
                        _ = Task.Run(() => FireInputEventAsync(vkCode));
                        handled = true;
                    }

                    if (handled)
                    {
                        return (IntPtr)1; // Eat the key press
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Hook] Error in callback: {ex}");
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private async Task MonitorCapsLockState(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(100, token); // Poll every 100ms
                    if (token.IsCancellationRequested) break;
                    await Dispatcher.InvokeAsync(() =>
                    {
                        bool isCapsLockOn = Keyboard.IsKeyToggled(Key.CapsLock);
                        if (PowerSwitch.IsChecked != isCapsLockOn)
                        {
                            PowerSwitch.IsChecked = isCapsLockOn;
                            _enabled = isCapsLockOn;
                            if (_enabled)
                            {
                                TryEnumerateInputEvents();
                            }
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Hook] CapsLock monitor error: {ex}");
                }
            }
        }

        private void ToggleCapsLock()
        {
            // Simulate Caps Lock key press
            keybd_event((byte)KeyInterop.VirtualKeyFromKey(Key.CapsLock), 0x45, KEYEVENTF_EXTENDEDKEY | 0, 0);
            keybd_event((byte)KeyInterop.VirtualKeyFromKey(Key.CapsLock), 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const int WH_KEYBOARD_LL = 13;
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
        #endregion

        public enum MyTestEnum
        {
            None = 0,
            OptionA = 1,
            OptionB = 2
        }
    }
}
