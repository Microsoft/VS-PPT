using Microsoft.VisualStudio.Language.Intellisense;

namespace Microsoft.VisualStudio.Editor.PeekF1
{
    internal sealed class PeekHelpRelationship : IPeekRelationship
    {
        private PeekHelpRelationship() { }

        private static PeekHelpRelationship s_instance;

        internal static PeekHelpRelationship Instance
        {
            get { return s_instance ?? (s_instance = new PeekHelpRelationship()); }
        }

        public string DisplayName
        {
            get { return "Help"; }
        }

        public string Name
        {
            get { return "Help"; }
        }
    }
}
