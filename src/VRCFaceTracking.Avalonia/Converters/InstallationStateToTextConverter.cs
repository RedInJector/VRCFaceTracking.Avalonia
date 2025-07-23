using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRCFaceTracking.Avalonia.Assets;
using VRCFaceTracking.Core.Models;

namespace VRCFaceTracking.Avalonia.Converters
{
    public class InstallationStateToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                InstallState.NotInstalled => Resources.InstallButton_Text_Install,
                InstallState.Outdated => Resources.InstallButton_Text_Install,
                InstallState.Installed => Resources.InstallButton_Text_Uninstall,
                InstallState.AwaitingRestart => Resources.InstallButton_Text_AwaitingRestart,
                _ => "Error"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
