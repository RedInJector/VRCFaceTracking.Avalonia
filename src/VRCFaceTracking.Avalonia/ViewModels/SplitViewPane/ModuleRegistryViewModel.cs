using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using VRCFaceTracking.Avalonia.Assets;
using VRCFaceTracking.Avalonia.Services;
using VRCFaceTracking.Avalonia.Views;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Avalonia.ViewModels.SplitViewPane;

public partial class ModuleRegistryViewModel : ViewModelBase
{
    private InstallableTrackingModule[] _registryInfos;
    private IModuleDataService _moduleDataService { get; }
    private ModuleInstaller _moduleInstaller { get; }
    private ILibManager _libManager { get; }
    private SemaphoreSlim _moduleRatingLock = new(1);

    public ModuleRegistryViewModel()
    {
        _moduleDataService = Ioc.Default.GetService<IModuleDataService>()!;
        _moduleInstaller = Ioc.Default.GetService<ModuleInstaller>()!;
        _libManager = Ioc.Default.GetService<ILibManager>()!;

        ModuleRegistryView.LocalModuleInstalled += LocalModuleInstalled;
        //ModuleRegistryView.RemoteModuleInstalled += RemoteModuleInstalled;

        _registryInfos = GetRemoteModules();

        ResetInstalledModulesList(suppressReinit: true);

        InstalledModules.CollectionChanged += OnLocalModuleCollectionChanged;

        _noRemoteModulesDetected = _registryInfos == null;

        // Hide UI if the user has no remote modules (IE not internet connection) and no local modules
        _modulesDetected = !_noRemoteModulesDetected || InstalledModules.Count > 0;

        foreach (var module in _registryInfos)
        {
            FilteredModuleInfos.Add(module);
        }
        SelectedInstalledModule = InstalledModules.First();
        SelectedInfoModule = InstalledModules.First();
    }

    [NotifyPropertyChangedFor(nameof(InstallButtonText))]
    [NotifyPropertyChangedFor(nameof(InstallButtonActive))]
    [ObservableProperty] private InstallableTrackingModule _module;
    [ObservableProperty] private InstallableTrackingModule _selectedInstalledModule;
    [ObservableProperty] private InstallableTrackingModule _selectedInfoModule;
    [ObservableProperty] private bool _requestReinit;
    [ObservableProperty] private string _searchText;
    [ObservableProperty] private bool _noRemoteModulesDetected;
    [ObservableProperty] private bool _modulesDetected;
    [ObservableProperty] private int _moduleRating;
    [ObservableProperty] private bool _dragItemDetected;
    [ObservableProperty] private int _selectedTabIndex;
    public ObservableCollection<InstallableTrackingModule> FilteredModuleInfos { get; } = [];
    public ObservableCollection<InstallableTrackingModule> InstalledModules { get; set; } = [];

    public string InstallButtonText
    {
        get
        {
            if (Module == null)
                return Resources.InstallButton_Text_Install;

            return Module.InstallationState switch
            {
                InstallState.NotInstalled or InstallState.Outdated => Resources.InstallButton_Text_Install,
                InstallState.Installed => Resources.InstallButton_Text_Uninstall,
                InstallState.AwaitingRestart => Resources.InstallButton_Text_AwaitingRestart,
                _ => Resources.InstallButton_Text_Install,
            };
        }
    }
    public bool InstallButtonActive
    {
        get
        {
            if(Module == null)
                return false;
            if (Module.InstallationState == InstallState.AwaitingRestart)
                return false;

            return true;
        }
    }

    partial void OnSelectedTabIndexChanged(int oldValue, int newValue)
    {
        if (newValue == 0) 
            Module = SelectedInfoModule;
        if (newValue == 1)
            Module = SelectedInstalledModule;
    }
    partial void OnSelectedInfoModuleChanged(InstallableTrackingModule value)
    {
        if(SelectedTabIndex == 0)
            Module = value;
    }
    partial void OnSelectedInstalledModuleChanged(InstallableTrackingModule value)
    {
        if(SelectedTabIndex == 1)
            Module = value;
    }
    public InstallableTrackingModule[] GetRemoteModules()
    {
        IEnumerable<InstallableTrackingModule> remoteModules = [];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            Task.Run((Func<Task>)(async () =>
            {
                remoteModules = await this._moduleDataService.GetRemoteModules()
                    .ConfigureAwait(false);
            }), cts.Token).Wait(cts.Token);
        }
        catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
        {
            return null;
        }

