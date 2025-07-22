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

        // this should be moved to the ViewModel i think
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

        // TODO: this should also be moved to ViewModel
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

