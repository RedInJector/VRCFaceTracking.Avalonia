using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRCFaceTracking.Avalonia.Services
{
    public class DropOverlayService
    {
        public event Action<bool> ShowOverlayChanged;

        public void Show()
        {
            ShowOverlayChanged?.Invoke(true);
        }
        public void Hide()
        {
            ShowOverlayChanged?.Invoke(false);
        }

    }
}
