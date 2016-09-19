// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public class VSMSBuildNuGetProject : MSBuildNuGetProject
    {
        private readonly EnvDTEProject _project;

        public VSMSBuildNuGetProject(
            EnvDTEProject project,
            IMSBuildNuGetProjectSystem msbuildNuGetProjectSystem,
            string folderNuGetProjectPath,
            string packagesConfigFolderPath) : base(
                msbuildNuGetProjectSystem,
                folderNuGetProjectPath,
                packagesConfigFolderPath)
        {
            _project = project;
        }

        public override async Task<IReadOnlyList<ExternalProjectReference>> GetProjectReferenceClosureAsync(
            ExternalProjectReferenceContext context)
        {
            return await VsProjectReferenceUtility.GetProjectReferenceClosureAsync(_project, context);
        }
    }
}
