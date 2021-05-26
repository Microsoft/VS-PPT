using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Diagnostics;

namespace Microsoft.VisualStudio.Editor.PeekF1
{
    internal class F1PeekResult : IPeekResult
    {
        public bool CanNavigateTo
        {
            get { return true; }
        }

        public IPeekResultDisplayInfo DisplayInfo
        {
            get { return new PeekResultDisplayInfo("Help", "Help", "Help", "Help"); }
        }

#pragma warning disable 0067
        public event EventHandler Disposed;
#pragma warning restore 0067

        public string HelpUrl { get; private set; }

        public Action<IPeekResult, object, object> PostNavigationCallback
        {
            get
            {
                return null;
            }
        }

        public F1PeekResult(string helpUrl)
        {
            if (string.IsNullOrWhiteSpace(helpUrl))
            {
                throw new ArgumentException("helpUrl");
            }

            this.HelpUrl = helpUrl;
        }

        public void NavigateTo(object data)
        {
            string uri = this.HelpUrl;
            F1PeekResultScrollState f1PeekScrollState = data as F1PeekResultScrollState;
            if (f1PeekScrollState != null && f1PeekScrollState.BrowserUrl != null)
            {
                uri = f1PeekScrollState.BrowserUrl.AbsoluteUri;
            }

            Process.Start(uri);
        }

        public void Dispose()
        {
        }
    }
}
