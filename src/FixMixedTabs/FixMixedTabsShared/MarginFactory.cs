using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Diagnostics;

namespace FixMixedTabs
{
    #region InformationBar Factory
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(InformationBarMargin.MarginName)]
    [MarginContainer(PredefinedMarginNames.Top)]
    [ContentType("any")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class MarginFactory : IWpfTextViewMarginProvider
    {
        [Import]
        private ITextDocumentFactoryService _TextDocumentFactoryService = null;

        [Import]
        private IEditorOperationsFactoryService _OperationsFactory = null;

        [Import]
        private ITextUndoHistoryRegistry _UndoHistoryRegistry = null;

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost textViewHost, IWpfTextViewMargin containerMargin)
        {
            IWpfTextView view = textViewHost.TextView;

            // Files larger than 1 MB should be skipped to avoid hangs.
            if (view.TextSnapshot.Length > (1024 * 1024))
                return null;

            ITextDocument document;
            if (!_TextDocumentFactoryService.TryGetTextDocument(view.TextDataModel.DocumentBuffer, out document))
                return null;

            ITextUndoHistory history;
            if (!_UndoHistoryRegistry.TryGetHistory(view.TextBuffer, out history))
            {
                Debug.Fail("Unexpected: couldn't get an undo history for the given text buffer");
                return null;
            }

            return new InformationBarMargin(view, document, _OperationsFactory.GetEditorOperations(view), history);
        }
    }
    #endregion
}
