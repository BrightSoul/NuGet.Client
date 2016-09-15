// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Helper class for calling the RestoreCommand
    /// </summary>
    public static class BuildIntegratedRestoreUtility
    {
        /// <summary>
        /// Restore a build integrated project and update the lock file
        /// </summary>
        public static async Task<RestoreResult> RestoreAsync(
            BuildIntegratedNuGetProject project,
            ExternalProjectReferenceContext context,
            IEnumerable<SourceRepository> sources,
            string effectiveGlobalPackagesFolder,
            IEnumerable<string> fallbackPackageFolders,
            CancellationToken token)
        {
            return await RestoreAsync(
                project,
                context,
                sources,
                effectiveGlobalPackagesFolder,
                fallbackPackageFolders,
                c => { },
                token);
        }

        /// <summary>
        /// Restore a build integrated project and update the lock file
        /// </summary>
        public static async Task<RestoreResult> RestoreAsync(
            BuildIntegratedNuGetProject project,
            ExternalProjectReferenceContext context,
            IEnumerable<SourceRepository> sources,
            string effectiveGlobalPackagesFolder,
            IEnumerable<string> fallbackPackageFolders,
            Action<SourceCacheContext> cacheContextModifier,
            CancellationToken token)
        {
            using (var cacheContext = new SourceCacheContext())
            {
                cacheContextModifier(cacheContext);

                var providers = RestoreCommandProviders.Create(effectiveGlobalPackagesFolder,
                    fallbackPackageFolders,
                    sources,
                    cacheContext,
                    context.Logger);

                // Restore
                var result = await RestoreAsync(
                    project,
                    project.PackageSpec,
                    context,
                    providers,
                    cacheContext,
                    token);

                // Throw before writing if this has been canceled
                token.ThrowIfCancellationRequested();

                // Write out the lock file and msbuild files
                await result.CommitAsync(context.Logger, token);

                return result;
            }
        }

        /// <summary>
        /// Restore without writing the lock file
        /// </summary>
        internal static async Task<RestoreResult> RestoreAsync(
            BuildIntegratedNuGetProject project,
            PackageSpec packageSpec,
            ExternalProjectReferenceContext context,
            RestoreCommandProviders providers,
            SourceCacheContext cacheContext,
            CancellationToken token)
        {
            // Restoring packages
            var logger = context.Logger;
            logger.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                Strings.BuildIntegratedPackageRestoreStarted,
                project.ProjectName));

            var request = new RestoreRequest(packageSpec, providers, cacheContext, logger);
            request.MaxDegreeOfConcurrency = PackageManagementConstants.DefaultMaxDegreeOfParallelism;

            // Add the existing lock file if it exists
            var lockFilePath = ProjectJsonPathUtilities.GetLockFilePath(project.JsonConfigPath);
            request.LockFilePath = lockFilePath;
            request.ExistingLockFile = LockFileUtilities.GetLockFile(lockFilePath, logger);

            // Find the full closure of project.json files and referenced projects
            var projectReferences = await project.GetProjectReferenceClosureAsync(context);
            request.ExternalProjects = projectReferences.ToList();

            token.ThrowIfCancellationRequested();

            var command = new RestoreCommand(request);

            // Execute the restore
            var result = await command.ExecuteAsync(token);

            // Report a final message with the Success result
            if (result.Success)
            {
                logger.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                    Strings.BuildIntegratedPackageRestoreSucceeded,
                    project.ProjectName));
            }
            else
            {
                logger.LogMinimal(string.Format(CultureInfo.CurrentCulture,
                    Strings.BuildIntegratedPackageRestoreFailed,
                    project.ProjectName));
            }

            return result;
        }

        /// <summary>
        /// Find all packages added to <paramref name="updatedLockFile"/>.
        /// </summary>
        public static IReadOnlyList<PackageIdentity> GetAddedPackages(
            LockFile originalLockFile,
            LockFile updatedLockFile)
        {
            var updatedPackages = updatedLockFile.Targets.SelectMany(target => target.Libraries)
                .Where(library => library.Type == LibraryType.Package)
                .Select(library => new PackageIdentity(library.Name, library.Version));

            var originalPackages = originalLockFile.Targets.SelectMany(target => target.Libraries)
                .Where(library => library.Type == LibraryType.Package)
                .Select(library => new PackageIdentity(library.Name, library.Version));

            var results = updatedPackages.Except(originalPackages, PackageIdentity.Comparer).ToList();

            return results;
        }

        /// <summary>
        /// Creates an index of the project unique name to the cache entry.
        /// The cache entry contains the project and the closure of project.json files.
        /// </summary>
        public static async Task<Dictionary<string, BuildIntegratedProjectCacheEntry>>
            CreateBuildIntegratedProjectStateCache(
                IReadOnlyList<IDependencyGraphProject> projects,
                ExternalProjectReferenceContext context)
        {
            var cache = new Dictionary<string, BuildIntegratedProjectCacheEntry>();

            // Find all project closures
            foreach (var project in projects)
            {
                // Get all project.json file paths in the closure
                var closure = await project.GetProjectReferenceClosureAsync(context);

                var files = new HashSet<string>(StringComparer.Ordinal);

                // Store the last modified date of the project.json file
                // If there are any changes a restore is needed
                var lastModified = project.LastModified;

                foreach (var reference in closure)
                {
                    if (!string.IsNullOrEmpty(reference.MSBuildProjectPath))
                    {
                        files.Add(reference.MSBuildProjectPath);
                    }

                    if (reference.PackageSpecPath != null)
                    {
                        files.Add(reference.PackageSpecPath);
                    }
                }

                var projectInfo = new BuildIntegratedProjectCacheEntry(files, lastModified);
                var projectPath = project.MSBuildProjectPath;

                if (!cache.ContainsKey(projectPath))
                {
                    cache.Add(projectPath, projectInfo);
                }
                else
                {
                    Debug.Fail("project list contains duplicate projects");
                }
            }

            return cache;
        }

        /// <summary>
        /// Verifies that the caches contain the same projects and that each project contains the same closure.
        /// This is used to detect if any projects have changed before verifying the lock files.
        /// </summary>
        public static bool CacheHasChanges(
            IReadOnlyDictionary<string, BuildIntegratedProjectCacheEntry> previousCache,
            IReadOnlyDictionary<string, BuildIntegratedProjectCacheEntry> currentCache)
        {
            foreach (var item in currentCache)
            {
                var projectName = item.Key;
                BuildIntegratedProjectCacheEntry projectInfo;
                if (!previousCache.TryGetValue(projectName, out projectInfo))
                {
                    // A new project was added, this needs a restore
                    return true;
                }

                if (item.Value.ProjectConfigLastModified?.Equals(projectInfo.ProjectConfigLastModified) != true)
                {
                    // project.json has been modified
                    return true;
                }

                if (!item.Value.ReferenceClosure.SetEquals(projectInfo.ReferenceClosure))
                {
                    // The project closure has changed
                    return true;
                }
            }

            // no project changes have occurred
            return false;
        }

        /// <summary>
        /// Validate that all project.lock.json files are validate for the project.json files,
        /// and that no packages are missing.
        /// If a full restore is required this will return false.
        /// </summary>
        /// <remarks>Floating versions and project.json files with supports require a full restore.</remarks>
        public static bool IsRestoreRequired(
            IReadOnlyList<IDependencyGraphProject> projects,
            IReadOnlyList<string> packageFolderPaths,
            ExternalProjectReferenceContext context)
        {
            var packagesChecked = new HashSet<PackageIdentity>();
            var pathResolvers = packageFolderPaths.Select(path => new VersionFolderPathResolver(path));

            return projects.Any(p => p.IsRestoreRequired(pathResolvers, packagesChecked, context));
        }

        /// <summary>
        /// Find the list of parent projects which directly or indirectly reference the child project.
        /// </summary>
        public static IReadOnlyList<BuildIntegratedNuGetProject> GetParentProjectsInClosure(
            IReadOnlyList<BuildIntegratedNuGetProject> projects,
            BuildIntegratedNuGetProject target,
            IReadOnlyDictionary<string, BuildIntegratedProjectCacheEntry> cache)
        {
            if (projects == null)
            {
                throw new ArgumentNullException(nameof(projects));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            var parents = new HashSet<BuildIntegratedNuGetProject>();

            var targetProjectJson = Path.GetFullPath(target.JsonConfigPath);

            foreach (var project in projects)
            {
                // do not count the target as a parent
                if (!target.Equals(project))
                {
                    BuildIntegratedProjectCacheEntry cacheEntry;

                    if (cache.TryGetValue(project.MSBuildProjectPath, out cacheEntry))
                    {
                        // find all projects which have a child reference matching the same project.json path as the target
                        if (cacheEntry.ReferenceClosure.Any(reference =>
                            string.Equals(targetProjectJson, Path.GetFullPath(reference), StringComparison.OrdinalIgnoreCase)))
                        {
                            parents.Add(project);
                        }
                    }
                }
            }

            // sort parents by name to make this more deterministic during restores
            return parents.OrderBy(parent => parent.ProjectName, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Find direct project references from a larger set of references.
        /// </summary>
        public static ISet<ExternalProjectReference> GetDirectReferences(
            string rootUniqueName,
            ISet<ExternalProjectReference> references)
        {
            var directReferences = new HashSet<ExternalProjectReference>();

            var root = references
                .FirstOrDefault(p => rootUniqueName.Equals(p.UniqueName, StringComparison.Ordinal));

            if (root == null)
            {
                return directReferences;
            }

            foreach (var uniqueName in root.ExternalProjectReferences)
            {
                var directReference = references
                    .FirstOrDefault(p => uniqueName.Equals(p.UniqueName, StringComparison.Ordinal));

                if (directReference != null)
                {
                    directReferences.Add(directReference);
                }
            }

            return directReferences;
        }

        /// <summary>
        /// Find the project closure from a larger set of references.
        /// </summary>
        public static ISet<ExternalProjectReference> GetExternalClosure(
            string rootUniqueName,
            ISet<ExternalProjectReference> references)
        {
            var closure = new HashSet<ExternalProjectReference>();

            // Start with the parent node
            var parent = references.FirstOrDefault(project =>
                    rootUniqueName.Equals(project.UniqueName, StringComparison.Ordinal));

            if (parent != null)
            {
                closure.Add(parent);
            }

            // Loop adding child projects each time
            var notDone = true;
            while (notDone)
            {
                notDone = false;

                foreach (var childName in closure
                    .Where(project => project.ExternalProjectReferences != null)
                    .SelectMany(project => project.ExternalProjectReferences)
                    .ToArray())
                {
                    var child = references.FirstOrDefault(project =>
                        childName.Equals(project.UniqueName, StringComparison.Ordinal));

                    // Continue until nothing new is added
                    if (child != null)
                    {
                        notDone |= closure.Add(child);
                    }
                }
            }

            return closure;
        }

        /// <summary>
        /// Find the list of child projects direct or indirect references of target project in
        /// reverse dependency order like the least dependent package first.
        /// </summary>
        public static void GetChildProjectsInClosure(BuildIntegratedNuGetProject target,
            IReadOnlyList<BuildIntegratedNuGetProject> projects,
            IList<BuildIntegratedNuGetProject> orderedChilds,
            HashSet<string> uniqueProjectNames,
            IReadOnlyDictionary<string, BuildIntegratedProjectCacheEntry> cache)
        {
            if (projects == null)
            {
                throw new ArgumentNullException(nameof(projects));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (orderedChilds == null)
            {
                orderedChilds = new List<BuildIntegratedNuGetProject>();
            }

            if (uniqueProjectNames == null)
            {
                uniqueProjectNames = new HashSet<string>();
            }

            uniqueProjectNames.Add(target.ProjectName);

            if (!orderedChilds.Contains(target))
            {
                BuildIntegratedProjectCacheEntry cacheEntry;
                if (cache.TryGetValue(target.MSBuildProjectPath, out cacheEntry))
                {
                    foreach (var reference in cacheEntry.ReferenceClosure)
                    {
                        var packageSpecPath = Path.GetFullPath(reference);
                        var depProject = projects.FirstOrDefault(
                            proj =>
                                StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(proj.JsonConfigPath),
                                    packageSpecPath));

                        if (depProject != null && !orderedChilds.Contains(depProject) && uniqueProjectNames.Add(depProject.ProjectName))
                        {
                            GetChildProjectsInClosure(depProject, projects, orderedChilds, uniqueProjectNames, cache);
                        }
                    }
                }
                orderedChilds.Add(target);
            }
        }
    }
}
