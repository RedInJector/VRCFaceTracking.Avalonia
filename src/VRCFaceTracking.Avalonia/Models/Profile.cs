using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRCFaceTracking.Core.Models;

namespace VRCFaceTracking.Avalonia.Models
{
    public partial class Profile : ObservableObject
    {
        [ObservableProperty] private bool _canLoad = false;
        [ObservableProperty] private string _name = "";
        public ObservableCollection<InstallableTrackingModule> Modules { get; set; } = [];
        public ObservableCollection<Guid> notFoundModules { get; set; } = [];
        public Profile() { }
    }
}
