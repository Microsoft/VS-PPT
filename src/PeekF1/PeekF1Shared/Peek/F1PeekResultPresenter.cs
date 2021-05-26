using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace Microsoft.VisualStudio.Editor.PeekF1
{
    [Export(typeof(IPeekResultPresenter))]
    [Name("Peek Help Presenter")]
    internal class F1PeekResultPresenter : IPeekResultPresenter
    {
        public IPeekResultPresentation TryCreatePeekResultPresentation(IPeekResult result)
        {
            F1PeekResult f1Result = result as F1PeekResult;
            if (f1Result != null)
            {
                return new F1PeekResultPresentation(f1Result);
            }

            return null;
        }
    }
}
