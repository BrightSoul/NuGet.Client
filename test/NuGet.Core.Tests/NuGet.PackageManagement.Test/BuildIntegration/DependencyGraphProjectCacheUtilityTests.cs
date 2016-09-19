// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.PackageManagement.Test;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Test
{
    public class DependencyGraphProjectCacheUtilityTests
    {
        [Fact]
        public void DependencyGraphProjectCacheUtility_GetExternalClosure_Empty()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>();

            // Act
            var closure = DependencyGraphProjectCacheUtility.GetExternalClosure("test", references);

            // Assert
            Assert.Equal(0, closure.Count);
        }

        [Fact]
        public void DependencyGraphProjectCacheUtility_GetExternalClosure_NotFound()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                BuildIntegrationTestUtility.CreateReference("a", "b"),
                BuildIntegrationTestUtility.CreateReference("b")
            };

            // Act
            var closure = DependencyGraphProjectCacheUtility.GetExternalClosure("z", references);

            // Assert
            Assert.Equal(0, closure.Count);
        }

        [Fact]
        public void DependencyGraphProjectCacheUtility_GetExternalClosure_Single()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                BuildIntegrationTestUtility.CreateReference("a", "b"),
                BuildIntegrationTestUtility.CreateReference("b")
            };

            // Act
            var closure = DependencyGraphProjectCacheUtility.GetExternalClosure("b", references);

            // Assert
            Assert.Equal("b", closure.Single().UniqueName);
        }

        [Fact]
        public void DependencyGraphProjectCacheUtility_GetExternalClosure_Basic()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                BuildIntegrationTestUtility.CreateReference("a", "b", "c"),
                BuildIntegrationTestUtility.CreateReference("b"),
                BuildIntegrationTestUtility.CreateReference("c", "d"),
                BuildIntegrationTestUtility.CreateReference("d")
            };

            // Act
            var closure = DependencyGraphProjectCacheUtility.GetExternalClosure("a", references);

            // Assert
            Assert.Equal(4, closure.Count);
        }

        [Fact]
        public void DependencyGraphProjectCacheUtility_GetExternalClosure_Subset()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                BuildIntegrationTestUtility.CreateReference("a", "b", "c"),
                BuildIntegrationTestUtility.CreateReference("b"),
                BuildIntegrationTestUtility.CreateReference("c", "d"),
                BuildIntegrationTestUtility.CreateReference("d")
            };

            // Act
            var closure = DependencyGraphProjectCacheUtility.GetExternalClosure("c", references);

            // Assert
            Assert.Equal(2, closure.Count);
        }

        [Fact]
        public void DependencyGraphProjectCacheUtility_GetExternalClosure_Cycle()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                BuildIntegrationTestUtility.CreateReference("a", "b"),
                BuildIntegrationTestUtility.CreateReference("b", "a")
            };

            // Act
            var closure = DependencyGraphProjectCacheUtility.GetExternalClosure("a", references);

            // Assert
            Assert.Equal(2, closure.Count);
        }

        [Fact]
        public void DependencyGraphProjectCacheUtility_GetExternalClosure_Overlapping()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                BuildIntegrationTestUtility.CreateReference("a", "b", "c"),
                BuildIntegrationTestUtility.CreateReference("b", "d", "c"),
                BuildIntegrationTestUtility.CreateReference("c", "d"),
                BuildIntegrationTestUtility.CreateReference("d", "e"),
                BuildIntegrationTestUtility.CreateReference("e"),
            };

            // Act
            var closure = DependencyGraphProjectCacheUtility.GetExternalClosure("a", references);

            // Assert
            Assert.Equal(5, closure.Count);
        }

        [Fact]
        public void DependencyGraphProjectCacheUtility_GetExternalClosure_OverlappingSubSet()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                BuildIntegrationTestUtility.CreateReference("a", "b", "c"),
                BuildIntegrationTestUtility.CreateReference("b", "d", "c"),
                BuildIntegrationTestUtility.CreateReference("c", "d"),
                BuildIntegrationTestUtility.CreateReference("d", "e"),
                BuildIntegrationTestUtility.CreateReference("e"),
            };

            // Act
            var closure = DependencyGraphProjectCacheUtility.GetExternalClosure("b", references);

            // Assert
            Assert.Equal(4, closure.Count);
        }

        [Fact]
        public void DependencyGraphProjectCacheUtility_GetExternalClosure_MissingReference()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                BuildIntegrationTestUtility.CreateReference("a", "b", "c"),
                BuildIntegrationTestUtility.CreateReference("b", "d", "c"),
                BuildIntegrationTestUtility.CreateReference("c", "d"),
                BuildIntegrationTestUtility.CreateReference("d", "e"),
            };

            // Act
            var closure = DependencyGraphProjectCacheUtility.GetExternalClosure("b", references);

            // Assert
            Assert.Equal(3, closure.Count);
        }

        [Fact]
        public void DependencyGraphProjectCacheUtility_GetDirectReferences_Basic()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                BuildIntegrationTestUtility.CreateReference("a", "b", "c"),
                BuildIntegrationTestUtility.CreateReference("b"),
                BuildIntegrationTestUtility.CreateReference("c", "d"),
                BuildIntegrationTestUtility.CreateReference("d")
            };

            // Act
            var actual = DependencyGraphProjectCacheUtility
                .GetDirectReferences("a", references)
                .OrderBy(r => r.UniqueName)
                .ToList();

            // Assert
            Assert.Equal(2, actual.Count);
            Assert.Equal("b", actual[0].UniqueName);
            Assert.Equal("c", actual[1].UniqueName);
        }

        [Fact]
        public void DependencyGraphProjectCacheUtility_GetDirectReferences_MissingReference()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                BuildIntegrationTestUtility.CreateReference("a", "b", "c"),
                BuildIntegrationTestUtility.CreateReference("b"),
                BuildIntegrationTestUtility.CreateReference("d")
            };

            // Act
            var actual = DependencyGraphProjectCacheUtility
                .GetDirectReferences("a", references)
                .OrderBy(r => r.UniqueName)
                .ToList();

            // Assert
            Assert.Equal(1, actual.Count);
            Assert.Equal("b", actual[0].UniqueName);
        }

        [Fact]
        public void DependencyGraphProjectCacheUtility_GetDirectReferences_MissingRoot()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                BuildIntegrationTestUtility.CreateReference("a", "b", "c"),
                BuildIntegrationTestUtility.CreateReference("b"),
                BuildIntegrationTestUtility.CreateReference("d")
            };

            // Act
            var actual = DependencyGraphProjectCacheUtility
                .GetDirectReferences("e", references)
                .OrderBy(r => r.UniqueName)
                .ToList();

            // Assert
            Assert.Equal(0, actual.Count);
        }

        [Fact]
        public async Task DependencyGraphProjectCacheUtility_IsRestoreRequiredDependencyChanged()
        {
            // Arrange
            var projectName = "testproj";

            using (var packagesFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                JsonConfigUtility.AddDependency(json, new PackageDependency("nuget.versioning", VersionRange.Parse("1.0.7")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var result = await BuildIntegratedRestoreUtility.RestoreAsync(
                    project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    packagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var projects = new List<BuildIntegratedNuGetProject>() { project };

                JsonConfigUtility.AddDependency(json, new PackageDependency("nuget.core", VersionRange.Parse("2.8.3")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                var packageFolders = new List<string>() { packagesFolder };

                // Act
                var b = DependencyGraphProjectCacheUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.True(b);
            }
        }

        [Fact]
        public async Task DependencyGraphProjectCacheUtility_IsRestoreRequiredChangedSha512()
        {
            // Arrange
            var projectName = "testproj";

            using (var packagesFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                JsonConfigUtility.AddDependency(json, new NuGet.Packaging.Core.PackageDependency("nuget.versioning", VersionRange.Parse("1.0.7")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
            {
                Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
            };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var result = await BuildIntegratedRestoreUtility.RestoreAsync(
                    project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    packagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var projects = new List<BuildIntegratedNuGetProject>() { project };

                var packageFolders = new List<string>() { packagesFolder };

                var resolver = new VersionFolderPathResolver(packagesFolder);
                var hashPath = resolver.GetHashPath("nuget.versioning", NuGetVersion.Parse("1.0.7"));

                using (var writer = new StreamWriter(hashPath))
                {
                    writer.Write("ANAWESOMELYWRONGHASH!!!");
                }

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                // Act
                var b = DependencyGraphProjectCacheUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.True(b);
            }
        }

        [Fact]
        public async Task DependencyGraphProjectCacheUtility_IsRestoreRequiredMissingPackage()
        {
            // Arrange
            var projectName = "testproj";

            using (var packagesFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                JsonConfigUtility.AddDependency(json, new NuGet.Packaging.Core.PackageDependency("nuget.versioning", VersionRange.Parse("1.0.7")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var result = await BuildIntegratedRestoreUtility.RestoreAsync(
                    project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    packagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var projects = new List<BuildIntegratedNuGetProject>() { project };

                var resolver = new VersionFolderPathResolver(packagesFolder);
                var packageFolders = new List<string>() { packagesFolder };

                var pathToDelete = resolver.GetInstallPath("nuget.versioning", NuGetVersion.Parse("1.0.7"));

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                // Act
                TestFileSystemUtility.DeleteRandomTestFolder(pathToDelete);

                var b = DependencyGraphProjectCacheUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.True(b);
            }
        }

        [Fact]
        public async Task DependencyGraphProjectCacheUtility_IsRestoreNotRequiredWithFloatingVersion()
        {
            // Arrange
            var projectName = "testproj";

            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                json.Add("dependencies", JObject.Parse("{ \"nuget.versioning\": \"1.0.*\" }"));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
            {
                Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
            };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var effectiveGlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);

                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    effectiveGlobalPackagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var projects = new List<BuildIntegratedNuGetProject>() { project };

                var packageFolders = new List<string>() { effectiveGlobalPackagesFolder };

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                // Act
                var b = DependencyGraphProjectCacheUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.False(b);
            }
        }

        [Fact]
        public async Task DependencyGraphProjectCacheUtility_IsRestoreRequiredWithNoChanges()
        {
            // Arrange
            var projectName = "testproj";

            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                JsonConfigUtility.AddDependency(json, new NuGet.Packaging.Core.PackageDependency("nuget.versioning", VersionRange.Parse("1.0.7")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
            {
                Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
            };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var effectiveGlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);

                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    effectiveGlobalPackagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var projects = new List<BuildIntegratedNuGetProject>() { project };

                var packageFolders = new List<string>() { effectiveGlobalPackagesFolder };

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                // Act
                var b = DependencyGraphProjectCacheUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.False(b);
            }
        }

        [Fact]
        public async Task DependencyGraphProjectCacheUtility_IsRestoreRequiredWithNoChangesFallbackFolder()
        {
            // Arrange
            var projectName = "testproj";

            using (var globalFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var fallbackFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                JsonConfigUtility.AddDependency(json, new NuGet.Packaging.Core.PackageDependency("nuget.versioning", VersionRange.Parse("1.0.7")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                // Restore to the fallback folder
                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    fallbackFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var projects = new List<BuildIntegratedNuGetProject>() { project };

                var packageFolders = new List<string>() { globalFolder, fallbackFolder };

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                // Act
                var b = DependencyGraphProjectCacheUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.False(b);
            }
        }

        [Fact]
        public async Task DependencyGraphProjectCacheUtility_IsRestoreRequiredWithNoChangesFallbackFolderIgnoresOtherHashes()
        {
            // Arrange
            var projectName = "testproj";

            using (var globalFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var fallbackFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                JsonConfigUtility.AddDependency(json, new NuGet.Packaging.Core.PackageDependency("nuget.versioning", VersionRange.Parse("1.0.7")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                // Restore to the fallback folder
                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    fallbackFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                // Restore to global folder
                result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    globalFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var resolver = new VersionFolderPathResolver(fallbackFolder);
                var hashPath = resolver.GetHashPath("nuget.versioning", NuGetVersion.Parse("1.0.7"));
                File.WriteAllText(hashPath, "AA00F==");

                var projects = new List<BuildIntegratedNuGetProject>() { project };

                var packageFolders = new List<string>() { globalFolder, fallbackFolder };

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                // Act
                var b = DependencyGraphProjectCacheUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.False(b);
            }
        }

        [Fact]
        public async Task DependencyGraphProjectCacheUtility_CacheDiffersOnClosure()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath2 = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
                BuildIntegrationTestUtility.CreateConfigJson(randomConfig);
                BuildIntegrationTestUtility.CreateConfigJson(randomConfig2);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath,
                    "project1");

                var project1 = new TestBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);
                project1.ProjectClosure = new List<ExternalProjectReference>()
                {
                    BuildIntegrationTestUtility.CreateReference("a", "a/project.json", new string[] { "b/project.json" }),
                    BuildIntegrationTestUtility.CreateReference("b", "b/project.json", new string[] { }),
                };

                var msBuildNuGetProjectSystem2 = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath2,
                    "project2");

                var project2 = new TestBuildIntegratedNuGetProject(randomConfig2, msBuildNuGetProjectSystem2);
                project2.ProjectClosure = new List<ExternalProjectReference>() { };

                var projects = new List<BuildIntegratedNuGetProject>();
                projects.Add(project1);
                projects.Add(project2);

                var cache = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                    projects,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext());

                var projects2 = new List<BuildIntegratedNuGetProject>();
                project1.ProjectClosure = new List<ExternalProjectReference>()
                {
                    BuildIntegrationTestUtility.CreateReference("a", "a/project.json", new string[] { "d/project.json" }),
                    BuildIntegrationTestUtility.CreateReference("d", "d/project.json", new string[] { }),
                };

                projects2.Add(project1);
                projects2.Add(project2);
                var cache2 = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                    projects2,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext());

                // Act
                var b = DependencyGraphProjectCacheUtility.CacheHasChanges(cache, cache2);

                // Assert
                Assert.True(b);
            }
        }

        [Fact]
        public async Task DependencyGraphProjectCacheUtility_CacheHasChanges_ReturnsTrue_IfSupportProfilesDiffer()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath2 = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
                var supports = new JObject
                {
                    ["uap.app"] = new JObject()
                };
                var configJson = new JObject
                {
                    ["frameworks"] = new JObject
                    {
                        ["uap10.0"] = new JObject()
                    },
                    ["supports"] = supports
                };

                File.WriteAllText(randomConfig, configJson.ToString());
                Thread.Sleep(2000);
                File.WriteAllText(randomConfig2, configJson.ToString());

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath,
                    "project1");

                var project1 = new TestBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);
                project1.ProjectClosure = new List<ExternalProjectReference>()
                {
                    BuildIntegrationTestUtility.CreateReference("a", "a/project.json", new string[] { "b/project.json" }),
                    BuildIntegrationTestUtility.CreateReference("b", "b/project.json", new string[] { }),
                };

                var msBuildNuGetProjectSystem2 = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath2,
                    "project2");

                var project2 = new TestBuildIntegratedNuGetProject(randomConfig2, msBuildNuGetProjectSystem2);
                project2.ProjectClosure = new List<ExternalProjectReference>() { };

                var projects = new List<BuildIntegratedNuGetProject>();
                projects.Add(project1);
                projects.Add(project2);

                var cache = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                    projects,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext());

                supports["net46"] = new JObject();
                File.WriteAllText(randomConfig, configJson.ToString());
                var cache2 = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                    projects,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext());

                // Act 1
                var result1 = DependencyGraphProjectCacheUtility.CacheHasChanges(cache, cache2);

                // Assert 1
                Assert.True(result1);

                // Act 2
                var cache3 = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                    projects,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext());
                var result2 = DependencyGraphProjectCacheUtility.CacheHasChanges(cache2, cache3);

                // Assert 2
                Assert.False(result2);
            }
        }

        [Fact]
        public async Task DependencyGraphProjectCacheUtility_CacheDiffersOnProjects()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath2 = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
                BuildIntegrationTestUtility.CreateConfigJson(randomConfig);
                BuildIntegrationTestUtility.CreateConfigJson(randomConfig2);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath,
                    "project1");

                var project1 = new TestBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);
                project1.ProjectClosure = new List<ExternalProjectReference>()
                {
                    BuildIntegrationTestUtility.CreateReference("a", "a/project.json", new string[] { "b/project.json" }),
                    BuildIntegrationTestUtility.CreateReference("b", "b/project.json", new string[] { }),
                };

                var msBuildNuGetProjectSystem2 = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath2,
                    "project2");

                var project2 = new TestBuildIntegratedNuGetProject(randomConfig2, msBuildNuGetProjectSystem2);
                project2.ProjectClosure = new List<ExternalProjectReference>() { };

                var projects = new List<BuildIntegratedNuGetProject>();
                projects.Add(project1);

                var cache = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                    projects,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext());

                // Add a new project to the second cache
                projects.Add(project2);
                var cache2 = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                    projects,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext());

                // Act
                var b = DependencyGraphProjectCacheUtility.CacheHasChanges(cache, cache2);

                // Assert
                Assert.True(b);
            }
        }

        [Fact]
        public async Task DependencyGraphProjectCacheUtility_SameCache()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath2 = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
                BuildIntegrationTestUtility.CreateConfigJson(randomConfig);
                BuildIntegrationTestUtility.CreateConfigJson(randomConfig2);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath,
                    "project1");

                var project1 = new TestBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);
                project1.ProjectClosure = new List<ExternalProjectReference>()
                {
                    BuildIntegrationTestUtility.CreateReference("a", "a/project.json", new string[] { "b/project.json" }),
                    BuildIntegrationTestUtility.CreateReference("b", "b/project.json", new string[] { }),
                };

                var msBuildNuGetProjectSystem2 = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath2,
                    "project2");

                var project2 = new TestBuildIntegratedNuGetProject(randomConfig2, msBuildNuGetProjectSystem2);
                project2.ProjectClosure = new List<ExternalProjectReference>() { };

                var projects = new List<BuildIntegratedNuGetProject>();
                projects.Add(project1);
                projects.Add(project2);

                var cache = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                    projects,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext());
                var cache2 = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                    projects,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext());

                // Act
                var b = DependencyGraphProjectCacheUtility.CacheHasChanges(cache, cache2);

                // Assert
                Assert.False(b);
            }
        }

        [Fact]
        public async Task DependencyGraphProjectCacheUtility_CreateCache()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath2 = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
                BuildIntegrationTestUtility.CreateConfigJson(randomConfig);
                BuildIntegrationTestUtility.CreateConfigJson(randomConfig2);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath,
                    "project1");

                var project1 = new TestBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);
                project1.ProjectClosure = new List<ExternalProjectReference>()
                {
                    BuildIntegrationTestUtility.CreateReference("a", "a/project.json", new string[] { "b/project.json" }),
                    BuildIntegrationTestUtility.CreateReference("b", "b/project.json", new string[] { }),
                };

                var msBuildNuGetProjectSystem2 = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath2,
                    "project2");

                var project2 = new TestBuildIntegratedNuGetProject(randomConfig2, msBuildNuGetProjectSystem2);
                project2.ProjectClosure = new List<ExternalProjectReference>() { };

                var projects = new List<BuildIntegratedNuGetProject>();
                projects.Add(project1);
                projects.Add(project2);

                // Act
                var cache = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                    projects,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext());

                // Assert
                Assert.Equal(2, cache.Count);
                Assert.Equal(2, cache[project1.MSBuildProjectPath].ReferenceClosure.Count);
                Assert.Equal(0, cache[project2.MSBuildProjectPath].ReferenceClosure.Count);
                Assert.Equal("a|b", string.Join("|", cache[project1.MSBuildProjectPath].ReferenceClosure));
            }
        }
    }
}
