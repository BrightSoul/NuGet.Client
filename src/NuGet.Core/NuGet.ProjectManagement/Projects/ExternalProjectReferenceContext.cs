using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.ProjectModel;

namespace NuGet.ProjectManagement
{
    public class ExternalProjectReferenceContext
    {
        /// <summary>
        /// Create a new build integrated project reference context and caches.
        /// </summary>
        public ExternalProjectReferenceContext(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            Logger = logger;

            DirectReferenceCache = new Dictionary<string, IReadOnlyList<ExternalProjectReference>>(
                StringComparer.OrdinalIgnoreCase);

            ClosureCache = new Dictionary<string, IReadOnlyList<ExternalProjectReference>>(
                StringComparer.OrdinalIgnoreCase);

            SpecCache = new Dictionary<string, PackageSpec>(
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// A cache of a project's direct references. The key is the full path to the project.
        /// </summary>
        public IDictionary<string, IReadOnlyList<ExternalProjectReference>> DirectReferenceCache { get; }

        /// <summary>
        /// A cache of the full closure of project references. The key is the full path to the project.
        /// </summary>
        public IDictionary<string, IReadOnlyList<ExternalProjectReference>> ClosureCache { get; }

        /// <summary>
        /// Cached project.json files
        /// </summary>
        public IDictionary<string, PackageSpec> SpecCache { get; }

        /// <summary>
        /// Logger
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Retrieves a project.json file from the cache. It will be added if it does not exist already.
        /// </summary>
        public PackageSpec GetOrCreateSpec(string projectName, string projectJsonPath)
        {
            PackageSpec spec;
            if (!SpecCache.TryGetValue(projectJsonPath, out spec))
            {
                // Read the spec and add it to the cache
                spec = JsonPackageSpecReader.GetPackageSpec(
                    projectName,
                    projectJsonPath);

                SpecCache.Add(projectJsonPath, spec);
            }

            return spec;
        }
    }
}