        // Now comes the tricky bit, we get all locally installed modules and add them to the list.
        // If any of the IDs match a remote module and the other data contained within does not match,
        // then we need to set the local module install state to outdated. If everything matches then we need to set the install state to installed.
        var installedModules = _moduleDataService.GetInstalledModules();

        var localModules = new List<InstallableTrackingModule>();    // dw about it
        foreach (var installedModule in installedModules)
        {
            installedModule.InstallationState = InstallState.Installed;
            var remoteModule = remoteModules.FirstOrDefault(x => x.ModuleId == installedModule.ModuleId);
            if (remoteModule == null)   // If this module is completely missing from the remote list, then we need to add it to the list.
            {
                // This module is installed but not in the remote list, so we need to add it to the list.
                localModules.Add(installedModule);
            }
            else
            {
                // This module is installed and in the remote list, so we need to update the remote module's install state.
                remoteModule.InstallationState = remoteModule.Version != installedModule.Version ? InstallState.Outdated : InstallState.Installed;
            }
        }

        var remoteCount = remoteModules.Count();

        // Sort our data by name, then place dfg at the top of the list :3
        remoteModules = remoteModules.OrderByDescending(x => x.AuthorName == "dfgHiatus")
                                     .ThenBy(x => x.ModuleName);

        var modules = remoteModules.ToArray();
        var first = modules.First();

