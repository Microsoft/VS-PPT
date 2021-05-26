using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TelemetryForPPT;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Editor.PeekF1
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal class TextViewCreationListener : IVsTextViewCreationListener
    {
        [Import(typeof(IVsEditorAdaptersFactoryService))]
        private IVsEditorAdaptersFactoryService _editorFactory = null;

        private ITelemetrySession _telemetrySession;

        [Import]
        private IPeekBroker _peekBroker = null;

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            IWpfTextView textView = _editorFactory.GetWpfTextView(textViewAdapter);
            if (textView == null)
            {
                return;
            }

            if (_telemetrySession == null)
            {
                _telemetrySession = TelemetrySessionForPPT.Create(this.GetType().Assembly);
            }

            IOleCommandTarget next;
            var commandFilter = new CommandFilter(textView, _peekBroker, _telemetrySession);
            int hr = textViewAdapter.AddCommandFilter(commandFilter, out next);
            if (next != null)
            {
                commandFilter.Next = next;
            }
        }
    }
}
