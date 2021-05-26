using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace Microsoft.VisualStudio.Editor.PeekF1
{
    [Export(typeof(IPeekableItemSourceProvider))]
    [ContentType("text")]
    [Name("Peek Help Provider")]
    internal class F1PeekableItemSourceProvider : IPeekableItemSourceProvider
    {
        public IPeekableItemSource TryCreatePeekableItemSource(ITextBuffer textBuffer)
        {
            // we don't need textBuffer because we get Help URL via DTE
            return new F1PeekableItemSource();
        }
    }
}
