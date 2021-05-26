using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.Editor.PeekF1
{
    /// <summary>
    /// When WebBrowser recieves focus (for example user clicks on it) we also send WPF focus to the 
    /// containing user control. The WPF focus must be inside for Peek to properly pass commands down
    /// to this presentation.
    /// </summary>
    internal class F1PeekResultPresentation : IPeekResultPresentation, INotifyPropertyChanged
    {
        private readonly Uri _helpUri;
        private F1PeekPresentationControl _control;
        private IWpfTextView _textView;

        /// <summary>
        /// WebBrowser control command group that contains the Find command.
        /// </summary>
        private Guid _CGID_IWebBrowser = new Guid("ED016940-BD5B-11CF-BA4E-00C04FD70816");

        public F1PeekResultPresentation(F1PeekResult f1Result)
        {
            if (f1Result == null)
            {
                throw new ArgumentNullException("f1Result");
            }
            _helpUri = new Uri(f1Result.HelpUrl);
        }

        public Brush ContainingTextViewBackground
        {
            get
            {
                return _textView.Background;
            }
        }

        public IWpfTextView ContainingTextView
        {
            get
            {
                return _textView;
            }
        }

        public System.Windows.Forms.WebBrowser Browser
        {
            get
            {
                return _browser;
            }
        }

        #region IPeekResultPresentation

        public IPeekResultScrollState CaptureScrollState()
        {
            return new F1PeekResultScrollState(this);
        }

        public void Close()
        {
        }

        public System.Windows.UIElement Create(IPeekSession session, IPeekResultScrollState state)
        {
            _textView = (IWpfTextView)session.TextView;
            _textView.BackgroundBrushChanged += OnTextViewBackgroundBrushChanged;

            _textView.LayoutChanged += ContainingTextView_LayoutChanged;

            _control = new F1PeekPresentationControl();
            _control.DataContext = this;

            _browser = new System.Windows.Forms.WebBrowser();
            _browser.Url = _helpUri;
            _control.WinFormsHost.Child = _browser;

            _browser.DocumentCompleted += Browser_DocumentCompleted;
            _browser.Navigating += Browser_Navigating;

            return _control;
        }

        private void ContainingTextView_LayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (_control != null)
            {
                ThreadHelper.JoinableTaskFactory.WithPriority(_control.Dispatcher, DispatcherPriority.Background).RunAsync(async () =>
                {
                    await Task.Yield();
                    _control.WinFormsHost.HandleLayoutChanged(_textView);
                });
            }
        }

        private void OnTextViewBackgroundBrushChanged(object sender, BackgroundBrushChangedEventArgs e)
        {
            this.RaisePropertyChanged("ContainingTextViewBackground");
        }

        protected void RaisePropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler tempHandler = PropertyChanged;
            if (tempHandler != null)
            {
                tempHandler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void Browser_DocumentCompleted(object sender, System.Windows.Forms.WebBrowserDocumentCompletedEventArgs e)
        {
            _browser.DocumentCompleted -= Browser_DocumentCompleted;

            _control.ProgressBar.Visibility = Visibility.Collapsed;
            Keyboard.Focus(_control.PresentationRoot);

            _browser.Document.Focusing += Document_Focusing;
        }

        private void Browser_Navigating(object sender, System.Windows.Forms.WebBrowserNavigatingEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Url.Fragment) && e.Url.Host != null && e.Url.Host.EndsWith("msdn.microsoft.com"))
            {
                e.Cancel = true;

                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await Task.Yield();
                    _browser.Navigate(e.Url.AbsoluteUri + "#content");
                });
            }
        }

        private void SendFocusToPeek()
        {
            if (_control != null)
            {
                Keyboard.Focus(_control.PresentationRoot);
                _control.PresentationRoot.Focus();
            }
        }

        private void Document_Focusing(object sender, System.Windows.Forms.HtmlElementEventArgs e)
        {
            SendFocusToPeek();
        }


#pragma warning disable 0067
        public event EventHandler RecreateContent;
#pragma warning restore 0067

        public void ScrollIntoView(IPeekResultScrollState state)
        {
        }

        public void SetKeyboardFocus()
        {
            SendFocusToPeek();
        }

        public bool TryOpen(IPeekResult otherResult)
        {
            // Cannot open other results
            return false;
        }

        public double ZoomLevel
        {
            get
            {
                return 1.0;
            }
            set
            {
            }
        }

        public void Dispose()
        {
            if (_browser != null)
            {
                _browser.DocumentCompleted -= Browser_DocumentCompleted;
                _browser.Navigating -= Browser_Navigating;
                if (_browser.Document != null)
                {
                    _browser.Document.Focusing -= Document_Focusing;
                }

                _browser.Dispose();
            }

            if (_textView != null)
            {
                _textView.BackgroundBrushChanged -= OnTextViewBackgroundBrushChanged;
                _textView.LayoutChanged -= ContainingTextView_LayoutChanged;
            }
        }

        public bool CanSave(out string defaultPath)
        {
            defaultPath = null;
            return false;
        }

        public bool IsDirty
        {
            get { return false; }
        }

#pragma warning disable 0067
        public event EventHandler IsDirtyChanged;
#pragma warning restore 0067

        public bool IsReadOnly
        {
            get { return true; }
        }

#pragma warning disable 0067
        public event EventHandler IsReadOnlyChanged;
        private System.Windows.Forms.WebBrowser _browser;
#pragma warning restore 0067

        event EventHandler<RecreateContentEventArgs> IPeekResultPresentation.RecreateContent
        {
            add { }
            remove { }
        }

        public bool TryPrepareToClose()
        {
            // Nothing to prepare
            return true;
        }

        public bool TrySave(bool saveAs)
        {
            return true;
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
