﻿using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Versioning;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace NuGet.Commands
{
    /// <summary>
    /// In Memory dg file provider.
    /// </summary>
    public class DependencyGraphSpecRequestProvider : IPreLoadedRestoreRequestProvider
    {
        private readonly DependencyGraphSpec _dgFile;
        private readonly RestoreCommandProvidersCache _providerCache;
        private readonly Dictionary<string, PackageSpec> _projectJsonCache = new Dictionary<string, PackageSpec>(StringComparer.Ordinal);

        public DependencyGraphSpecRequestProvider(
            RestoreCommandProvidersCache providerCache,
            DependencyGraphSpec dgFile)
        {
            _dgFile = dgFile;
            _providerCache = providerCache;
        }

        public Task<IReadOnlyList<RestoreSummaryRequest>> CreateRequests(RestoreArgs restoreContext)
        {
            var requests = GetRequestsFromItems(restoreContext, _dgFile);

            return Task.FromResult<IReadOnlyList<RestoreSummaryRequest>>(requests);
        }

        private IReadOnlyList<RestoreSummaryRequest> GetRequestsFromItems(RestoreArgs restoreContext, DependencyGraphSpec dgFile)
        {
            if (restoreContext == null)
            {
                throw new ArgumentNullException(nameof(restoreContext));
            }

            if (dgFile == null)
            {
                throw new ArgumentNullException(nameof(dgFile));
            }

            // Write the dg file to disk of the NUGET_PERSIST_DG is set.
            MSBuildRestoreUtility.PersistDGFileIfDebugging(dgFile, restoreContext.Log);

            // Validate the dg file input, this throws if errors are found.
            SpecValidationUtility.ValidateDependencySpec(dgFile);

            // Create requests
            var requests = new List<RestoreSummaryRequest>();

            foreach (var projectNameToRestore in dgFile.Restore)
            {
                var closure = dgFile.GetClosure(projectNameToRestore);

                var externalClosure = new HashSet<ExternalProjectReference>(closure.Select(GetExternalProject));

                var rootProject = externalClosure.Single(p =>
                    StringComparer.Ordinal.Equals(projectNameToRestore, p.ProjectName));

                var request = Create(rootProject, externalClosure, restoreContext, settingsOverride: null);

                requests.Add(request);
            }

            return requests;
        }

        private static ExternalProjectReference GetExternalProject(PackageSpec rootProject)
        {
            var projectReferences = rootProject.RestoreMetadata?.ProjectReferences ?? new List<ProjectRestoreReference>();

            return new ExternalProjectReference(
                rootProject.RestoreMetadata.ProjectUniqueName,
                rootProject,
                rootProject.RestoreMetadata?.ProjectPath,
                projectReferences.Select(p => p.ProjectUniqueName));
        }

        private RestoreSummaryRequest Create(
            ExternalProjectReference project,
            HashSet<ExternalProjectReference> projectReferenceClosure,
            RestoreArgs restoreContext,
            ISettings settingsOverride)
        {
            // Get settings relative to the input file
            var rootPath = Path.GetDirectoryName(project.PackageSpec.FilePath);

            var settings = settingsOverride;

            if (settings == null)
            {
                settings = restoreContext.GetSettings(rootPath);
            }

            var globalPath = restoreContext.GetEffectiveGlobalPackagesFolder(rootPath, settings);
            var fallbackPaths = restoreContext.GetEffectiveFallbackPackageFolders(settings);

            var sources = restoreContext.GetEffectiveSources(settings);

            var sharedCache = _providerCache.GetOrCreate(
                globalPath,
                fallbackPaths,
                sources,
                restoreContext.CacheContext,
                restoreContext.Log);

            // Create request
            var request = new RestoreRequest(
                project.PackageSpec,
                sharedCache,
                restoreContext.Log,
                disposeProviders: false);

            // Set output type
            request.RestoreOutputType = project.PackageSpec?.RestoreMetadata?.OutputType ?? RestoreOutputType.Unknown;
            request.RestoreOutputPath = project.PackageSpec?.RestoreMetadata?.OutputPath ?? rootPath;

            // Standard properties
            restoreContext.ApplyStandardProperties(request);

            // Add project references
            request.ExternalProjects = projectReferenceClosure.ToList();

            // The lock file is loaded later since this is an expensive operation
            var summaryRequest = new RestoreSummaryRequest(
                request,
                project.MSBuildProjectPath,
                settings,
                sources);

            return summaryRequest;
        }

        /// <summary>
        /// Return all references for a given project path.
        /// References is modified by this method.
        /// This includes the root project.
        /// </summary>
        private static void CollectReferences(
            ExternalProjectReference root,
            Dictionary<string, ExternalProjectReference> allProjects,
            HashSet<ExternalProjectReference> references)
        {
            if (references.Add(root))
            {
                foreach (var child in root.ExternalProjectReferences)
                {
                    ExternalProjectReference childProject;
                    if (!allProjects.TryGetValue(child, out childProject))
                    {
                        // Let the resolver handle this later
                        Debug.Fail($"Missing project {childProject}");
                    }

                    // Recurse down
                    CollectReferences(childProject, allProjects, references);
                }
            }
        }
    }
}
