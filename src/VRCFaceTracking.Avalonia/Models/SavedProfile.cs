using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRCFaceTracking.Avalonia.Models
{
    public class SavedProfile
    {
        public string Name { get; set; }
        public List<Guid> modules { get; set; } = new();
        public SavedProfile() { }
        public SavedProfile(Profile profile)
        {
            Name = profile.Name;
            foreach (var module in profile.Modules) {
                 modules.Add(module.ModuleId);
            }
        }
    }
}
