using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.DependencyInjection;
using VRCFaceTracking.Avalonia.Services;
using VRCFaceTracking.Avalonia.ViewModels;
using VRCFaceTracking.Avalonia.ViewModels.SplitViewPane;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Avalonia.Views;

public partial class ModuleRegistryView : UserControl
{
    public static event Action<InstallableTrackingModule>? ModuleSelected;
    public static event Action? LocalModuleInstalled;
    private ModuleInstaller ModuleInstaller { get; }
    private ILibManager LibManager { get; set; }

    private readonly DropOverlayService _dropOverlayService;


    private static FilePickerFileType ZIP { get; } = new("Zip Files")
    {
        Patterns = [ "*.zip" ]
    };

    public ModuleRegistryView()
    {
        InitializeComponent();

        ModuleInstaller = Ioc.Default.GetService<ModuleInstaller>()!;
        LibManager = Ioc.Default.GetService<ILibManager>()!;
        _dropOverlayService = Ioc.Default.GetService<DropOverlayService>()!;

        this.Get<Button>("BrowseLocal")!.Click += async delegate
        {
            var topLevel = TopLevel.GetTopLevel(this)!;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a .zip.",
                AllowMultiple = false,
                FileTypeFilter = [ZIP]
            });

            if (files.Count == 0) return;

            string? path = null;
            try
            {
                path = await ModuleInstaller.InstallLocalModule(files.First().Path.AbsolutePath);
            }
            finally
            {
                if (path != null)
                {
                    BrowseLocalText.Text = "Successfully installed module.";
                    LocalModuleInstalled?.Invoke();
                    LibManager.Initialize();
                }
                else
                {
                    BrowseLocalText.Text = "Failed to install module. Check logs for more information.";
                }
            }
        };

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);

        this.DetachedFromVisualTree += OnDetachedFromVisualTree;
    }


    private void OnDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        var vm = DataContext as ModuleRegistryViewModel;
        vm.DetachedFromVisualTree();
        _dropOverlayService.Hide();
    }

    //private void InstallButton_Click(object? sender, RoutedEventArgs e)
    //{
    //    if (ModuleList.ItemCount == 0) return;
    //    var index = ModuleList.SelectedIndex;
    //    if (index == -1) index = 0;
    //    if (ModuleList.Items[index] is not InstallableTrackingModule module) return;

    //    InstallButton.Content = "Please Restart VRCFT";
    //    InstallButton.IsEnabled = false;
    //    OnModuleSelected(ModuleList, null);
    //}

    //private void ModuleSelectionTabChanged(object? sender, SelectionChangedEventArgs e)
    //{
    //    if (sender is not TabControl tabControl) return;

    //    var currentlySelectedItem = tabControl.SelectedContent;

    //    if (currentlySelectedItem is not Visual visual) return;

    //    var listBox = FindChild<ListBox>(visual);

    //    if (listBox == null) return;

    //    if (listBox.SelectedIndex == -1)
    //        listBox.SelectedIndex = 0;

    //    //OnModuleSelected(listBox, null);
    //}

    // Helper method to find a child control of a specific type
    //private T FindChild<T>(Visual parent) where T : Visual
    //{
    //    foreach (var child in parent.GetVisualChildren())
    //    {
    //        if (child is T result)
    //        {
    //            return result;
    //        }

    //        // Recursively search in child elements
    //        var foundChild = FindChild<T>(child);
    //        if (foundChild != null)
    //        {
    //            return foundChild;
    //        }
    //    }

    //    return null;
    //}

    //private void OnModuleSelected(object? sender, SelectionChangedEventArgs e)
    //{

    //    if(sender is not ListBox moduleListBox) return;
    //    if (moduleListBox.ItemCount == 0) return;

    //    var index = moduleListBox.SelectedIndex;
    //    if (index == -1) index = 0;
    //    if (moduleListBox.Items[index] is not InstallableTrackingModule module) return;

    //    switch (module.InstallationState)
    //    {
    //        case InstallState.NotInstalled or InstallState.Outdated:
    //        {
    //            InstallButton.Content = "Install";
    //            InstallButton.IsEnabled = true;
    //            break;
    //        }
    //        case InstallState.Installed:
    //        {
    //            InstallButton.Content = "Uninstall";
    //            InstallButton.IsEnabled = true;
    //            break;
    //        }
    //    }

    //    if (sender is ListBox listBox && listBox.SelectedItem is InstallableTrackingModule selectedModule)
    //    {
    //        ModuleSelected?.Invoke(selectedModule);
    //    }
    //}


    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        _dropOverlayService.Show();
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        _dropOverlayService.Hide();
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        _dropOverlayService.Hide();

        var vm = DataContext as ModuleRegistryViewModel;

        var items = e.Data.GetFiles();
        if (items == null) return;

        foreach (var file in items)
        {
            if (!file.Name.EndsWith(".zip"))
                continue;


            var res = await vm.InstallModule(file);
            if (res)
            {
                BrowseLocalText.Text = "Successfully installed module(s).";
                LocalModuleInstalled?.Invoke();
                LibManager.Initialize();
            }
            else
            {
                BrowseLocalText.Text = "Failed to install module(s). Check logs for more information.";
            }
        }
    }

}

