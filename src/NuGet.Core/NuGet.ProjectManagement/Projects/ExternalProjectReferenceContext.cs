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
        /// Logger
        /// </summary>
        public ILogger Logger { get; }
    }
}
