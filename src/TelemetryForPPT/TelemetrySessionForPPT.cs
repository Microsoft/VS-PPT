using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Microsoft.VisualStudio.TelemetryForPPT
{
    public class TelemetrySessionForPPT : IPartImportsSatisfiedNotification
    {
        private static TelemetrySessionForPPT s_instance = new TelemetrySessionForPPT();
        private ITelemetrySessionFactory _telemetrySessionFactory;

        [ImportMany]
        private List<Lazy<ITelemetrySessionFactory, IOrderable>> _unOrderedTelemetrySessionFactoryExports = null;

        private TelemetrySessionForPPT()
        {
        }

        private ITelemetrySessionFactory TelemetrySessionFactory
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (_telemetrySessionFactory == null)
                {
                    IComponentModel componentModel = ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel)) as IComponentModel;
                    if (componentModel != null)
                    {
                        componentModel.DefaultCompositionService.SatisfyImportsOnce(this);
                    }
                }

                return _telemetrySessionFactory ?? (_telemetrySessionFactory = new DefaultTelemetrySessionFactory());
            }
        }

        public void OnImportsSatisfied()
        {
            try
            {
                var lazyFActory = Orderer.Order(_unOrderedTelemetrySessionFactoryExports).FirstOrDefault();
                if (lazyFActory != null)
                {
                    _telemetrySessionFactory = lazyFActory.Value;
                }
            }
            catch
            {
                Debug.Fail("Failed to instantiate ITelemetrySessionFactory.");
            }
        }

        public static ITelemetrySession Create(Assembly callingAssembly)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return s_instance.TelemetrySessionFactory.CreateSession(callingAssembly);
        }
    }
}
