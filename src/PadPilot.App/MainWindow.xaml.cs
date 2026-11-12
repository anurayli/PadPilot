using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PadPilot.Core;

namespace PadPilot.App;

public partial class MainWindow : Window
{
    readonly PadManager _manager = new();
    readonly XboxBridge? _bridge;
    readonly ObservableCollection<PadViewModel> _pads = [];
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };

    public MainWindow()
    {
        InitializeComponent();
        PadList.ItemsSource = _pads;
        try
        {
            _bridge = new XboxBridge();
            StatusText.Text = "Hazır. Bağlanan pad otomatik tanınır ve oyunlara Xbox 360 pad'i olarak sunulur.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "ViGEmBus sürücüsüne bağlanılamadı; emülasyon kapalı, yalnızca izleme çalışır. (" + ex.Message + ")";
        }

        _manager.PadAdded += pad => Dispatcher.BeginInvoke(() =>
        {
            var vm = new PadViewModel(pad, _bridge);
            _pads.Add(vm);
            vm.IsEmulating = true;
        });
        _manager.PadRemoved += pad => Dispatcher.BeginInvoke(() =>
        {
            var vm = _pads.FirstOrDefault(p => p.Device == pad);
            if (vm == null) return;
            vm.IsEmulating = false;
            _pads.Remove(vm);
        });
        _timer.Tick += (_, _) =>
        {
            foreach (var vm in _pads) vm.Refresh();
            EmptyText.Visibility = _pads.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        };
        _timer.Start();
        _manager.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _bridge?.Dispose();
        _manager.Dispose();
        base.OnClosed(e);
    }

    async void Rumble_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not PadViewModel vm) return;
        vm.Device.SetRumble(200, 200);
        await Task.Delay(400);
        vm.Device.SetRumble(0, 0);
    }

    void Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PadViewModel vm, Tag: string hex }) return;
        var c = (Color)ColorConverter.ConvertFromString(hex);
        vm.Device.SetLightbar(c.R, c.G, c.B);
    }
}

public sealed class PadViewModel(PadDevice device, XboxBridge? bridge) : INotifyPropertyChanged
{
    PadState _s;
    bool _emulating;

    public PadDevice Device { get; } = device;
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name => Device.DisplayName;
    public string Info =>
        $"{(Device.Bluetooth ? "Bluetooth" : "USB")} · {Device.ReportRateHz:0} Hz" +
        (_s.FullReport ? "" : " · basit mod (pil ve sensör verisi yok)");
    public string BatteryText => _s.FullReport ? $"Pil %{_s.BatteryPercent}{(_s.Charging ? " · şarjda" : "")}" : "";
    public double LeftDotX => _s.LX / 255.0 * 62;
    public double LeftDotY => _s.LY / 255.0 * 62;
    public double RightDotX => _s.RX / 255.0 * 62;
    public double RightDotY => _s.RY / 255.0 * 62;
    public double L2 => _s.L2;
    public double R2 => _s.R2;
    public string ButtonsText => _s.Buttons == PadButtons.None ? "Basılı tuş yok" : _s.Buttons.ToString();
    public string OutputStatus => Device.OutputStatus;

    public bool IsEmulating
    {
        get => _emulating;
        set
        {
            if (value == _emulating || bridge == null) return;
            try
            {
                if (value) bridge.Attach(Device); else bridge.Detach(Device);
                _emulating = value;
            }
            catch { /* sanal pad olusturulamadi; kutu isaretsiz kalir */ }
        }
    }

    public void Refresh()
    {
        _s = Device.State;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
