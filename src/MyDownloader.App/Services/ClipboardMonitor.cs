using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;

namespace MyDownloader.App.Services
{
    /// <summary>
    /// 剪贴板变更监听：通过 Win32 AddClipboardFormatListener 监听 WM_CLIPBOARDUPDATE。
    /// </summary>
    public class ClipboardMonitor : IDisposable
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private IntPtr _hwnd;
        private readonly Action<string> _onUrl;
        private readonly Func<Task<string?>> _readClipboard;
        private string _lastText = string.Empty;
        private DateTime _lastTime = DateTime.MinValue;

        public ClipboardMonitor(Action<string> onUrl, Func<Task<string?>> readClipboard)
        {
            _onUrl = onUrl;
            _readClipboard = readClipboard;
        }

        /// <summary>在主窗口句柄就绪后调用。</summary>
        public void Attach(TopLevel topLevel)
        {
            var handle = topLevel.TryGetPlatformHandle();
            if (handle == null) return;
            _hwnd = handle.Handle;
            AddClipboardFormatListener(_hwnd);
            // Avalonia 12 中 Win32Properties 位于 Avalonia.Controls，钩子通过 AddWndProcHookCallback 注册。
            Win32Properties.AddWndProcHookCallback(topLevel, WndProc);
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                // 异步读取剪贴板，避免在 UI 线程同步阻塞造成死锁。
                Dispatcher.UIThread.Post(async () =>
                {
                    var text = (await _readClipboard())?.Trim();
                    // 仅对"看起来像可下载文件"的链接触发，避免对普通网页地址打扰。
                    if (string.IsNullOrEmpty(text) || !MyDownloader.Core.PathTools.LooksLikeDownloadUrl(text))
                        return;

                    // 仅对极短时间内的相同内容去重（防止系统重复写入），
                    // 用户稍后主动再次复制同一地址仍会提示。
                    var now = DateTime.Now;
                    if (text == _lastText && (now - _lastTime).TotalSeconds < 1.5)
                        return;
                    _lastText = text;
                    _lastTime = now;
                    _onUrl(text);
                });
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_hwnd != IntPtr.Zero)
            {
                try { RemoveClipboardFormatListener(_hwnd); } catch { /* 忽略 */ }
                _hwnd = IntPtr.Zero;
            }
        }
    }
}
