using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Editor.PeekF1
{
    internal class F1PeekableItem : IPeekableItem
    {
        private string _helpUrl;

        public F1PeekableItem(string helpUrl)
        {
            if (string.IsNullOrWhiteSpace(helpUrl))
            {
                throw new ArgumentException("helpUrl");
            }

            _helpUrl = helpUrl;
        }

        public string DisplayName
        {
            get { return "Help"; }
        }

        public IPeekResultSource GetOrCreateResultSource(string relationshipName)
        {
            return new F1PeekResultSource(_helpUrl);
        }

        public IEnumerable<IPeekRelationship> Relationships
        {
            get { return new IPeekRelationship[] { PeekHelpRelationship.Instance }; }
        }
    }
}
