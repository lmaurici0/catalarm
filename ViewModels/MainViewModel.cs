﻿using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MauiCatAlarm.Platforms.Android;
using MauiCatAlarm.Services;

namespace MauiCatAlarm.ViewModels;

public class MainViewModel : ObservableObject, IDisposable
{
    private readonly AlarmService _alarmService;
    private readonly Func<AlarmPage> _alarmPageFactory;
    private TimeSpan _alarmTime;

    public MainViewModel(AlarmService alarmService, Func<AlarmPage> alarmPageFactory)
    {
        _alarmService = alarmService;
        _alarmPageFactory = alarmPageFactory;

        _alarmService.IsEnabledChanged += AlarmService_IsEnabledChanged;
        _alarmService.ScheduledTimeChanged += AlarmService_ScheduledTimeChanged;
        _alarmService.RingtoneChanged += AlarmService_RingtoneChanged;
        App.Current.PropertyChanged += App_PropertyChanged;

        AlarmTime = _alarmService.GetScheduledTime() ?? new TimeSpan(9, 0, 0);
        ToggleAlarmCommand = new AsyncRelayCommand(ToggleAlarmAsync);
        NavigateToAlarmCommand = new RelayCommand(NavigateToAlarm);
        UpdateAlarmRingtoneCommand = new AsyncRelayCommand(UpdateAlarmRingtoneAsync);

        App.Current.Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
        {
            OnPropertyChanged(nameof(CurrentTime));
            OnPropertyChanged(nameof(CurrentWeekday));
            OnPropertyChanged(nameof(CurrentMonth));
            OnPropertyChanged(nameof(CurrentDayNumber));
            OnPropertyChanged(nameof(EnabledAlarmLabel));
            return true;
        });
    }

    public string CurrentTime => DateTime.Now.ToString("T");

    public string CurrentWeekday => DateTime.Now.ToString("dddd", new CultureInfo("pt-BR"));

    public string CurrentMonth => DateTime.Now.ToString("MMM", new CultureInfo("pt-BR"));

    public string CurrentDayNumber => DateTime.Now.ToString("dd");

    public string ToggleAlarmText => IsAlarmSet ? "Desligar alarme" : "Programar alarme";

    public string EnabledAlarmLabel => FormatAlarmText();

    public bool IsAlarmSet => _alarmService.IsSet();

    public bool IsAlarmUnset => !IsAlarmSet;

    public bool IsAlarmOngoing { get; private set; }

    public ICommand ToggleAlarmCommand { get; }

    public ICommand NavigateToAlarmCommand { get; }

    public ICommand UpdateAlarmRingtoneCommand { get; }

    public TimeSpan AlarmTime
    {
        get => _alarmTime;
        set => SetProperty(ref _alarmTime, value);
    }

    public string AlarmRingtoneName => _alarmService.GetAlarmRingtoneName();

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _alarmService.IsEnabledChanged -= AlarmService_IsEnabledChanged;
            _alarmService.ScheduledTimeChanged -= AlarmService_ScheduledTimeChanged;
            App.Current.PropertyChanged -= App_PropertyChanged;
        }
    }

    protected virtual async Task ToggleAlarmAsync()
    {
        var status = await Permissions.CheckStatusAsync<PostNotificationsPermission>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<PostNotificationsPermission>();
            if (status != PermissionStatus.Granted)
            {
                if (App.Current.MainPage != null)
                {
                    await App.Current.MainPage.DisplayAlert(
                        "Permissão Requirida",
                        "Você precisa ativar a permissão de notificação",
                        "OK");
                }
                return;
            }
        }

        if (_alarmService.IsSet())
        {
            _alarmService.DeleteAlarm();
        }
        else
        {
            _alarmService.SetAlarm(AlarmTime);
        }
    }

    protected virtual void NavigateToAlarm()
    {
        var alarmPage = _alarmPageFactory();
        App.Current.MainPage = alarmPage;
    }

    private void AlarmService_RingtoneChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(AlarmRingtoneName));
    }

    private void AlarmService_ScheduledTimeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(EnabledAlarmLabel));
    }

    private void AlarmService_IsEnabledChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsAlarmSet));
        OnPropertyChanged(nameof(IsAlarmUnset));
        OnPropertyChanged(nameof(ToggleAlarmText));
    }

    private void App_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(App.IsAlarmActive))
        {
            IsAlarmOngoing = App.Current.IsAlarmActive;
            OnPropertyChanged(nameof(IsAlarmOngoing));
        }
    }

    private string FormatAlarmText()
    {
        var nextOccurrence = NextAlarm();
        if (nextOccurrence == null)
        {
            return $"Zzzzzzzz…";
        }

        if (nextOccurrence.Value.Date > DateTime.Today)
        {
            return $"Você vai acordar as {nextOccurrence.Value:t} amanhã.";
        }

        return $"Você será acordado as {nextOccurrence.Value:t}.";
    }

    private DateTime? NextAlarm()
    {
        var scheduledTime = _alarmService.GetScheduledTime();
        if (scheduledTime == null)
            return null;

        var nextOccurence = DateTime.Today
            .AddHours(scheduledTime.Value.Hours)
            .AddMinutes(scheduledTime.Value.Minutes);

        if (nextOccurence < DateTime.Now)
            nextOccurence = nextOccurence.AddDays(1);

        return nextOccurence;
    }

    private Task UpdateAlarmRingtoneAsync()
    {
        _alarmService.PickAlarmRingtone();
        return Task.CompletedTask;
    }
}
