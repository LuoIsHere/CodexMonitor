using System.Drawing;
using Forms = System.Windows.Forms;

namespace LuoIsHere.CodexMonitor.App;

public sealed class TrayIconService : IDisposable
{
    private readonly Icon _applicationIcon;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _contextMenu;
    private bool _backgroundNotificationShown;
    private bool _disposed;

    public TrayIconService()
    {
        _applicationIcon = LoadApplicationIcon();

        var openItem = new Forms.ToolStripMenuItem("打开窗口");
        openItem.Click += OnOpenClick;

        var refreshItem = new Forms.ToolStripMenuItem("立即刷新");
        refreshItem.Click += OnRefreshClick;

        var exitItem = new Forms.ToolStripMenuItem("退出");
        exitItem.Click += OnExitClick;

        _contextMenu = new Forms.ContextMenuStrip();
        _contextMenu.Items.Add(openItem);
        _contextMenu.Items.Add(refreshItem);
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

    public event EventHandler? RefreshRequested;

    public event EventHandler? ExitRequested;

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

    private void OnOpenClick(object? sender, EventArgs e)
        => OpenRequested?.Invoke(this, EventArgs.Empty);

    private void OnRefreshClick(object? sender, EventArgs e)
        => RefreshRequested?.Invoke(this, EventArgs.Empty);

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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.MouseClick -= OnMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _applicationIcon.Dispose();
    }
}
