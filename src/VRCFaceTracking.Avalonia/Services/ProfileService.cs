using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRCFaceTracking.Avalonia.Helpers;
using VRCFaceTracking.Avalonia.Models;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Services;

namespace VRCFaceTracking.Avalonia.Services
{
    public class ProfileService
    {
        private ILogger<ProfileService> _logger;
        private IModuleDataService _moduleDataService { get; }
        private ILibManager _libManager { get; }
        private ILocalSettingsService _settingService;
        private List<Profile> _profiles;
        private bool _hasChanges;
        public bool HasUnsavedChanges => _hasChanges;

        private Profile _activeProfile = null;
        private Task _initializationTask;

        private readonly SemaphoreSlim _applyProfileLock = new SemaphoreSlim(1, 1);

        public ProfileService(
            ILogger<ProfileService> logger,
            ILocalSettingsService settingService,
            IModuleDataService moduleDataService,
            ILibManager libManager
            )
        {
            _logger = logger;
            _settingService = settingService;
            _moduleDataService = moduleDataService;
            _libManager = libManager;

            _initializationTask = InitializeAsync();
        }

        private void MarkDirty() => _hasChanges = true;
        private void MarkClean() => _hasChanges = false;

        private void Track(Profile profile)
        {
            profile.PropertyChanged += (s, e) =>
            {
                MarkDirty();
            };

            if (profile.Modules is INotifyCollectionChanged notifyCollection)
            {
                notifyCollection.CollectionChanged += (s, e) => MarkDirty();
            }
        }
        public void AddProfile(Profile profile)
        {
            if (!_profiles.Contains(profile))
            {
                _profiles.Add(profile);
                Track(profile);
                MarkDirty();

                _logger.LogInformation("Profile \"{}\" added", profile.Name);
            }
        }
        public void RemoveProfile(Profile profile)
        {
            if (_profiles.Contains(profile))
            {
                _profiles.Remove(profile);
                MarkDirty();
                _logger.LogInformation("Profile \"{}\" removed", profile.Name);
            }
        }
        public async Task ApplyProfile(Profile profile)
        {
            // TeardownAllAndResetAsync() isn't safe to call concurrently
            // thats why we have a lock here
            await _applyProfileLock.WaitAsync();
            try
            {
                // the Initialize() method blocks
                // the callers' thread because thats how async works in c#
                // and thats why it's wrapped in a task here
                await Task.Run(async () =>
                {
                    await _initializationTask;

                    if (!_profiles.Contains(profile))
                    {
                        AddProfile(profile);
                    }
                    var modules = _moduleDataService.GetInstalledModules();
                    foreach (var module in modules)
                    {
                        module.Instantiatable = false;
                        module.Order = 0;
                    }

                    for (int i = 0; i < profile.Modules.Count; i++)
                    {
                        var obj = profile.Modules[i];
                        var match = modules.FirstOrDefault(x => x.DllFileName == obj.DllFileName);

                        if (match != null)
                        {
                            match.Order = i;
                            match.Instantiatable = true;
                        }
                    }

                    await _moduleDataService.SaveInstalledModulesDataAsync(modules);
                    // this function isn't async despite the name
                    _libManager.TeardownAllAndResetAsync();
                    _libManager.Initialize();
                    _activeProfile = profile;
                    _logger.LogInformation("Setting active profile to \"{}\"", profile.Name);
                });
            }
            finally
            {
                _applyProfileLock.Release();
            }
        }
        public async Task<List<Profile>> GetProfilesAsync()
        {
            await _initializationTask;
            return _profiles;
        }
        public async Task<Profile> GetActiveProfile()
        {
            await _initializationTask;
            return _activeProfile;
        }
        private async Task InitializeAsync()
        {
            
            _logger.LogInformation("Initializing");
            _profiles = new();

            var activeProfileName = await _settingService.ReadSettingAsync<string>("active_profile", null, false);
            var loadedProfiles = await _settingService.ReadSettingAsync<List<SavedProfile>>("profiles", null, false);
            if (loadedProfiles == null)
                return;

            var installedModules = _moduleDataService.GetInstalledModules();
            var installedModuleMap = installedModules.ToDictionary(m => m.DllFileName);

            foreach (var saved in loadedProfiles)
            {
                var matchedModules = saved.modules
                    .Where(installedModuleMap.ContainsKey)
                    .Select(filename => installedModuleMap[filename])
                    .ToList();

                if (matchedModules.Count == saved.modules.Count)
                {
                    _profiles.Add(new Profile
                    {
                        Name = saved.Name,
                        Modules = new System.Collections.ObjectModel.ObservableCollection<InstallableTrackingModule>(matchedModules),
                        CanLoad = true
                    });
                }
            }

            if (activeProfileName != null)
                foreach (var profile in _profiles)
                {
                    if (profile.Name == activeProfileName)
                    {
                        _activeProfile = profile;
                        break;
                    }
                }

            MarkClean();
        }
        public async Task SaveProfiles()
        {
            _logger.LogInformation("Saving profiles");
            List<SavedProfile> toSave = new();
            foreach (var profile in _profiles)
            {
                SavedProfile p = new();
                p.Name = profile.Name;
                foreach (var module in profile.Modules)
                {
                    p.modules.Add(module.DllFileName);
                }
                toSave.Add(p);
            }

            await _settingService.SaveSettingAsync("profiles", toSave);
            if(_activeProfile != null)
                await _settingService.SaveSettingAsync("active_profile", _activeProfile.Name);

            MarkClean();
        }
    }
}
