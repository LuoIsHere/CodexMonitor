using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;

namespace LuoIsHere.CodexMonitor.App.ViewModels;

public sealed class FloatingWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MainWindowViewModel _source;
    private FloatingWindowDisplaySettings _display;
    private bool _disposed;

    public FloatingWindowViewModel(
        MainWindowViewModel source,
        FloatingWindowDisplaySettings display)
    {
        _source = source;
        _display = display;
        _source.PropertyChanged += OnSourcePropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FiveHourPercent => _source.FiveHourPercent;

    public string WeeklyPercent => _source.WeeklyPercent;

    public string FiveHourResetTime => _source.FiveHourResetTime;

    public string WeeklyResetTime => _source.WeeklyResetTime;

    public string AccountText => _source.AccountText;

    public string LastRefreshText => _source.LastRefreshText;

    public Visibility FiveHourVisibility => ToVisibility(_display.ShowFiveHourQuota);

    public Visibility WeeklyVisibility => ToVisibility(_display.ShowWeeklyQuota);

    public Visibility LastRefreshVisibility => ToVisibility(_display.ShowLastRefreshTime);

    public Visibility ResetTimesVisibility => ToVisibility(_display.ShowResetTimes);

    public Visibility SubscriptionVisibility => ToVisibility(_display.ShowSubscription);

    public Visibility FirstDividerVisibility => ToVisibility(
        _display.ShowFiveHourQuota &&
        (_display.ShowWeeklyQuota || _display.ShowLastRefreshTime));

    public Visibility SecondDividerVisibility => ToVisibility(
        _display.ShowWeeklyQuota && _display.ShowLastRefreshTime);

    public void ApplyDisplaySettings(FloatingWindowDisplaySettings display)
    {
        _display = display;
        OnPropertyChanged(nameof(FiveHourVisibility));
        OnPropertyChanged(nameof(WeeklyVisibility));
        OnPropertyChanged(nameof(LastRefreshVisibility));
        OnPropertyChanged(nameof(ResetTimesVisibility));
        OnPropertyChanged(nameof(SubscriptionVisibility));
        OnPropertyChanged(nameof(FirstDividerVisibility));
        OnPropertyChanged(nameof(SecondDividerVisibility));
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var floatingProperty = e.PropertyName switch
        {
            nameof(MainWindowViewModel.FiveHourPercent) => nameof(FiveHourPercent),
            nameof(MainWindowViewModel.WeeklyPercent) => nameof(WeeklyPercent),
            nameof(MainWindowViewModel.FiveHourResetTime) => nameof(FiveHourResetTime),
            nameof(MainWindowViewModel.WeeklyResetTime) => nameof(WeeklyResetTime),
            nameof(MainWindowViewModel.AccountText) => nameof(AccountText),
            nameof(MainWindowViewModel.LastRefreshText) => nameof(LastRefreshText),
            _ => null,
        };

        if (floatingProperty is not null)
        {
            OnPropertyChanged(floatingProperty);
        }
    }

    private static Visibility ToVisibility(bool value)
        => value ? Visibility.Visible : Visibility.Collapsed;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _source.PropertyChanged -= OnSourcePropertyChanged;
    }
}
