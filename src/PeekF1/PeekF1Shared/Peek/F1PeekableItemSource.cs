using EnvDTE;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Editor.PeekF1
{
    internal class F1PeekableItemSource : IPeekableItemSource
    {
        public void AugmentPeekSession(IPeekSession session, IList<IPeekableItem> peekableItems)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (session.RelationshipName == PeekHelpRelationship.Instance.Name)
            {
                DTE dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE;
                Assumes.Present(dte);
                var attributes = new Dictionary<string, string[]>(StringComparer.CurrentCultureIgnoreCase);
                ExtractAttributes(dte.ActiveWindow.ContextAttributes, attributes);
                ExtractAttributes(dte.ContextAttributes, attributes);

                string helpUrl = HelpUrlBuilder.Build(attributes);
                if (!string.IsNullOrWhiteSpace(helpUrl))
                {
                    peekableItems.Add(new F1PeekableItem(helpUrl));
                }
            }
        }

        private static void ExtractAttributes(ContextAttributes contextAttributes, Dictionary<string, string[]> attributes)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                ExtractAttributes(contextAttributes.HighPriorityAttributes, attributes);
                if (contextAttributes != null)
                {
                    contextAttributes.Refresh();
                    foreach (ContextAttribute attr in contextAttributes)
                    {
                        var attrCollection = attr.Values as ICollection;
                        if (attrCollection != null)
                        {
                            string[] values = new string[attrCollection.Count];
                            int i = 0;
                            foreach (string value in attrCollection)
                            {
                                values[i++] = value;
                            }
                            attributes.Add(attr.Name, values);
                        }
                    }
                }
            }
            catch { }
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}
