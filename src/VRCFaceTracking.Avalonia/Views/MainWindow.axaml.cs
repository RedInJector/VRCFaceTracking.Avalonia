using Avalonia;
using Avalonia.Layout;
using Avalonia.Controls;
using System.Runtime.InteropServices;
using VRCFaceTracking.Avalonia.ViewModels;
using System;

namespace VRCFaceTracking.Avalonia.Views;

public partial class MainWindow : Window
{
    // constructor with 1 parameter is needed to stop the DI to instantly create the window (when declared as singleton)
    // during the startup phase and crashing the whole android app
    // with "Specified method is not supported window" error
    public MainWindow(MainViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        AdjustTitleBarForPlatform();
        this.AttachDevTools();
    }

    private void AdjustTitleBarForPlatform()
    {
        if (OperatingSystem.IsIOS() || OperatingSystem.IsAndroid()) // mobile
        {
            // elements are already disabled.
        }
        else if (OperatingSystem.IsMacOS())
        {
            ApplicationTitleBar.IsVisible = true;
            TitleBarContent.HorizontalAlignment = HorizontalAlignment.Center;
        }
        else if (OperatingSystem.IsWindows())
        {
            ApplicationTitleBar.IsVisible = true;
            TitleBarContent.HorizontalAlignment = HorizontalAlignment.Left;
        }
        else if (OperatingSystem.IsLinux())
        {
            // Linux has so many edge cases, we will just let the OS handle titlebars.
            SystemDecorations = SystemDecorations.Full;
            ExtendClientAreaToDecorationsHint = false;
        }
        else // unknown platform, revert to use platform's decorations
        {
            SystemDecorations = SystemDecorations.Full;
            ExtendClientAreaToDecorationsHint = false;
        }

    }

    public MainWindow() : this(new MainViewModel()) { }
}
