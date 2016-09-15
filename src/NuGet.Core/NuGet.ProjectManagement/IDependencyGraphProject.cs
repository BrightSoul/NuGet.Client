// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace NuGet.ProjectManagement
{
    public interface IDependencyGraphProject
    {
        /// <summary>
        /// Gets the path to the MSBuild project file. This is an absolute path.
        /// </summary>
        string MSBuildProjectPath { get; }

        /// <summary>
        /// Get the time when the project was last modified. This is used for cache invalidation.
        /// </summary>
        DateTimeOffset LastModified { get; }

        PackageSpec GetPackageSpecForRestore(ExternalProjectReferenceContext context);

        bool IsRestoreRequired(
            IEnumerable<VersionFolderPathResolver> pathResolvers,
            ISet<PackageIdentity> packagesChecked,
            ExternalProjectReferenceContext context);

        Task<IReadOnlyList<ExternalProjectReference>> GetProjectReferenceClosureAsync(
            ExternalProjectReferenceContext context);
    }
}
