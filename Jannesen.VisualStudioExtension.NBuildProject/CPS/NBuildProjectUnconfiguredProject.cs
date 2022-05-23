using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.VS;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Jannesen.VisualStudioExtension.NBuildProject.CPS
{
    [Export]
    [AppliesTo(NBuildProjectUnconfiguredProject.UniqueCapability)]
    [ProjectTypeRegistration(
        projectTypeGuid                     : ProjectTypeGuid,
        displayName                         : "#1",
        displayProjectFileExtensions        : "#2",
        defaultProjectExtension             : ProjectExtension,
        language                            : "NBuildProject",
        resourcePackageGuid                 : VSPackage.PackageGuidString,
        //Capabilities                        = "OpenProjectFile",
        //DisableAsynchronousProjectTreeLoad  = true,
        PossibleProjectExtensions           = ProjectExtension,
        ProjectTemplatesDir                 = @"ProjectTemplates")]
    [ProvideProjectItem(
        projectFactoryType                  : ProjectTypeGuid,
        itemCategoryName                    : "My Items",
        templatesDir                        : @"ItemTemplates",
        priority                            : 500)]
    internal class NBuildProjectUnconfiguredProject
    {
        public      const   string                                          ProjectTypeGuid  = "2286C783-8279-485C-B9A4-1E666CCB004B";
        public      const   string                                          ProjectExtension = "nbuildproj";
        internal    const   string                                          UniqueCapability = "NBuildProject";

        [ImportingConstructor]
        public                                                                      NBuildProjectUnconfiguredProject(UnconfiguredProject unconfiguredProject)
        {
            this.ProjectHierarchies = new OrderPrecedenceImportCollection<IVsHierarchy>(projectCapabilityCheckProvider: unconfiguredProject);
        }

        [Import]
        internal            UnconfiguredProject                                     UnconfiguredProject { get; }

        [Import]
        internal            IActiveConfiguredProjectSubscriptionService             SubscriptionService { get; }

        [Import]
        internal            IProjectThreadingService                                ThreadHandling { get; }

        [Import]
        internal            ActiveConfiguredProject<ConfiguredProject>              ActiveConfiguredProject { get; }

        [Import]
        internal            ActiveConfiguredProject<NBuildProjectConfiguredProject> MyActiveConfiguredProject { get; }

        [ImportMany(ExportContractNames.VsTypes.IVsProject, typeof(IVsProject))]
        internal            OrderPrecedenceImportCollection<IVsHierarchy>           ProjectHierarchies { get; }

        internal            IVsHierarchy                                            ProjectHierarchy
        {
            get { return this.ProjectHierarchies.Single().Value; }
        }
    }
}
