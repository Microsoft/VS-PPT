using System;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.Editor.PeekF1
{
    internal static class NativeMethods
    {
        [DllImport("GDI32.DLL", EntryPoint = "CreateRectRgn")]
        internal static extern IntPtr CreateRectRgn(Int32 x1, Int32 y1, Int32 x2, Int32 y2);

        [DllImport("User32.dll", SetLastError = true)]
        internal static extern Int32 SetWindowRgn(IntPtr hWnd, IntPtr hRgn, Boolean bRedraw);
    }
}
