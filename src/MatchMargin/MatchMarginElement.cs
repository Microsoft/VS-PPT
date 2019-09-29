using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Microsoft.VisualStudio.PowerTools.MatchMargin
{
    [Export(typeof(EditorOptionDefinition))]
    [Name(MatchMarginEnabledOption.OptionName)]
    public sealed class MatchMarginEnabledOption : WpfViewOptionDefinition<bool>
    {
        public const string OptionName = "MatchMarginEnabled";
        public readonly static EditorOptionKey<bool> OptionKey = new EditorOptionKey<bool>(MatchMarginEnabledOption.OptionName);

        public override bool Default { get { return true; } }

        public override EditorOptionKey<bool> Key { get { return MatchMarginEnabledOption.OptionKey; } }
    }

    [Export(typeof(EditorOptionDefinition))]
    [Name(MatchAdornmentEnabledOption.OptionName)]
    public sealed class MatchAdornmentEnabledOption : WpfViewOptionDefinition<bool>
    {
        public const string OptionName = "MatchAdornmentEnabled";
        public readonly static EditorOptionKey<bool> OptionKey = new EditorOptionKey<bool>(MatchAdornmentEnabledOption.OptionName);

        public override bool Default { get { return true; } }

        public override EditorOptionKey<bool> Key { get { return MatchAdornmentEnabledOption.OptionKey; } }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(MatchColorFormat.Name)]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class MatchColorFormat : EditorFormatDefinition
    {
        public const string Name = "MatchColor";

        public MatchColorFormat()
        {
            this.DisplayName = Strings.MatchColor;
            this.ForegroundColor = Color.FromRgb(147, 112, 219);        //Color of the margin mark
            this.BackgroundColor = Color.FromRgb(103, 109, 91);        //Color of the adornment. Old: (228, 219, 246);
        }
    }

    /// <summary>
    /// Helper class to handle the rendering of the match margin.
    /// </summary>
    internal class MatchMarginElement : FrameworkElement
    {
        private readonly IWpfTextView _textView;
        private readonly IAdornmentLayer _layer;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly IVerticalScrollBar _scrollBar;

        private BackgroundSearch _search;
        private string _highlight;
        private SnapshotSpan? _highlightSpan;

        private Brush _marginMatchBrush;
        private Brush _adornmentMatchBrush;

        private bool _hasEvents;
        private bool _optionsChanging;
        private bool _isMarginEnabled;
        private bool _areAdornmentsEnabled;
        private bool _isDisposed;
        private const double MarkPadding = 1.0;
        private const double MarkThickness = 4.0;

        private const int SearchBufferSize = 4096;
        private const int MaximumHighlightWordLength = 128; //This should be small compared to SearchBufferSize

        private double _left = double.MaxValue;    //The horizontal extent to which we've rendered the match adornment.
        private double _right = double.MinValue;

        /// <summary>
        /// Constructor for the MatchMarginElement.
        /// </summary>
        /// <param name="textView">ITextView to which this MatchMargenElement will be attached.</param>
        /// <param name="factory">Instance of the MatchMarginFactory that is creating the margin.</param>
        /// <param name="verticalScrollbar">Vertical scrollbar of the ITextViewHost that contains <paramref name="textView"/>.</param>
        public MatchMarginElement(IWpfTextView textView, MatchMarginFactory factory, IVerticalScrollBar verticalScrollbar)
        {
            _textView = textView;
            this.IsHitTestVisible = false;

            _layer = textView.GetAdornmentLayer("MatchMarginAdornmentLayer");

            _scrollBar = verticalScrollbar;

            _editorFormatMap = factory.EditorFormatMapService.GetEditorFormatMap(textView);

            this.Width = 6.0;

            _textView.Options.OptionChanged += this.OnOptionChanged;
            this.IsVisibleChanged += this.OnViewOrMarginVisiblityChanged;
            _textView.VisualElement.IsVisibleChanged += this.OnViewOrMarginVisiblityChanged;

            this.OnOptionChanged(null, null);
        }

        private bool UpdateEventHandlers(bool checkEvents, bool wereAdornmentsEnabled = true)
        {
            bool needEvents = checkEvents &&
                              _textView.VisualElement.IsVisible &&
                              (this.MarginActive || this.AdornmentsActive);

            if (needEvents != _hasEvents)
            {
                _hasEvents = needEvents;
                if (needEvents)
                {
                    _editorFormatMap.FormatMappingChanged += OnFormatMappingChanged;
                    _textView.LayoutChanged += OnLayoutChanged;
                    _textView.Selection.SelectionChanged += OnPositionChanged;
                    _scrollBar.Map.MappingChanged += OnMappingChanged;

                    this.OnFormatMappingChanged(null, null);

                    return this.UpdateMatches(wereAdornmentsEnabled);
                }
                else
                {
                    _editorFormatMap.FormatMappingChanged -= OnFormatMappingChanged;
                    _textView.LayoutChanged -= OnLayoutChanged;
                    _textView.Selection.SelectionChanged -= OnPositionChanged;
                    _scrollBar.Map.MappingChanged -= OnMappingChanged;

                    if (_search != null)
                    {
                        _search.Abort();
                        _search = null;
                    }
                    _highlight = null;
                    _highlightSpan = null;
                }
            }

            return false;
        }

        private Brush GetBrush(string name, string resource)
        {
            var rd = _editorFormatMap.GetProperties(name);

            if (rd.Contains(resource))
            {
                return rd[resource] as Brush;
            }

            return null;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;

                _textView.Options.OptionChanged -= this.OnOptionChanged;
                this.IsVisibleChanged -= this.OnViewOrMarginVisiblityChanged;
                _textView.VisualElement.IsVisibleChanged -= this.OnViewOrMarginVisiblityChanged;
                this.UpdateEventHandlers(false);
            }
        }

        public bool Enabled
        {
            get
            {
                return _isMarginEnabled;
            }
        }

        private void OnOptionChanged(object sender, EditorOptionChangedEventArgs e)
        {
            bool wereAdornmentsEnabled = _areAdornmentsEnabled;
            _areAdornmentsEnabled = _textView.Options.GetOptionValue(MatchAdornmentEnabledOption.OptionKey);

            bool wasMarginEnabled = _isMarginEnabled;
            _isMarginEnabled = _textView.Options.GetOptionValue(MatchMarginEnabledOption.OptionKey);

            try
            {
                _optionsChanging = true;

                this.Visibility = this.Enabled ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                _optionsChanging = false;
            }

            bool refreshed = this.UpdateEventHandlers(true, wereAdornmentsEnabled);

            //If the UpdateEventHandlers call above didn't initiate a search then we need to force the adornments and the margin to update
            //to update if they were turned on/off.
            if (!refreshed)
            {
                if (wereAdornmentsEnabled != _areAdornmentsEnabled)
                {
                    this.RedrawAdornments();
                }

                if (wasMarginEnabled != _isMarginEnabled)
                {
                    this.InvalidateVisual();
                }
            }
        }

        private void OnFormatMappingChanged(object sender, FormatItemsEventArgs e)
        {
            _marginMatchBrush = this.GetBrush(MatchColorFormat.Name, EditorFormatDefinition.ForegroundBrushId);
            _adornmentMatchBrush = this.GetBrush(MatchColorFormat.Name, EditorFormatDefinition.BackgroundBrushId);
        }

        /// <summary>
        /// Handler for layout changed events.
        /// </summary>
        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (AnyTextChanges(e.OldViewState.EditSnapshot.Version, e.NewViewState.EditSnapshot.Version))
            {
                //There were text changes so we need to recompute possible matches
                this.UpdateMatches();
            }
            else
            {
                //No text changes so just redraw adornments from previous matches.
                this.RedrawAdornments(e.NewOrReformattedLines, e.NewViewState.ViewportLeft, e.NewViewState.ViewportRight);
            }
        }

        /// <summary>
        /// Handler for either the caret position changing or a change
        /// in the mapping of the scroll bar.
        /// </summary>
        private void OnPositionChanged(object sender, EventArgs e)
        {
            this.UpdateMatches();
        }

        private void OnViewOrMarginVisiblityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            //There is no need to update event handlers if the visibility change is the result of an options change (since we will
            //update the event handlers after changing all the options).
            //
            //It is possible this will get called twice in quick succession (when the tab containing the host is made visible, the view and the margin
            //will get visibility changed events).
            if (!_optionsChanging)
            {
                this.UpdateEventHandlers(true);
            }
        }

        /// <summary>
        /// Handler for the scrollbar changing its coordinate mapping.
        /// </summary>
        private void OnMappingChanged(object sender, EventArgs e)
        {
            //Simply invalidate the visual: the positions of the various highlights haven't changed.
            this.InvalidateVisual();
        }

        private static bool AnyTextChanges(ITextVersion oldVersion, ITextVersion currentVersion)
        {
            while (oldVersion != currentVersion)
            {
                if (oldVersion.Changes.Count > 0)
                    return true;
                oldVersion = oldVersion.Next;
            }

            return false;
        }

        private void RedrawAdornments()
        {
            this.RedrawAdornments(null, _textView.ViewportLeft, _textView.ViewportRight);
        }

        private void RedrawAdornments(IList<ITextViewLine> newOrReformattedLines, double viewportLeft, double viewportRight)
        {
            if (_areAdornmentsEnabled)
            {
                if ((newOrReformattedLines == null) || (viewportLeft < _left) || (viewportRight > _right))
                {
                    _left = viewportLeft - 50.0;    //Add in a little padding so small scrolls won't trigger a refresh.
                    _right = viewportRight + 50.0;

                    _layer.RemoveAllAdornments();
                    newOrReformattedLines = _textView.TextViewLines;
                }

                if ((_highlight != null) && (_search != null))
                {
                    //Take a snapshot of the matches found to date (this could still be changing
                    //if the search has not completed yet).
                    IList<SnapshotSpan> matches = _search.Matches;
                    if ((matches.Count > 0) && (matches[0].Snapshot == _textView.TextSnapshot))
                    {
                        //matches is sorted, as is newOrReformattedLines (as are the spans in visibleText), so keep track of the last match found since it
                        //is a good starting point for the next match.
                        int firstLegalMatch = 0;

                        int caretPosition = _textView.Caret.Position.BufferPosition;

                        foreach (var line in newOrReformattedLines)
                        {
                            //Find all matches on the visible text in line.
                            if (line.TextRight > _left)
                            {
                                SnapshotPoint? leftCharacter = line.GetBufferPositionFromXCoordinate(_left);
                                if (!leftCharacter.HasValue)
                                    leftCharacter = line.Start;

                                SnapshotPoint? rightCharacter = line.GetBufferPositionFromXCoordinate(_right);
                                if (!rightCharacter.HasValue)
                                    rightCharacter = line.End;

                                if (leftCharacter.Value.Position > rightCharacter.Value.Position)
                                {
                                    // Swap left & right (due to bidi sequences, we could end up with left coming after right in the buffer)
                                    var t = leftCharacter;
                                    leftCharacter = rightCharacter;
                                    rightCharacter = t;
                                }

                                //We know the start & endpoints exist, the edit & visual snapshots represent the same instant and we're mapping up so just map up
                                SnapshotPoint leftInVisualBuffer = _textView.TextViewModel.GetNearestPointInVisualSnapshot(leftCharacter.Value, _textView.VisualSnapshot, PointTrackingMode.Negative);
                                SnapshotPoint rightInVisualBuffer = _textView.TextViewModel.GetNearestPointInVisualSnapshot(rightCharacter.Value, _textView.VisualSnapshot, PointTrackingMode.Negative);

                                NormalizedSnapshotSpanCollection visibleText = _textView.BufferGraph.MapDownToSnapshot(new SnapshotSpan(leftInVisualBuffer, rightInVisualBuffer),
                                                                                                                                  SpanTrackingMode.EdgeInclusive, _textView.TextSnapshot);

                                foreach (var span in visibleText)
                                {
                                    while (true)
                                    {
                                        firstLegalMatch = FindMatchIndex(matches, span.Start, firstLegalMatch);
                                        if (firstLegalMatch >= matches.Count)
                                        {
                                            //No more matches, we might as well stop.
                                            return;
                                        }

                                        SnapshotSpan matchingSpan = matches[firstLegalMatch];

                                        if (matchingSpan.Start <= span.End)
                                        {
                                            ++firstLegalMatch;  //Make sure we don't redraw this adornment.

                                            //Don't draw the adornment for the word the caret is adjacent to.
                                            if ((caretPosition < matchingSpan.Start) || (caretPosition > matchingSpan.End))
                                            {
                                                Geometry g = _textView.TextViewLines.GetMarkerGeometry(matchingSpan);
                                                if (g != null)
                                                {
                                                    _layer.AddAdornment(matchingSpan, null, new GeometryAdornment(_adornmentMatchBrush, g));
                                                }
                                            }
                                        }
                                        else
                                            break;  //No matches in this span.
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                _layer.RemoveAllAdornments();
                _left = double.MaxValue;        //reset the left/right bounds so we'll redraw when options are turned back on.
                _right = double.MinValue;
            }
        }

        private bool MarginActive { get { return _isMarginEnabled && this.IsVisible; } }
        private bool AdornmentsActive { get { return _areAdornmentsEnabled; } }

        private static int FindMatchIndex(IList<SnapshotSpan> matches, int start, int firstLegalMatch)
        {
            //Search for a match where (start < match.End) && index >= firstLegalMatch.
            int low = firstLegalMatch;
            int high = matches.Count;

            while (low < high)
            {
                int middle = (low + high) / 2;
                if (matches[middle].End <= start)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        /// <summary>
        /// Start a background search for all instances of this.highlight.
        /// </summary>
        /// <returns>
        /// true if a either a search has been queued or if the adornments/margin have been cleared since the highlight was removed.
        /// </returns>
        private bool UpdateMatches(bool wereAdornmentsEnabled = true)
        {
            if (_hasEvents)
            {
                SnapshotSpan? oldHighlightSpan = _highlightSpan;
                string oldHighlight = _highlight;

                bool matchWholeWord = _textView.Selection.IsEmpty;
                _highlightSpan = matchWholeWord
                                     ? BackgroundSearch.GetExtentOfWord(_textView.Caret.Position.BufferPosition)
                                     : this.SelectionToHighlightSpan();

                _highlight = (_highlightSpan.HasValue) ? _highlightSpan.Value.GetText() : null;

                //Do a new search if the highlight changed, there is no existing search, or the existing search was on the wrong snapshot.
                if ((_highlight != oldHighlight) || (_search == null) || (_search.Snapshot != _textView.TextSnapshot))
                {
                    //The text of the highlight changes ... restart the search.
                    if (_search != null)
                    {
                        //Stop and blow away the old search (even if it didn't finish, the results are not interesting anymore).
                        _search.Abort();
                        _search = null;
                    }

                    if (_highlight != null)
                    {
                        //The underlying buffer could be very large, meaning that doing the search for all matches on the UI thread
                        //is a bad idea. Do the search on the background thread and use a callback to invalidate the visual when
                        //the entire search has completed.
                        _search = new BackgroundSearch(_textView.TextSnapshot, _highlight, matchWholeWord,
                                                            delegate
                                                            {
                                                                //Force the invalidate to happen on the UI thread to satisfy WPF
                                                                this.Dispatcher.Invoke(DispatcherPriority.Normal,
                                                                                        new DispatcherOperationCallback(delegate
                                                                                        {
                                                                                            //Guard against the view closing before dispatcher executes this.
                                                                                            if (!_isDisposed)
                                                                                            {
                                                                                                this.InvalidateVisual();
                                                                                                this.RedrawAdornments();
                                                                                            }
                                                                                            return null;
                                                                                        }),
                                                                                        null);
                                                            });
                    }
                    else
                    {
                        //no highlight == no adornments or marks.
                        _layer.RemoveAllAdornments();
                        this.InvalidateVisual();
                    }

                    return true;
                }
                else if ((oldHighlight != null) && wereAdornmentsEnabled && this.AdornmentsActive)
                {
                    //The highlight didn't change and isn't null ... therefore both old & new highlight spans have values. Update the adornments so we don't highlight the
                    //match the caret is on.
                    SnapshotSpan translatedOldHighlightSpan = oldHighlightSpan.Value.TranslateTo(_textView.TextSnapshot, SpanTrackingMode.EdgeInclusive);
                    if (translatedOldHighlightSpan != _highlightSpan.Value)
                    {
                        //The spans moved (e.g. the user moved from this on one line to this on another).
                        //Remove the adornment from the new highlight.
                        _layer.RemoveAdornmentsByVisualSpan(_highlightSpan.Value);

                        //Add an adornment at the old source of the highlight span.
                        Geometry g = _textView.TextViewLines.GetMarkerGeometry(translatedOldHighlightSpan);
                        if (g != null)
                        {
                            _layer.AddAdornment(translatedOldHighlightSpan, null, new GeometryAdornment(_adornmentMatchBrush, g));
                        }
                    }
                }
            }

            return false;
        }

        private SnapshotSpan? SelectionToHighlightSpan()
        {
            SnapshotSpan selection = _textView.Selection.StreamSelectionSpan.SnapshotSpan;

            if ((selection.Length > 0) && (selection.Length < MatchMarginElement.MaximumHighlightWordLength))
            {
                if (_textView.Selection.Mode == TextSelectionMode.Box)
                {
                    //Make sure both end points of the selection are on the same TextViewLine if it is a box selection.
                    //Get the TextViewLine for the active point (== caret) since the view is probably going to have that formatted already.
                    ITextViewLine line = _textView.GetTextViewLineContainingBufferPosition(_textView.Selection.ActivePoint.Position);
                    if (!line.ContainsBufferPosition(_textView.Selection.AnchorPoint.Position))
                        return null;
                }

                //Check to make sure that the selection contains something other than whitespace.
                int end = selection.End;
                for (int i = selection.Start; (i < end); ++i)
                {
                    char c = selection.Snapshot[i];
                    if (!char.IsWhiteSpace(c))
                        return selection;
                }
            }

            return null;
        }

        /// <summary>
        /// Override for the FrameworkElement's OnRender. When called, redraw
        /// all of the markers 
        /// </summary>
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (_search != null)
            {
                //There is a word that should be highlighted. It doesn't matter whether or not the search has completed or
                //is still in progress: draw red marks for each match found so far (the completion callback on the search
                //will guarantee that the highlight display gets invalidated once the search has completed).

                //Take a snapshot of the matches found to date (this could still be changing
                //if the search has not completed yet).
                IList<SnapshotSpan> matches = _search.Matches;

                double lastY = double.MinValue;
                int markerCount = Math.Min(500, matches.Count);
                for (int i = 0; (i < markerCount); ++i)
                {
                    //Get (for small lists) the index of every match or, for long lists, the index of every
                    //(count / 1000)th entry. Use longs to avoid any possible integer overflow problems.
                    int index = (int)(((long)(i) * (long)(matches.Count)) / ((long)markerCount));
                    SnapshotPoint match = matches[index].Start;

                    //Translate the match from its snapshot to the view's current snapshot (the versions should be the same,
                    //but this will handle it if -- for some reason -- they are not).
                    double y = Math.Floor(_scrollBar.GetYCoordinateOfBufferPosition(match.TranslateTo(_textView.TextSnapshot, PointTrackingMode.Negative)));
                    if (y + MarkThickness > lastY)
                    {
                        lastY = y;
                        this.DrawMark(drawingContext, _marginMatchBrush, y);
                    }
                }
            }
        }

        private void DrawMark(DrawingContext drawingContext, Brush brush, double y)
        {
            drawingContext.DrawRectangle(brush, null,
                                         new Rect(MarkPadding, y - MarkThickness * 0.5, this.Width - MarkPadding * 2.0, MarkThickness));
        }

        /// <summary>
        /// Helper class to do a search for matches on a background thread while
        /// providing thread-safe access to intermediate results.
        /// </summary>
        private class BackgroundSearch
        {
            private bool _abort;
            private static List<SnapshotSpan> s_emptyList = new List<SnapshotSpan>(0);
            private IList<SnapshotSpan> _matches = s_emptyList;
            public readonly ITextSnapshot Snapshot;

            public static SnapshotSpan? GetExtentOfWord(SnapshotPoint position)
            {
                int start = position.Position;
                int end = position;

                int startLimit = Math.Max(0, end - (MatchMarginElement.MaximumHighlightWordLength + 1));
                while (--start >= startLimit)
                {
                    if (!IsWordCharacter(position.Snapshot[start]))
                        break;
                }
                ++start;

                int endLimit = Math.Min(position.Snapshot.Length, start + MatchMarginElement.MaximumHighlightWordLength);
                while (end < endLimit)
                {
                    if (!IsWordCharacter(position.Snapshot[end]))
                        break;

                    ++end;
                }

                if ((start != end) && (end - start < MatchMarginElement.MaximumHighlightWordLength))
                    return new SnapshotSpan(position.Snapshot, start, end - start);
                else
                    return null;
            }

            public static bool IsWordCharacter(char c)
            {
                return (c == '_') | char.IsLetterOrDigit(c);
            }

            /// <summary>
            /// Search for all instances of <paramref name="searchText"/> in <paramref name="snapshot"/>. Call
            /// <paramref name="completionCallback"/> once the search has completed.
            /// </summary>
            /// <param name="snapshot">Text snapshot in which to search.</param>
            /// <param name="searchText">Test to search for.</param>
            /// <param name="completionCallback">Delegate to call if the search is completed (will be called on the UI thread).</param>
            /// <remarks>The constructor must be called from the UI thread.</remarks>
            public BackgroundSearch(ITextSnapshot snapshot, string searchText, bool matchWholeWord, Action completionCallback)
            {
                this.Snapshot = snapshot;

                ThreadPool.QueueUserWorkItem(delegate (object state)
                {
                    //Lower our priority so that we do not compete with the rendering.
                    System.Threading.Thread.CurrentThread.Priority = ThreadPriority.Lowest;
                    System.Threading.Thread.CurrentThread.IsBackground = true;

                    List<SnapshotSpan> newMatches = new List<SnapshotSpan>();

                    int start = 0;
                    while (true)
                    {
                        int end = Math.Min(snapshot.Length, start + MatchMarginElement.SearchBufferSize);
                        string text = snapshot.GetText(start, end - start);

                        int offset = (start == 0) ? 0 : 1;
                        while (true)
                        {
                            int match = text.IndexOf(searchText, offset, StringComparison.Ordinal);
                            if (match == -1)
                                break;

                            if (matchWholeWord)
                            {
                                //Make sure the character preceeding the match is a word break
                                //(or the very start of the buffer, which is the only time match can equal 0).
                                if ((match == 0) || !BackgroundSearch.IsWordCharacter(text[match - 1]))
                                {
                                    //Make sure the character after the match is a word break.
                                    //If we're at the end of text, then it is only considered a word break if that is also the very end of the buffer.
                                    if ((match + searchText.Length == text.Length)
                                        ? (end == snapshot.Length)
                                        : !BackgroundSearch.IsWordCharacter(text[match + searchText.Length]))
                                    {
                                        SnapshotSpan matchSpan = new SnapshotSpan(snapshot, match + start, searchText.Length);
                                        newMatches.Add(matchSpan);
                                    }
                                }
                            }
                            else
                            {
                                //Any match is a match.
                                SnapshotSpan matchSpan = new SnapshotSpan(snapshot, match + start, searchText.Length);
                                newMatches.Add(matchSpan);
                            }

                            //Continue searching at the location of the next possible match
                            //(we could add one more since there needs to be one character of whitespace between matches, but
                            //then we'd need an if to guard against placing offset past the end of text).
                            offset = match + searchText.Length;
                        }

                        //Check to see if the search should be aborted because no one cares about the result any more.
                        if (_abort)
                            return;

                        if (end == snapshot.Length)
                            break;  //All done.

                        //rollback from the end enough so that we can match something that we previously matched at the very end of the
                        //(along with the preceeding character so we can ensure it starts on a word break).
                        start = end - (searchText.Length + 1);
                    }

                    //This should be a thread safe operation since it is atomic
                    _matches = newMatches;

                    completionCallback();
                });
            }

            /// <summary>
            /// About the current search.
            /// </summary>
            public void Abort()
            {
                _abort = true;
            }

            public IList<SnapshotSpan> Matches
            {
                get
                {
                    //This should be thread safe since we to a single assignment to this.matches (of a list that is never changed
                    //after the assignment is done).
                    return _matches;
                }
            }
        }

        public class GeometryAdornment : UIElement
        {
            private readonly DrawingVisual _child;

            public GeometryAdornment(Brush fillBrush, Geometry geometry)
            {
                _child = new DrawingVisual();
                DrawingContext context = _child.RenderOpen();
                context.DrawGeometry(fillBrush, null, geometry);
                context.Close();

                this.AddVisualChild(_child);
            }

            #region Member Overrides
            protected override Visual GetVisualChild(int index)
            {
                return _child;
            }

            protected override int VisualChildrenCount
            {
                get
                {
                    return 1;
                }
            }
            #endregion //Member Overrides
        }
    }
}