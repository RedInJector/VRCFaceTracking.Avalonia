using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRCFaceTracking.Avalonia.Models;
using VRCFaceTracking.Avalonia.Services;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Services;

namespace VRCFaceTracking.Avalonia.Tests
{
    [TestClass]
    public class ProfileServiceTest
    {
        private Mock<ILocalSettingsService> _mockSettingsService;
        private Mock<IModuleDataService> _mockModuleDataService;
        private ProfileService _profileService;


        [TestInitialize]
        public void Setup()
        {
            _mockSettingsService = new Mock<ILocalSettingsService>();
            _mockModuleDataService = new Mock<IModuleDataService>();
            _profileService = new ProfileService(_mockSettingsService.Object, _mockModuleDataService.Object);
        }

        [TestMethod]
        public async Task LoadProfilesAsyncTest_Single()
        {
            var testModuleId = Guid.NewGuid();

            var savedProfiles = new List<SavedProfile>
            {
                new SavedProfile{ Name = "TestProfile", modules = new List<Guid>{ testModuleId } }
            };

            _mockSettingsService
                .Setup(s => s.ReadSettingAsync<List<SavedProfile>>("profiles", null, false))
                .ReturnsAsync(savedProfiles);

            var installedModule = new InstallableTrackingModule();
            installedModule.ModuleId = testModuleId;
            installedModule.ModuleName = "TestModule";

            _mockModuleDataService
                .Setup(m => m.GetInstalledModules())
                .Returns(new List<InstallableTrackingModule> { installedModule });

            var result = await _profileService.InitializeAsync();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(true, result.First().CanLoad);
            Assert.AreEqual(1, result.First().Modules.Count);
            Assert.AreEqual("TestModule", result.First().Modules.First().ModuleName);
        }

        [TestMethod]
        public async Task LoadProfilesAsyncTest_Multiple()
        {
            var testModuleId1 = Guid.NewGuid();
            var testModuleId2 = Guid.NewGuid();
            var testModuleId3 = Guid.NewGuid();

            var savedProfiles = new List<SavedProfile>
            {
                new SavedProfile{ Name = "TestProfile1", modules = new List<Guid>{ testModuleId1 } },
                new SavedProfile{ Name = "TestProfile2", modules = new List<Guid>{ testModuleId1, testModuleId2 } }
            };

            _mockSettingsService
                .Setup(s => s.ReadSettingAsync<List<SavedProfile>>("profiles", null, false))
                .ReturnsAsync(savedProfiles);

            var installedModule1 = new InstallableTrackingModule();
            installedModule1.ModuleId = testModuleId1;
            installedModule1.ModuleName = "TestModule1";

            var installedModule2 = new InstallableTrackingModule();
            installedModule2.ModuleId = testModuleId2;
            installedModule2.ModuleName = "TestModule2";

            var installedModule3 = new InstallableTrackingModule();
            installedModule3.ModuleId = testModuleId3;
            installedModule3.ModuleName = "TestModule3";

            _mockModuleDataService
                .Setup(m => m.GetInstalledModules())
                .Returns(new List<InstallableTrackingModule> { installedModule1, installedModule2, installedModule3 });

            var result = await _profileService.InitializeAsync();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(true, result.First().CanLoad);
            Assert.AreEqual(true, result[1].CanLoad);
            Assert.AreEqual(1, result.First().Modules.Count);
            Assert.AreEqual(2, result[1].Modules.Count);
            Assert.AreEqual("TestModule1", result.First().Modules.First().ModuleName);
            Assert.AreEqual("TestModule1", result[1].Modules.First().ModuleName);
            Assert.AreEqual("TestModule2", result[1].Modules[1].ModuleName);
        }
    }
}
