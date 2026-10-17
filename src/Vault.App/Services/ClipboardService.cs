using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace Vault.App.Services;

public sealed class ClipboardService : IClipboardService
{
    private static readonly TimeSpan ClearAfter = TimeSpan.FromSeconds(15);

    private readonly DispatcherTimer _timer;
    private string? _lastCopied;

    public ClipboardService()
    {
        _timer = new DispatcherTimer { Interval = ClearAfter };
        _timer.Tick += (_, _) => ClearIfUnchanged();
    }

    public void CopySensitive(string text)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, text);
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[4]));
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[4]));
        data.SetData("CanUploadToCloudClipboard", new MemoryStream(new byte[4]));

        try
        {
            Clipboard.SetDataObject(data, copy: false);
        }
        catch (COMException)
        {
            return;
        }

        _lastCopied = text;
        _timer.Stop();
        _timer.Start();
    }

    private void ClearIfUnchanged()
    {
        _timer.Stop();
        try
        {
            if (_lastCopied is not null && Clipboard.ContainsText() && Clipboard.GetText() == _lastCopied)
                Clipboard.Clear();
        }
        catch (COMException)
        {
        }
        _lastCopied = null;
    }
}
