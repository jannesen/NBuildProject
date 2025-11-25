using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Jannesen.VisualStudioExtension.NBuildProject
{
    // VSPackage.Version is generated during build.

    [Guid(VSPackage.PackageGuidString)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", VSPackage.Version, IconResourceID = 400)]
    [Description("NBuildProject Visual Studio Extension.")]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed partial class VSPackage: AsyncPackage
    {
        public      const       string                              PackageGuidString   = "24077884-E16E-4CC2-937F-7CA74CCE53AE";

        public                                                      VSPackage()
        {
        }
    }
}
