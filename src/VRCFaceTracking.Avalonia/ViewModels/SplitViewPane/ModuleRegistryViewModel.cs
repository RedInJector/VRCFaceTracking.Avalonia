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
using Avalonia.Xaml.Interactions.Custom;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using RatingControlSample.Controls;
using VRCFaceTracking.Avalonia.Assets;
using VRCFaceTracking.Avalonia.Helpers;
using VRCFaceTracking.Avalonia.Models;
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
    private ProfileService _profileService { get; }

    public ModuleRegistryViewModel()
    {
        _moduleDataService = Ioc.Default.GetService<IModuleDataService>()!;
        _moduleInstaller = Ioc.Default.GetService<ModuleInstaller>()!;
        _libManager = Ioc.Default.GetService<ILibManager>()!;
        _profileService = Ioc.Default.GetService<ProfileService>()!;

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

        Dispatcher.UIThread.Post(async () =>
        {
            await _profileService.InitializeAsync();
            Profiles = new ObservableCollection<Profile>(_profileService.GetProfiles());
            SelectedProfile = Profiles.FirstOrDefault();
            OnPropertyChanged(nameof(Profiles));
        });
    }


    [ObservableProperty] private InstallableTrackingModule _selectedInstalledModule;
    [ObservableProperty] private InstallableTrackingModule _selectedInfoModule;
    [ObservableProperty] private int _selectedInstalledModuleRating;
    [ObservableProperty] private int _selectedInfoModuleRating;
    [ObservableProperty] private bool _requestReinit;
    [ObservableProperty] private string _searchText;
    [ObservableProperty] private bool _noRemoteModulesDetected;
    [ObservableProperty] private bool _modulesDetected;
    [ObservableProperty] private int _moduleRating;
    [ObservableProperty] private bool _dragItemDetected;
    [ObservableProperty] private int _selectedTabIndex;

    [ObservableProperty] private InstallableTrackingModule _selectedProfileInstalledModule;
    [ObservableProperty] private Profile _selectedProfile;
    [ObservableProperty] private string _selectedProfileName;
    [ObservableProperty] private string _selectedProfileNameError;
    public ObservableCollection<InstallableTrackingModule> FilteredModuleInfos { get; } = [];
    public ObservableCollection<InstallableTrackingModule> InstalledModules { get; set; } = [];
    public ObservableCollection<Profile> Profiles { get; set; } = [];
    public ObservableCollectionEx<InstallableTrackingModule> ModulesAvaliableForProfile { get; set; } = [];

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

    public int CorrectedModuleCount => Math.Max(0, InstalledModules.Count - 1);


    private bool IsModuleSameAs(InstallableTrackingModule val1, InstallableTrackingModule val2)
    {
        if (val1 == null || val2 == null) return false;

        return val1.ModuleName == val2.ModuleName
            && val1.AuthorName == val2.AuthorName
            && val1.ModuleId == val2.ModuleId;

    }
    private void RecalculateAvaliableModules()
    {
        ModulesAvaliableForProfile.Clear();
        if (SelectedProfile == null)
            return;
        if (InstalledModules == null)
            return;

        List<InstallableTrackingModule> avaliable = [];
        // This is just a fancy double for loop lol
        avaliable = InstalledModules.Where(m =>
                SelectedProfile.Modules.Where(s => IsModuleSameAs(m, s))
                .Count() == 0
            ).ToList();

        ModulesAvaliableForProfile.AddRange(avaliable);
    }

    partial void OnSelectedProfileNameChanged(string oldValue, string newValue)
    {
        if (newValue.Length < 1)
        {
            SelectedProfileNameError = "Can't be less than 1 char";
            return;
        }
        if (Profiles.Where(p => p.Name.Equals(newValue) && !p.Name.Equals(SelectedProfile.Name)).Any())
        {
            SelectedProfileNameError = "Already exists";
            return;
        }
        SelectedProfileNameError = "";
        SelectedProfile.Name = newValue;

        _profileService.SaveProfiles();
    }

    partial void OnSelectedProfileChanged(Profile value)
    {
        if (value != null)
            SelectedProfileName = value.Name;
        else
        {
            SelectedProfileNameError = null;
            SelectedProfileName = "";
        }

        RecalculateAvaliableModules();
    }
    private void RequestReinitialize()
    {
        RequestReinit = true;
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

    partial void OnSelectedInstalledModuleChanged(InstallableTrackingModule oldValue, InstallableTrackingModule newValue)
    {
        if (newValue == null)
            return;

        Dispatcher.UIThread.Post((Action)(async () =>
        {
            // Local modules don't have ratings 
            if (newValue.Local)
            {
                SelectedInstalledModuleRating = 0;
                ModuleRating = 0;
                return;
            }

            var myRating = await this._moduleDataService.GetMyRatingAsync(newValue);

            // Check if the selected module is still the same one that we called
            // GetMyRatingAsync for.
            if (SelectedInstalledModule?.ModuleId != newValue.ModuleId)
                return;

            if (myRating != null)
                SelectedInstalledModuleRating = (int)myRating;
            else
                SelectedInstalledModuleRating = 0;
        }));
    }

    partial void OnSelectedInfoModuleChanged(InstallableTrackingModule oldValue, InstallableTrackingModule newValue)
    {
        if (newValue == null)
            return;

        Dispatcher.UIThread.Post((Action)(async () =>
        {
            // Local modules don't have ratings 
            if (newValue.Local)
            {
                SelectedInfoModuleRating = 0;
                return;
            }

            var myRating = await this._moduleDataService.GetMyRatingAsync(newValue);

            // Check if the selected module is still the same one that we called
            // GetMyRatingAsync for.
            if (SelectedInfoModule?.ModuleId != newValue.ModuleId)
                return;

            if (myRating != null)
                SelectedInfoModuleRating = (int)myRating;
            else
                SelectedInfoModuleRating = 0;
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
        catch (Exception ex)
        {
            return false;
        }
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

    [RelayCommand]
    public void OpenUrl(string URL)
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

    [RelayCommand]
    public async Task RemoteModuleInstalledAsync(InstallableTrackingModule module)
    {
        switch (module.InstallationState)
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

    // A workaround for changing order without modifying the InstallableTrackingModule.cs
    [RelayCommand]
    public void DecrementOrder(InstallableTrackingModule module)
    {
        if (module != null)
        {
            module.Order--;
        }
    }

    [RelayCommand]
    public void IncrementOrder(InstallableTrackingModule module)
    {
        if (module != null)
        {
            module.Order++;
        }
    }

    [RelayCommand]
    public void BrowseLocal()
    {
        // i think this logic should be here but i'm not sure how to make it work
    }

    [RelayCommand]
    public void SetModuleRating(RatingControl.RatingCommandArgs args)
    {
        int rating = args.Rating;
        // should be safe to cast here
        InstallableTrackingModule module = (InstallableTrackingModule)args.Value;

        // Can't rate local modules
        if (module.Local)
            return;

        _moduleDataService.SetMyRatingAsync(module, rating);
    }

    [RelayCommand]
    public void AddNewProfile()
    {
        var p = new Profile();
        if (SelectedProfile == null)
            p.Name = "1";
        else
            p.Name = SelectedProfile.Name + "1";

        _profileService.AddProfile(p);
        Profiles.Add(p);

        SelectedProfile = p;

        _profileService.SaveProfiles();
    }

    [RelayCommand]
    public void RemoveProfile(Profile profile)
    {
        Profiles.Remove(profile);
        _profileService.RemoveProfile(profile);

        _profileService.SaveProfiles();
    }

    [RelayCommand]
    public void AddModuleToSelectedProfile(InstallableTrackingModule module)
    {
        SelectedProfile.Modules.Add(module);

        RecalculateAvaliableModules();
        _profileService.SaveProfiles();
    }
    [RelayCommand]
    public void RemoveModuleFromSelectedProfile(InstallableTrackingModule module)
    {
        SelectedProfile.Modules.Remove(module);


        RecalculateAvaliableModules();
        _profileService.SaveProfiles();
    }
}
