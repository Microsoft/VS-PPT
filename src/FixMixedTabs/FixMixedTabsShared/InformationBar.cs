using Microsoft.VisualStudio.TelemetryForPPT;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace FixMixedTabs
{
    internal sealed class InformationBarMargin : ContentControl, IWpfTextViewMargin
    {
        public const string MarginName = "InformationBarProPack2";
        private IWpfTextView _textView;
        private ITextDocument _document;
        private IEditorOperations _operations;
        private ITextUndoHistory _undoHistory;

        private bool _isClosed = true;
        private bool _dontShowAgain;
        private readonly ITelemetrySession _telemetrySession;

        public InformationBarMargin(IWpfTextView textView, ITextDocument document, IEditorOperations editorOperations, ITextUndoHistory undoHistory)
        {
            _textView = textView;
            _document = document;
            _operations = editorOperations;
            _undoHistory = undoHistory;

            _telemetrySession = TelemetrySessionForPPT.Create(this.GetType().Assembly);

            var informationBar = new InformationBarControl();
            informationBar.Tabify.Click += Tabify;
            informationBar.Untabify.Click += Untabify;
            informationBar.Hide.Click += Hide;
            informationBar.DontShowAgain.Click += DontShowAgain;

            this.Height = 0;
            this.Content = informationBar;
            this.Name = MarginName;

            document.FileActionOccurred += FileActionOccurred;

            // Delay the initial check until the view gets focus.
            textView.GotAggregateFocus += GotAggregateFocus;
        }

        private void DisableInformationBar(bool instant)
        {
            _dontShowAgain = true;
            this.CloseInformationBar(instant);

            if (_document != null)
            {
                _document.FileActionOccurred -= FileActionOccurred;
                _document = null;
            }

            if (_textView != null)
            {
                _textView.GotAggregateFocus -= GotAggregateFocus;
                _textView = null;
            }
        }

        private void CheckTabsAndSpaces()
        {
            if (_dontShowAgain)
                return;

            ITextSnapshot snapshot = _textView.TextDataModel.DocumentBuffer.CurrentSnapshot;

            int tabSize = _textView.Options.GetOptionValue(DefaultOptions.TabSizeOptionId);

            if (MixedTabsDetector.HasMixedTabsAndSpaces(tabSize, snapshot))
            {
                ShowInformationBar();
            }
            else
            {
                this.CloseInformationBar(false);
            }
        }

        #region Event Handlers

        private void FileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if (_dontShowAgain)
                return;

            if ((e.FileActionType & FileActionTypes.ContentLoadedFromDisk) != 0 ||
                (e.FileActionType & FileActionTypes.ContentSavedToDisk) != 0)
            {
                CheckTabsAndSpaces();
            }
        }

        private void GotAggregateFocus(object sender, EventArgs e)
        {
            if (_textView != null)
            {
                _textView.GotAggregateFocus -= GotAggregateFocus;

                CheckTabsAndSpaces();
            }
        }

        #endregion

        #region Hiding and showing the information bar

        private void Hide(object sender, RoutedEventArgs e)
        {
            this.CloseInformationBar(false);

            _telemetrySession.PostEvent("VS/PPT-FixMixedTabs/Hide");
        }

        private void DontShowAgain(object sender, RoutedEventArgs e)
        {
            this.DisableInformationBar(false);

            _telemetrySession.PostEvent("VS/PPT-FixMixedTabs/DontShowAgain");
        }

        private void CloseInformationBar(bool instant)
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;

            // Since we're going to be closing, make sure focus is back in the editor.
            _textView.VisualElement.Focus();

            ChangeHeightTo(0, instant);
        }

        private void ShowInformationBar()
        {
            if ((!_isClosed) || _dontShowAgain)
                return;

            _isClosed = false;
            ChangeHeightTo(27, false);
        }

        private void ChangeHeightTo(double newHeight, bool instant)
        {
            if (instant || _textView.Options.GetOptionValue(DefaultWpfViewOptions.EnableSimpleGraphicsId))
            {
                this.Height = newHeight;
            }
            else
            {
                DoubleAnimation animation = new DoubleAnimation(this.Height, newHeight, new Duration(TimeSpan.FromMilliseconds(175)));
                Storyboard.SetTarget(animation, this);
                Storyboard.SetTargetProperty(animation, new PropertyPath(StackPanel.HeightProperty));

                Storyboard storyboard = new Storyboard();
                storyboard.Children.Add(animation);

                storyboard.Begin(this);
            }
        }

        #endregion

        #region Performing Tabify and Untabify

        private void Tabify(object sender, RoutedEventArgs e)
        {
            if (_textView != null)
            {
                PerformActionInUndo(() =>
                {
                    _operations.SelectAll();
                    _operations.Tabify();
                });

                this.CloseInformationBar(false);

                _telemetrySession.PostEvent("VS/PPT-FixMixedTabs/Tabify");
            }
        }

        private void Untabify(object sender, RoutedEventArgs e)
        {
            if (_textView != null)
            {
                PerformActionInUndo(() =>
                {
                    _operations.SelectAll();
                    _operations.Untabify();
                });

                this.CloseInformationBar(false);

                _telemetrySession.PostEvent("VS/PPT-FixMixedTabs/Untabify");
            }
        }

        private void PerformActionInUndo(Action action)
        {
            ITrackingPoint anchor = _textView.TextSnapshot.CreateTrackingPoint(_textView.Selection.AnchorPoint.Position, PointTrackingMode.Positive);
            ITrackingPoint active = _textView.TextSnapshot.CreateTrackingPoint(_textView.Selection.ActivePoint.Position, PointTrackingMode.Positive);
            bool empty = _textView.Selection.IsEmpty;
            TextSelectionMode mode = _textView.Selection.Mode;

            using (var undo = _undoHistory.CreateTransaction("Untabify"))
            {
                _operations.AddBeforeTextBufferChangePrimitive();

                action();

                ITextSnapshot after = _textView.TextSnapshot;

                _operations.SelectAndMoveCaret(new VirtualSnapshotPoint(anchor.GetPoint(after)),
                                               new VirtualSnapshotPoint(active.GetPoint(after)),
                                               mode,
                                               EnsureSpanVisibleOptions.ShowStart);

                _operations.AddAfterTextBufferChangePrimitive();

                undo.Complete();
            }
        }

        #endregion


        #region IWpfTextViewMargin Members

        public FrameworkElement VisualElement
        {
            get
            {
                return this;
            }
        }

        #endregion

        #region ITextViewMargin Members

        public double MarginSize
        {
            get
            {
                return this.ActualHeight;
            }
        }

        public bool Enabled
        {
            get
            {
                return !_dontShowAgain;
            }
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return marginName == MarginName ? this : null;
        }

        public void Dispose()
        {
            this.DisableInformationBar(true);
        }

        #endregion
    }
}