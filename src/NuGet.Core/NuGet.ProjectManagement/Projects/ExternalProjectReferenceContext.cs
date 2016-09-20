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

            ProjectCache = new Dictionary<string, DependencyGraphProjectCacheEntry>(
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
        /// A cache of the files in a project that can have references and a last modified time. In practice, this is
        /// a list of all project.json and MSBuild project files in a closure. The key is the full path to the MSBuild
        /// project file.
        /// </summary>
        public Dictionary<string, DependencyGraphProjectCacheEntry> ProjectCache { get; set; }

        /// <summary>
        /// Logger
        /// </summary>
        public ILogger Logger { get; }
    }
}
