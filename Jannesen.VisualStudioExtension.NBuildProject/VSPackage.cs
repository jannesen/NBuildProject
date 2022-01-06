using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.ProjectSystem;

namespace Jannesen.VisualStudioExtension.NBuildProject.VSIX
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", VSPackage.Version, IconResourceID = 400)]
    [Guid(VSPackage.PackageGuid)]
    [Description("NBuildProject Visual Studio Extension.")]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class VSPackage: AsyncPackage
    {
        public      const       string                              PackageGuid     = "24077884-E16E-4CC2-937F-7CA74CCE53AE";
        public      const       string                              Version         = "1.10.00.000";        //@VERSIONINFO

        public                                                      VSPackage()
        {
        }
    }
}
