using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.ProjectSystem;

namespace Jannesen.VisualStudioExtension.NBuildProject.CPS
{
    [Export]
    [AppliesTo(NBuildProjectUnconfiguredProject.UniqueCapability)]
    internal class NBuildProjectConfiguredProject
    {
        [Import]
        internal        ConfiguredProject       ConfiguredProject   { get; private set; }

        [Import]
        internal        ProjectProperties       Properties          { get; private set; }

        public                                  NBuildProjectConfiguredProject()
        {
        }
    }
}
