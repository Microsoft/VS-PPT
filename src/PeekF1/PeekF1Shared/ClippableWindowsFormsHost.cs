using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Interop;
using System.Windows.Media;

namespace Microsoft.VisualStudio.Editor.PeekF1
{
    public class ClippableWindowsFormsHost : WindowsFormsHost
    {
        private Int32 _topLeftX = -1;
        private Int32 _topLeftY = -1;
        private Int32 _bottomRightX = -1;
        private Int32 _bottomRightY = -1;

        public void HandleLayoutChanged(IWpfTextView textView)
        {
            PresentationSource presentationSource = HwndSource.FromVisual(this);
            if (presentationSource == null)
            {
                return;
            }
            Visual rootVisual = presentationSource.RootVisual;
            if (rootVisual == null)
            {
                return;
            }
            if (!textView.VisualElement.IsDescendantOf(rootVisual))
            {
                return;
            }

            GeneralTransform transform = textView.VisualElement.TransformToAncestor(rootVisual);
            Rect scrollRect = transform.TransformBounds(new Rect(0, 0,
                textView.ViewportWidth, textView.ViewportHeight)).LogicalToDeviceUnits();

            GeneralTransform controlTransform = this.TransformToAncestor(rootVisual);
            Rect winFormsHostRect = controlTransform.TransformBounds(new Rect(0, 0,
                this.ActualWidth, this.ActualHeight)).LogicalToDeviceUnits();

            Rect intersectRect = Rect.Intersect(scrollRect, winFormsHostRect);

            int topLeftX = 0;
            int topLeftY = 0;
            int bottomRightX = 0;
            int bottomRightY = 0;
            if (intersectRect != Rect.Empty)
            {
                topLeftX = (int)(intersectRect.TopLeft.X - winFormsHostRect.TopLeft.X);
                topLeftY = (int)(intersectRect.TopLeft.Y - winFormsHostRect.TopLeft.Y);
                bottomRightX = (int)(intersectRect.BottomRight.X - winFormsHostRect.TopLeft.X);
                bottomRightY = (int)(intersectRect.BottomRight.Y - winFormsHostRect.TopLeft.Y);
            }

            if (_topLeftX != topLeftX || _topLeftY != topLeftY || _bottomRightX != bottomRightX || _bottomRightY != bottomRightY)
            {
                _topLeftX = topLeftX;
                _topLeftY = topLeftY;
                _bottomRightX = bottomRightX;
                _bottomRightY = bottomRightY;
                IntPtr hrgn = NativeMethods.CreateRectRgn(_topLeftX, _topLeftY, _bottomRightX, _bottomRightY);
                NativeMethods.SetWindowRgn(this.Handle, hrgn, true);
            }
        }
    }
}