using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using VRCFaceTracking.Avalonia.Models;
using VRCFaceTracking.Avalonia.Services;
using VRCFaceTracking.Avalonia.Views;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.OSC;
using VRCFaceTracking.Core.Params;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Avalonia.ViewModels.SplitViewPane;

public partial class HomePageViewModel : ViewModelBase
{
    public ILibManager LibManager { get; }
    public IOscTarget OscTarget { get; }
    private IModuleDataService ModuleDataService { get; }
    private ProfileService _profileService { get; }
    private OscRecvService OscRecvService { get; }
    private OscSendService OscSendService { get; }

    [ObservableProperty] private OscQueryService parameterOutputService;

    private int _messagesRecvd;
    [ObservableProperty] private string _messagesInPerSecCount;

    private int _messagesSent;
    [ObservableProperty] private string _messagesOutPerSecCount;

    [ObservableProperty] private bool _noModulesInstalled;

    [ObservableProperty] private bool _oscWasDisabled;

    [ObservableProperty] private int _currentParametersCount;

    [ObservableProperty] private int _legacyParametersCount;

    [ObservableProperty] private bool _isLegacyAvatar;

    [ObservableProperty] private bool _isTestAvatar;
    [ObservableProperty] private Profile _selectedProfile = null;

    public ObservableCollection<ModuleMetadata> LoadedModulesMetadata { get; set; }
    public ObservableCollection<Profile> Profiles { get; set; }


    private readonly DispatcherTimer _msgCounterTimer;

    public IRelayCommand NavigateToModulesCommand { get; }
    public IRelayCommand DismissWarningCommand { get; }

    public HomePageViewModel()
    {
        // Commands
        NavigateToModulesCommand = new RelayCommand(ExecuteNavigateToModules);
        DismissWarningCommand = new RelayCommand(ExecuteDismissWarning);

        // Services
        LibManager = Ioc.Default.GetService<ILibManager>()!;
        ParameterOutputService = Ioc.Default.GetService<OscQueryService>()!;
        ModuleDataService = Ioc.Default.GetService<IModuleDataService>()!;
        OscTarget = Ioc.Default.GetService<IOscTarget>()!;
        OscRecvService = Ioc.Default.GetService<OscRecvService>()!;
        OscSendService = Ioc.Default.GetService<OscSendService>()!;
        _profileService = Ioc.Default.GetService<ProfileService>()!;

        // Modules
        var installedNewModules = ModuleDataService.GetInstalledModules();
        NoModulesInstalled = !installedNewModules.Any();

        // Message Timer
        MessagesInPerSecCount = "0";
        MessagesOutPerSecCount = "0";
        OscRecvService.OnMessageReceived += MessageReceived;
        OscSendService.OnMessagesDispatched += MessageDispatched;
        _msgCounterTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _msgCounterTimer.Tick += (_, _) =>
        {
            MessagesInPerSecCount = _messagesRecvd.ToString();
            _messagesRecvd = 0;

            MessagesOutPerSecCount = _messagesSent.ToString();
            _messagesSent = 0;

            CurrentParametersCount = ParameterOutputService.AvatarParameters?.Count ?? 0;
            LegacyParametersCount = ParameterOutputService.AvatarParameters?.Count(p => p.Deprecated) ?? 0;
            IsLegacyAvatar = LegacyParametersCount > 0;
            IsTestAvatar = ParameterOutputService.AvatarInfo?.Id.StartsWith("local:") ?? false;
        };
        _msgCounterTimer.Start();

        LoadedModulesMetadata = LibManager.LoadedModulesMetadata;

        Dispatcher.UIThread.Post(async () =>
        {
            Profiles = new ObservableCollection<Profile>(await _profileService.GetProfilesAsync());
            OnPropertyChanged(nameof(Profiles));
            SelectedProfile = await _profileService.GetActiveProfile();
        });
    }

    private void MessageReceived(OscMessage msg) => _messagesRecvd++;
    private void MessageDispatched(int msgCount) => _messagesSent += msgCount;

    ~HomePageViewModel()
    {
        OscRecvService.OnMessageReceived -= MessageReceived;
        OscSendService.OnMessagesDispatched -= MessageDispatched;

        _msgCounterTimer.Stop();
    }

    private void ExecuteNavigateToModules()
    {
        var mainViewModel = Ioc.Default.GetRequiredService<MainViewModel>()!;
        mainViewModel.SelectedListItem = mainViewModel.Items[2]; // Module Page
    }

    private void ExecuteDismissWarning()
    {
        // Maybe make this show only once, as opposed to everytime you open this page?
        NoModulesInstalled = false;
    }

    partial void OnSelectedProfileChanged(Profile value)
    {

        Dispatcher.UIThread.Post(async () =>
        {
            if (await _profileService.GetActiveProfile() == value)
                return;

            LoadedModulesMetadata = null;
            OnPropertyChanged(nameof(LoadedModulesMetadata));

            await _profileService.ApplyProfile(value);
            LoadedModulesMetadata = LibManager.LoadedModulesMetadata;
            OnPropertyChanged(nameof(LoadedModulesMetadata));
        });
    }
}