        return modules;
    }

    partial void OnModuleChanged(InstallableTrackingModule oldValue, InstallableTrackingModule newValue)
    {
        if (oldValue != null)
            oldValue.PropertyChanged -= OnModulePropertyChanged;

        if (newValue != null)
        {
            newValue.PropertyChanged += OnModulePropertyChanged;
        }
    }

    private void OnModulePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstallableTrackingModule.InstallationState))
        {
            OnPropertyChanged(nameof(InstallButtonText));
            OnPropertyChanged(nameof(InstallButtonActive));
        }
    }

    public int CorrectedModuleCount => Math.Max(0, InstalledModules.Count - 1);

    private void RequestReinitialize()
    {
        RequestReinit = true;
    }

    [RelayCommand]
    public void ModuleTryReinitialize()
    {
        if (RequestReinit)
        {
            RequestReinit = false;
            var installedModules = InstalledModules.ToList();
            _moduleDataService.SaveInstalledModulesDataAsync(installedModules);
            _libManager.TeardownAllAndResetAsync();
            _libManager.Initialize();
        }
    }

    private void ModuleRatingChanged(object? sender, PropertyChangedEventArgs args)
    {

        if (args.PropertyName != "ModuleRating")
            return;

        // Can't rate local modules
        if (Module.Local)
            return;

        _moduleDataService.SetMyRatingAsync(Module, ModuleRating);
    }

    partial void OnSearchTextChanged(string value)
    {
        UpdateFilteredModules();
    }

    private void UpdateFilteredModules()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _registryInfos
            : _registryInfos.Where(m =>
                m.ModuleName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                m.DllFileName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                m.AuthorName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                m.ModuleDescription.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase));

        FilteredModuleInfos.Clear();
        foreach (var module in filtered)
        {
            FilteredModuleInfos.Add(module);
        }
    }

    partial void OnModuleChanged(InstallableTrackingModule value)
    {
        if (value == null)
            return;

        Dispatcher.UIThread.Post((Action)(async () =>
        {
            // Local modules don't have ratings 
            if (value.Local)
            {
                ModuleRating = 0;
                return;
            }

            var myRating = await this._moduleDataService.GetMyRatingAsync(value);

            // We use a lock here because without it a race condition occurres
            // when adding propertyChanged handlers.
            // a handler might be added from another async call before this one
            // finishes causing unwanted events firing.
            await _moduleRatingLock.WaitAsync();
            try
            {
                // Check if the selected module is still the same one that we called
                // GetMyRatingAsync for.
                if (Module?.ModuleId != value.ModuleId)
                    return;

                // Prevent events from firing
                PropertyChanged -= ModuleRatingChanged;
                if (myRating != null)
                    ModuleRating = (int)myRating;
                else
                    ModuleRating = 0;

                // return the events back
                PropertyChanged += ModuleRatingChanged;
            }
            finally
            {
                _moduleRatingLock.Release();
            }
        }));
    }

    private void LocalModuleInstalled()
    {
        _registryInfos = GetRemoteModules();

        ResetInstalledModulesList();

        FilteredModuleInfos.Clear();
        foreach (var module in _registryInfos)
        {
            FilteredModuleInfos.Add(module);
        }

        ModulesDetected = true;
    }

    public async void OnLocalModuleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InstalledModules.CollectionChanged -= OnLocalModuleCollectionChanged;

        RenumberModules();

        try
        {
            var installedModules = InstalledModules.ToList(); // Create a copy to avoid modification during save
            await _moduleDataService.SaveInstalledModulesDataAsync(installedModules);
            RequestReinitialize();
        }
        finally
        {
            // Re-enable the event handler
            InstalledModules.CollectionChanged += OnLocalModuleCollectionChanged;
        }
    }

    [RelayCommand]
    public async Task RemoteModuleInstalledAsync(InstallableTrackingModule module)
    {
        switch (Module.InstallationState)
        {
            case InstallState.NotInstalled or InstallState.Outdated:
            {
                var path = await _moduleInstaller.InstallRemoteModule(module);
                if (!string.IsNullOrEmpty(path))
                {
                    module!.InstallationState = InstallState.Installed;
                    // TODO: Uncomment
                    //await _moduleDataService.IncrementDownloadsAsync(module);
                    module!.Downloads++;
                }
                break;
            }
            case InstallState.Installed:
            {
                _moduleInstaller.MarkModuleForDeletion(module);
                break;
            }
        }
        ResetInstalledModulesList();
    }

    private void ResetInstalledModulesList(bool suppressReinit = false)
    {
        var installedModules = _moduleDataService.GetInstalledModules()
                                                 .Where(m => m.InstallationState != InstallState.AwaitingRestart);

        InstalledModules.Clear();
        int i = 0;
        foreach (var installedModule in installedModules)
        {
            installedModule.PropertyChanged += OnLocalModulePropertyChanged;
            InstalledModules.Add(installedModule);
        }
        RenumberModules();
        if (!suppressReinit)
            RequestReinitialize();
    }

    private async void OnLocalModulePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is not InstallableTrackingModule module)
            return;

        var desiredIndex = module.Order;
        var currentIndex = InstalledModules.IndexOf(module);

        if (desiredIndex >= 0 && desiredIndex < InstalledModules.Count)
            InstalledModules.Move(currentIndex, desiredIndex);

        RenumberModules();

        var _installedModules = InstalledModules.ToList();
        await _moduleDataService.SaveInstalledModulesDataAsync(_installedModules);
    }

    private void RenumberModules()
    {
        for (int i = 0; i < InstalledModules.Count; i++)
        {
            InstalledModules[i].Order = i;
        }
    }

    public void OpenModuleUrl()
    {
        OpenUrl(_module.ModulePageUrl);
    }

    private void OpenUrl(string URL)
    {
        try
        {
            Process.Start(URL);
        }
        catch
        {

            var url = URL.Replace("&", "^&");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    public void DetachedFromVisualTree()
    {
        _moduleDataService.SaveInstalledModulesDataAsync(InstalledModules);

        ModuleTryReinitialize();
    }

    public async Task<bool> InstallModule(IStorageItem file)
    {
        string path = string.Empty;
        try
        {
            path = await _moduleInstaller.InstallLocalModule(file.Path.AbsolutePath);
            return !string.IsNullOrEmpty(path);
        }
        catch (Exception ex) {
            return false;
        }
    }

    // A workaround for changing order without modifying the InstallableTrackingModule.cs
    [RelayCommand]
    public void DecrementOrder(InstallableTrackingModule module)
    {
        if (module != null) {
            module.Order--;
        }
    }
    [RelayCommand]
    public void IncrementOrder(InstallableTrackingModule module)
    {
        if (module != null) {
            module.Order++;
        }
    }

    [RelayCommand]
    public void InstallModule(InstallableTrackingModule module)
    {

    }

    [RelayCommand]
    public void BrowseLocal()
    {

    }

    [RelayCommand]
    public void ReinitializeModules()
    {

    }
    [RelayCommand]
    public void SelectModule()
    {

    }
}
