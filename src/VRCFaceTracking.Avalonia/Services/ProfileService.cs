using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRCFaceTracking.Avalonia.Models;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Services;

namespace VRCFaceTracking.Avalonia.Services
{
    public class ProfileService
    {
        private IModuleDataService _moduleDataService { get; }
        private ILocalSettingsService _settingService;
        private List<Profile> _profiles;

        private bool _hasChanges;

        public bool HasUnsavedChanges => _hasChanges;


        public ProfileService(ILocalSettingsService settingService, IModuleDataService moduleDataService)
        {
            _settingService = settingService;
            _moduleDataService = moduleDataService;
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
            }
        }
        public void RemoveProfile(Profile profile)
        {
            if (_profiles.Contains(profile))
            {
                _profiles.Remove(profile);
                MarkDirty();
            }
        }


        public List<Profile> GetProfiles()
        {
            return _profiles;
        }
        public async Task InitializeAsync()
        {
            _profiles = new();

            var loadedProfiles = await _settingService.ReadSettingAsync<List<SavedProfile>>("profiles", null, false);
            if (loadedProfiles == null)
                return; 

            var installedModules = _moduleDataService.GetInstalledModules();
            var installedModuleMap = installedModules.ToDictionary(m => m.ModuleId);

            foreach (var saved in loadedProfiles)
            {
                var matchedModules = saved.modules
                    .Where(installedModuleMap.ContainsKey)
                    .Select(id => installedModuleMap[id])
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

            MarkClean();
        }
        public async Task SaveProfiles()
        {
            List<SavedProfile> toSave = new();
            foreach (var profile in _profiles)
            {
                SavedProfile p = new();
                p.Name = profile.Name;
                foreach (var module in profile.Modules)
                {
                    p.modules.Add(module.ModuleId);
                }
                toSave.Add(p);
            }

            await _settingService.SaveSettingAsync("profiles", toSave);

            MarkClean();
        }
    }
}
