using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using KiwiTraffic.Core.Model;

namespace KiwiTraffic.App.Services;

/// <summary>
/// The system tray presence: status tooltip plus the menu the plan requires
/// (show/hide, refresh, always-on-top, settings, restore position, exit).
/// </summary>
public sealed partial class TrayIcon : IDisposable
{
    /// <summary>NotifyIcon truncates beyond its limit; doing it ourselves keeps the text predictable.</summary>
    private const int TooltipLimit = 63;

    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _topmostItem;

    private Icon? _currentIcon;
    private bool _disposed;

    public TrayIcon(bool topmost, bool windowVisible)
    {
        _toggleItem = new ToolStripMenuItem();
        _topmostItem = new ToolStripMenuItem("窗口置顶") { CheckOnClick = true, Checked = topmost };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new ToolStripMenuItem("刷新", null, (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(_topmostItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("设置…", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("恢复窗口位置", null, (_, _) => ResetPositionRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty)));

        _toggleItem.Click += (_, _) => ToggleRequested?.Invoke(this, EventArgs.Empty);
        _topmostItem.CheckedChanged += (_, _) => TopmostChanged?.Invoke(_topmostItem.Checked);

        _currentIcon = TrayIconArtwork.Create(UsageLevel.Normal);

        _notifyIcon = new NotifyIcon
        {
            Icon = _currentIcon,
            Text = "KiwiTraffic",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _notifyIcon.DoubleClick += (_, _) => ToggleRequested?.Invoke(this, EventArgs.Empty);

        SetWindowVisible(windowVisible);
    }

    public event EventHandler? ToggleRequested;

    public event EventHandler? RefreshRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ResetPositionRequested;

    public event EventHandler? ExitRequested;

    public event Action<bool>? TopmostChanged;

    public void SetWindowVisible(bool visible)
        => _toggleItem.Text = visible ? "隐藏窗口" : "显示窗口";

    /// <summary>Updates the menu without raising <see cref="TopmostChanged"/>.</summary>
    public void SetTopmost(bool topmost)
    {
        if (_topmostItem.Checked != topmost)
        {
            _topmostItem.Checked = topmost;
        }
    }

    /// <summary>Refreshes the tooltip and tints the icon with the usage level.</summary>
    public void UpdateStatus(string alias, string percentText, UsageLevel level, string? statusMessage)
    {
        var summary = string.IsNullOrEmpty(statusMessage)
            ? $"{alias} · {percentText}"
            : $"{alias} · {percentText} · {statusMessage}";

        _notifyIcon.Text = Truncate(summary, TooltipLimit);

        var icon = TrayIconArtwork.Create(level);
        _notifyIcon.Icon = icon;
        _currentIcon?.Dispose();
        _currentIcon = icon;
    }

    public void ShowHint(string title, string text)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(5000);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Hiding first stops a ghost icon lingering until the user hovers it.
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        _currentIcon = null;
    }

    private static string Truncate(string value, int limit)
        => value.Length <= limit ? value : value[..(limit - 1)] + "…";
}

/// <summary>
/// Draws the tray icon at runtime: a ring tinted by the usage level. No .ico
/// has to be kept in the repository, and the icon can reflect the reading.
/// </summary>
internal static class TrayIconArtwork
{
    private const int Size = 32;

    /// <summary>Bytes in an ICONDIR followed by one ICONDIRENTRY.</summary>
    private const int IconHeaderSize = 6 + 16;

    public static Icon Create(UsageLevel level)
    {
        using var bitmap = new Bitmap(Size, Size);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            var colour = level switch
            {
                UsageLevel.Critical => Color.FromArgb(0xE0, 0x45, 0x4A),
                UsageLevel.Warning => Color.FromArgb(0xE0, 0x9A, 0x2B),
                _ => Color.FromArgb(0x2D, 0x7F, 0xF9),
            };

            using var pen = new Pen(colour, 5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };

            graphics.DrawEllipse(pen, 7f, 7f, 18f, 18f);
        }

        return new Icon(WrapInIconContainer(bitmap));
    }

    /// <summary>
    /// Wraps a bitmap as a single-image .ico holding PNG data.
    /// </summary>
    /// <remarks>
    /// Done by hand so the icon never touches a raw HICON: going through
    /// <c>Bitmap.GetHicon</c> would mean calling <c>DestroyIcon</c> to avoid
    /// leaking a GDI handle, and that P/Invoke would drag
    /// <c>AllowUnsafeBlocks</c> into the whole assembly.
    /// </remarks>
    private static MemoryStream WrapInIconContainer(Bitmap bitmap)
    {
        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        var pngBytes = png.ToArray();

        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)0);        // reserved
            writer.Write((ushort)1);        // resource type: icon
            writer.Write((ushort)1);        // one image

            writer.Write((byte)bitmap.Width);
            writer.Write((byte)bitmap.Height);
            writer.Write((byte)0);          // palette size (0 = truecolour)
            writer.Write((byte)0);          // reserved
            writer.Write((ushort)1);        // colour planes
            writer.Write((ushort)32);       // bits per pixel
            writer.Write(pngBytes.Length);
            writer.Write(IconHeaderSize);   // offset of the image data

            writer.Write(pngBytes);
        }

        stream.Position = 0;
        return stream;
    }
}
