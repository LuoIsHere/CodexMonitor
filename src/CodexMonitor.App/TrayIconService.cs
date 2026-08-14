using System.Drawing;
using Forms = System.Windows.Forms;

namespace LuoIsHere.CodexMonitor.App;

public sealed class TrayIconService : IDisposable
{
    private readonly Icon _applicationIcon;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _contextMenu;
    private readonly Forms.ToolStripMenuItem _minimizeItem;
    private readonly Forms.ToolStripMenuItem _floatingWindowItem;
    private readonly Forms.ToolStripMenuItem _floatingWindowLockItem;
    private readonly IFloatingWindowService _floatingWindowService;
    private bool _backgroundNotificationShown;
    private bool _disposed;

    public TrayIconService(IFloatingWindowService floatingWindowService)
    {
        _floatingWindowService = floatingWindowService;
        _applicationIcon = LoadApplicationIcon();

        _minimizeItem = new Forms.ToolStripMenuItem("最小化");
        _minimizeItem.Click += OnMinimizeClick;

        _floatingWindowItem = new Forms.ToolStripMenuItem("启用悬浮窗")
        {
            Checked = floatingWindowService.IsEnabled,
            Enabled = floatingWindowService.IsAvailable,
        };
        _floatingWindowItem.Click += OnFloatingWindowClick;

        _floatingWindowLockItem = new Forms.ToolStripMenuItem("锁定悬浮窗")
        {
            Checked = floatingWindowService.IsLocked,
            Enabled = floatingWindowService.IsAvailable && floatingWindowService.IsEnabled,
        };
        _floatingWindowLockItem.Click += OnFloatingWindowLockClick;
        _floatingWindowService.StateChanged += OnFloatingWindowStateChanged;

        var settingsItem = new Forms.ToolStripMenuItem("设置");
        settingsItem.Click += OnSettingsClick;

        var exitItem = new Forms.ToolStripMenuItem("退出程序");
        exitItem.Click += OnExitClick;

        _contextMenu = CreateContextMenu();
        _contextMenu.Items.Add(_minimizeItem);
        _contextMenu.Items.Add(_floatingWindowItem);
        _contextMenu.Items.Add(_floatingWindowLockItem);
        _contextMenu.Items.Add(settingsItem);
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
            Icon = _applicationIcon,
            Text = "CodexMonitor",
            Visible = true,
        };
        _notifyIcon.MouseClick += OnMouseClick;
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? MinimizeRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public void SetCanMinimize(bool canMinimize)
    {
        if (!_disposed)
        {
            _minimizeItem.Enabled = canMinimize;
        }
    }

    public void ShowBackgroundNotificationOnce()
    {
        if (_backgroundNotificationShown || _disposed)
        {
            return;
        }

        _backgroundNotificationShown = true;
        _notifyIcon.BalloonTipTitle = "CodexMonitor";
        _notifyIcon.BalloonTipText = "程序仍在通知区域运行。";
        // Do not replace the application icon with Windows' built-in information glyph.
        _notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.None;
        _notifyIcon.ShowBalloonTip(3_000);
    }

    public void ShowRefreshFailureNotification(string error)
    {
        if (_disposed)
        {
            return;
        }

        const int maximumErrorLength = 160;
        var compactError = string.IsNullOrWhiteSpace(error)
            ? "未知错误"
            : error.Trim();
        if (compactError.Length > maximumErrorLength)
        {
            compactError = compactError[..maximumErrorLength] + "…";
        }

        _notifyIcon.BalloonTipTitle = "CodexMonitor 刷新失败";
        _notifyIcon.BalloonTipText = $"{compactError}\n详细信息已写入日志。";
        _notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.Warning;
        _notifyIcon.ShowBalloonTip(5_000);
    }

    private void OnMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            OpenRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnMinimizeClick(object? sender, EventArgs e)
        => MinimizeRequested?.Invoke(this, EventArgs.Empty);

    private void OnFloatingWindowClick(object? sender, EventArgs e)
    {
        if (!_floatingWindowService.IsAvailable)
        {
            return;
        }

        _floatingWindowService.SetEnabled(!_floatingWindowService.IsEnabled);
    }

    private void OnFloatingWindowLockClick(object? sender, EventArgs e)
    {
        if (!_floatingWindowService.IsAvailable || !_floatingWindowService.IsEnabled)
        {
            return;
        }

        _floatingWindowService.SetLocked(!_floatingWindowService.IsLocked);
    }

    private void OnFloatingWindowStateChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _floatingWindowItem.Checked = _floatingWindowService.IsEnabled;
        _floatingWindowLockItem.Checked = _floatingWindowService.IsLocked;
        _floatingWindowLockItem.Enabled =
            _floatingWindowService.IsAvailable && _floatingWindowService.IsEnabled;
    }

    private void OnSettingsClick(object? sender, EventArgs e)
        => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnExitClick(object? sender, EventArgs e)
        => ExitRequested?.Invoke(this, EventArgs.Empty);

    private static Icon LoadApplicationIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            using var extractedIcon = Icon.ExtractAssociatedIcon(processPath);
            if (extractedIcon is not null)
            {
                return (Icon)extractedIcon.Clone();
            }
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private static Forms.ContextMenuStrip CreateContextMenu()
    {
        var menu = new Forms.ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = true,
        };

        if (Forms.SystemInformation.HighContrast)
        {
            return menu;
        }

        menu.BackColor = Color.FromArgb(32, 32, 32);
        menu.ForeColor = Color.FromArgb(245, 245, 247);
        menu.Renderer = new Forms.ToolStripProfessionalRenderer(new DarkMenuColorTable());

        return menu;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _floatingWindowService.StateChanged -= OnFloatingWindowStateChanged;
        _notifyIcon.MouseClick -= OnMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _applicationIcon.Dispose();
    }

    private sealed class DarkMenuColorTable : Forms.ProfessionalColorTable
    {
        private static readonly Color Background = Color.FromArgb(32, 32, 32);
        private static readonly Color Selection = Color.FromArgb(51, 74, 104);
        private static readonly Color Border = Color.FromArgb(68, 68, 68);

        public override Color ToolStripDropDownBackground => Background;

        public override Color ImageMarginGradientBegin => Background;

        public override Color ImageMarginGradientMiddle => Background;

        public override Color ImageMarginGradientEnd => Background;

        public override Color MenuBorder => Border;

        public override Color MenuItemBorder => Selection;

        public override Color MenuItemSelected => Selection;

        public override Color MenuItemSelectedGradientBegin => Selection;

        public override Color MenuItemSelectedGradientEnd => Selection;

        public override Color SeparatorDark => Border;

        public override Color SeparatorLight => Background;
    }
}
