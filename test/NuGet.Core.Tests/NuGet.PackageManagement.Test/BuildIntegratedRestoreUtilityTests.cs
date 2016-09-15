﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging;
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
    public class BuildIntegratedRestoreUtilityTests
    {
        [Fact]
        public void BuildIntegratedRestoreUtility_GetExternalClosure_Empty()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>();

            // Act
            var closure = BuildIntegratedRestoreUtility.GetExternalClosure("test", references);

            // Assert
            Assert.Equal(0, closure.Count);
        }

        [Fact]
        public void BuildIntegratedRestoreUtility_GetExternalClosure_NotFound()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                CreateReference("a", "b"),
                CreateReference("b")
            };

            // Act
            var closure = BuildIntegratedRestoreUtility.GetExternalClosure("z", references);

            // Assert
            Assert.Equal(0, closure.Count);
        }

        [Fact]
        public void BuildIntegratedRestoreUtility_GetExternalClosure_Single()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                CreateReference("a", "b"),
                CreateReference("b")
            };

            // Act
            var closure = BuildIntegratedRestoreUtility.GetExternalClosure("b", references);

            // Assert
            Assert.Equal("b", closure.Single().UniqueName);
        }

        [Fact]
        public void BuildIntegratedRestoreUtility_GetExternalClosure_Basic()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                CreateReference("a", "b", "c"),
                CreateReference("b"),
                CreateReference("c", "d"),
                CreateReference("d")
            };

            // Act
            var closure = BuildIntegratedRestoreUtility.GetExternalClosure("a", references);

            // Assert
            Assert.Equal(4, closure.Count);
        }

        [Fact]
        public void BuildIntegratedRestoreUtility_GetExternalClosure_Subset()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                CreateReference("a", "b", "c"),
                CreateReference("b"),
                CreateReference("c", "d"),
                CreateReference("d")
            };

            // Act
            var closure = BuildIntegratedRestoreUtility.GetExternalClosure("c", references);

            // Assert
            Assert.Equal(2, closure.Count);
        }

        [Fact]
        public void BuildIntegratedRestoreUtility_GetExternalClosure_Cycle()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                CreateReference("a", "b"),
                CreateReference("b", "a")
            };

            // Act
            var closure = BuildIntegratedRestoreUtility.GetExternalClosure("a", references);

            // Assert
            Assert.Equal(2, closure.Count);
        }

        [Fact]
        public void BuildIntegratedRestoreUtility_GetExternalClosure_Overlapping()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                CreateReference("a", "b", "c"),
                CreateReference("b", "d", "c"),
                CreateReference("c", "d"),
                CreateReference("d", "e"),
                CreateReference("e"),
            };

            // Act
            var closure = BuildIntegratedRestoreUtility.GetExternalClosure("a", references);

            // Assert
            Assert.Equal(5, closure.Count);
        }

        [Fact]
        public void BuildIntegratedRestoreUtility_GetExternalClosure_OverlappingSubSet()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                CreateReference("a", "b", "c"),
                CreateReference("b", "d", "c"),
                CreateReference("c", "d"),
                CreateReference("d", "e"),
                CreateReference("e"),
            };

            // Act
            var closure = BuildIntegratedRestoreUtility.GetExternalClosure("b", references);

            // Assert
            Assert.Equal(4, closure.Count);
        }

        [Fact]
        public void BuildIntegratedRestoreUtility_GetExternalClosure_MissingReference()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                CreateReference("a", "b", "c"),
                CreateReference("b", "d", "c"),
                CreateReference("c", "d"),
                CreateReference("d", "e"),
            };

            // Act
            var closure = BuildIntegratedRestoreUtility.GetExternalClosure("b", references);

            // Assert
            Assert.Equal(3, closure.Count);
        }

        [Fact]
        public void BuildIntegratedRestoreUtility_GetDirectReferences_Basic()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                CreateReference("a", "b", "c"),
                CreateReference("b"),
                CreateReference("c", "d"),
                CreateReference("d")
            };

            // Act
            var actual = BuildIntegratedRestoreUtility
                .GetDirectReferences("a", references)
                .OrderBy(r => r.UniqueName)
                .ToList();

            // Assert
            Assert.Equal(2, actual.Count);
            Assert.Equal("b", actual[0].UniqueName);
            Assert.Equal("c", actual[1].UniqueName);
        }

        [Fact]
        public void BuildIntegratedRestoreUtility_GetDirectReferences_MissingReference()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                CreateReference("a", "b", "c"),
                CreateReference("b"),
                CreateReference("d")
            };

            // Act
            var actual = BuildIntegratedRestoreUtility
                .GetDirectReferences("a", references)
                .OrderBy(r => r.UniqueName)
                .ToList();

            // Assert
            Assert.Equal(1, actual.Count);
            Assert.Equal("b", actual[0].UniqueName);
        }

        [Fact]
        public void BuildIntegratedRestoreUtility_GetDirectReferences_MissingRoot()
        {
            // Arrange
            var references = new HashSet<ExternalProjectReference>()
            {
                CreateReference("a", "b", "c"),
                CreateReference("b"),
                CreateReference("d")
            };

            // Act
            var actual = BuildIntegratedRestoreUtility
                .GetDirectReferences("e", references)
                .OrderBy(r => r.UniqueName)
                .ToList();

            // Assert
            Assert.Equal(0, actual.Count);
        }

        private static ExternalProjectReference CreateReference(string name)
        {
            return new ExternalProjectReference(
                uniqueName: name,
                packageSpec: null,
                msbuildProjectPath: name,
                projectReferences: new List<string>());
        }

        private static ExternalProjectReference CreateReference(string name, params string[] children)
        {
            return new ExternalProjectReference(
                uniqueName: name,
                packageSpec: null,
                msbuildProjectPath: name,
                projectReferences: children.ToList());
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_RestoreProjectNameProjectJson()
        {
            // Arrange
            var projectName = "testproj";

            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "testproj.project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                CreateConfigJson(projectConfig.FullName);

                var sources = new List<SourceRepository>
            {
                Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
            };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework,
                    new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var effectiveGlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);

                // Act
                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    GetExternalProjectReferenceContext(),
                    sources,
                    effectiveGlobalPackagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                // Assert
                Assert.True(File.Exists(Path.Combine(projectFolder.FullName, "testproj.project.lock.json")));
                Assert.True(result.Success);
                Assert.False(File.Exists(Path.Combine(projectFolder.FullName, "project.lock.json")));
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_IsRestoreRequiredDependencyChanged()
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

                CreateConfigJson(projectConfig.FullName);

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
                    GetExternalProjectReferenceContext(),
                    sources,
                    packagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var projects = new List<BuildIntegratedNuGetProject>() { project };

                JsonConfigUtility.AddDependency(json, new NuGet.Packaging.Core.PackageDependency("nuget.core", VersionRange.Parse("2.8.3")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var context = GetExternalProjectReferenceContext();

                var packageFolders = new List<string>() { packagesFolder };

                // Act
                var b = BuildIntegratedRestoreUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.True(b);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_IsRestoreRequiredChangedSha512()
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

                CreateConfigJson(projectConfig.FullName);

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
                    GetExternalProjectReferenceContext(),
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

                var context = GetExternalProjectReferenceContext();

                // Act
                var b = BuildIntegratedRestoreUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.True(b);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_IsRestoreRequiredMissingPackage()
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

                CreateConfigJson(projectConfig.FullName);

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
                    GetExternalProjectReferenceContext(),
                    sources,
                    packagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var projects = new List<BuildIntegratedNuGetProject>() { project };

                var resolver = new VersionFolderPathResolver(packagesFolder);
                var packageFolders = new List<string>() { packagesFolder };

                var pathToDelete = resolver.GetInstallPath("nuget.versioning", NuGetVersion.Parse("1.0.7"));

                var context = GetExternalProjectReferenceContext();

                // Act
                TestFileSystemUtility.DeleteRandomTestFolder(pathToDelete);

                var b = BuildIntegratedRestoreUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.True(b);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_IsRestoreNotRequiredWithFloatingVersion()
        {
            // Arrange
            var projectName = "testproj";

            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                CreateConfigJson(projectConfig.FullName);

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
                    GetExternalProjectReferenceContext(),
                    sources,
                    effectiveGlobalPackagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var projects = new List<BuildIntegratedNuGetProject>() { project };

                var packageFolders = new List<string>() { effectiveGlobalPackagesFolder };

                var context = GetExternalProjectReferenceContext();

                // Act
                var b = BuildIntegratedRestoreUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.False(b);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_IsRestoreRequiredWithNoChanges()
        {
            // Arrange
            var projectName = "testproj";

            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                CreateConfigJson(projectConfig.FullName);

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
                    GetExternalProjectReferenceContext(),
                    sources,
                    effectiveGlobalPackagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var projects = new List<BuildIntegratedNuGetProject>() { project };

                var packageFolders = new List<string>() { effectiveGlobalPackagesFolder };

                var context = GetExternalProjectReferenceContext();

                // Act
                var b = BuildIntegratedRestoreUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.False(b);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_IsRestoreRequiredWithNoChangesFallbackFolder()
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

                CreateConfigJson(projectConfig.FullName);

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
                    GetExternalProjectReferenceContext(),
                    sources,
                    fallbackFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var projects = new List<BuildIntegratedNuGetProject>() { project };

                var packageFolders = new List<string>() { globalFolder, fallbackFolder };

                var context = GetExternalProjectReferenceContext();

                // Act
                var b = BuildIntegratedRestoreUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.False(b);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_IsRestoreRequiredWithNoChangesFallbackFolderIgnoresOtherHashes()
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

                CreateConfigJson(projectConfig.FullName);

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
                    GetExternalProjectReferenceContext(),
                    sources,
                    fallbackFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                // Restore to global folder
                result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    GetExternalProjectReferenceContext(),
                    sources,
                    globalFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var resolver = new VersionFolderPathResolver(fallbackFolder);
                var hashPath = resolver.GetHashPath("nuget.versioning", NuGetVersion.Parse("1.0.7"));
                File.WriteAllText(hashPath, "AA00F==");

                var projects = new List<BuildIntegratedNuGetProject>() { project };

                var packageFolders = new List<string>() { globalFolder, fallbackFolder };

                var context = GetExternalProjectReferenceContext();

                // Act
                var b = BuildIntegratedRestoreUtility.IsRestoreRequired(projects, packageFolders, context);

                // Assert
                Assert.False(b);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_CacheDiffersOnClosure()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath2 = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
                CreateConfigJson(randomConfig);
                CreateConfigJson(randomConfig2);

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
                CreateReference("a", "a/project.json", new string[] { "b/project.json" }),
                CreateReference("b", "b/project.json", new string[] { }),
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

                var cache = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(
                    projects,
                    GetExternalProjectReferenceContext());

                var projects2 = new List<BuildIntegratedNuGetProject>();
                project1.ProjectClosure = new List<ExternalProjectReference>() {
                CreateReference("a", "a/project.json", new string[] { "d/project.json" }),
                CreateReference("d", "d/project.json", new string[] { }),
            };

                projects2.Add(project1);
                projects2.Add(project2);
                var cache2 = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(
                    projects2,
                    GetExternalProjectReferenceContext());

                // Act
                var b = BuildIntegratedRestoreUtility.CacheHasChanges(cache, cache2);

                // Assert
                Assert.True(b);
            }
        }

        [Fact]
        public async Task CacheHasChanges_ReturnsTrue_IfSupportProfilesDiffer()
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
                CreateReference("a", "a/project.json", new string[] { "b/project.json" }),
                CreateReference("b", "b/project.json", new string[] { }),
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

                var cache = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(
                    projects,
                    GetExternalProjectReferenceContext());

                supports["net46"] = new JObject();
                File.WriteAllText(randomConfig, configJson.ToString());
                var cache2 = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(
                    projects,
                    GetExternalProjectReferenceContext());

                // Act 1
                var result1 = BuildIntegratedRestoreUtility.CacheHasChanges(cache, cache2);

                // Assert 1
                Assert.True(result1);

                // Act 2
                var cache3 = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(
                    projects,
                    GetExternalProjectReferenceContext());
                var result2 = BuildIntegratedRestoreUtility.CacheHasChanges(cache2, cache3);

                // Assert 2
                Assert.False(result2);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_CacheDiffersOnProjects()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath2 = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
                CreateConfigJson(randomConfig);
                CreateConfigJson(randomConfig2);

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
                CreateReference("a", "a/project.json", new string[] { "b/project.json" }),
                CreateReference("b", "b/project.json", new string[] { }),
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

                var cache = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(
                    projects,
                    GetExternalProjectReferenceContext());

                // Add a new project to the second cache
                projects.Add(project2);
                var cache2 = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(
                    projects,
                    GetExternalProjectReferenceContext());

                // Act
                var b = BuildIntegratedRestoreUtility.CacheHasChanges(cache, cache2);

                // Assert
                Assert.True(b);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_SameCache()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath2 = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
                CreateConfigJson(randomConfig);
                CreateConfigJson(randomConfig2);

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
                CreateReference("a", "a/project.json", new string[] { "b/project.json" }),
                CreateReference("b", "b/project.json", new string[] { }),
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

                var cache = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(
                    projects,
                    GetExternalProjectReferenceContext());
                var cache2 = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(
                    projects,
                    GetExternalProjectReferenceContext());

                // Act
                var b = BuildIntegratedRestoreUtility.CacheHasChanges(cache, cache2);

                // Assert
                Assert.False(b);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_CreateCache()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath2 = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
                CreateConfigJson(randomConfig);
                CreateConfigJson(randomConfig2);

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
                    CreateReference("a", "a/project.json", new string[] { "b/project.json" }),
                    CreateReference("b", "b/project.json", new string[] { }),
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
                var cache = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(
                    projects,
                    GetExternalProjectReferenceContext());

                // Assert
                Assert.Equal(2, cache.Count);
                Assert.Equal(2, cache[project1.MSBuildProjectPath].ReferenceClosure.Count);
                Assert.Equal(0, cache[project2.MSBuildProjectPath].ReferenceClosure.Count);
                Assert.Equal("a|b", string.Join("|", cache[project1.MSBuildProjectPath].ReferenceClosure));
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_BasicRestoreTest()
        {
            // Arrange
            var projectName = "testproj";

            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                CreateConfigJson(projectConfig.FullName);

                var sources = new List<SourceRepository>
            {
                Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
            };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework,
                    new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var effectiveGlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);

                // Act
                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    GetExternalProjectReferenceContext(),
                    sources,
                    effectiveGlobalPackagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                // Assert
                Assert.True(File.Exists(Path.Combine(projectFolder.FullName, "project.lock.json")));
                Assert.True(result.Success);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_RestoreToRelativePathGlobalPackagesFolder()
        {
            // Arrange
            var projectName = "testproj";

            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var configFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var solutionFolderParent = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                File.WriteAllText(projectConfig.FullName, ProjectJsonWithPackage);

                var sources = new List<SourceRepository>
                    {
                        Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                    };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework,
                    new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var configContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<config>
<add key=""globalPackagesFolder"" value=""..\NuGetPackages"" />
</config>
</configuration>";

                var configSubFolder = Path.Combine(configFolder, "sub");
                Directory.CreateDirectory(configSubFolder);

                File.WriteAllText(Path.Combine(configFolder, "sub", "nuget.config"), configContents);

                var settings = new Configuration.Settings(configSubFolder);

                var solutionFolder = new DirectoryInfo(Path.Combine(solutionFolderParent, "solutionFolder"));
                solutionFolder.Create();

                var effectiveGlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);

                var context = GetExternalProjectReferenceContext();
                var logger = (TestLogger)context.Logger;

                // Act
                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    context,
                    sources,
                    effectiveGlobalPackagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                // Assert
                Assert.True(File.Exists(Path.Combine(projectFolder.FullName, "project.lock.json")));

                var packagesFolder = Path.Combine(configFolder, "NuGetPackages");

                Assert.True(Directory.Exists(packagesFolder));
                Assert.True(File.Exists(Path.Combine(
                    packagesFolder,
                    "EntityFramework",
                    "5.0.0",
                    "EntityFramework.5.0.0.nupkg")));

                Assert.True(result.Success, logger.ShowErrors());
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_GetChildProjectsInClosure_MultipleChildHierarchy()
        {           
            // Arrange
            using (var randomProjectFolderPathProject1 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathA = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathB = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathC = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectConfig = new FileInfo(Path.Combine(randomProjectFolderPathProject1, "project.json"));
                var projectConfigA = new FileInfo(Path.Combine(randomProjectFolderPathA, "project.json"));
                var projectConfigB = new FileInfo(Path.Combine(randomProjectFolderPathB, "project.json"));
                var projectConfigC = new FileInfo(Path.Combine(randomProjectFolderPathC, "project.json"));
                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var testNuGetProjectContext = new TestNuGetProjectContext();

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathProject1,
                    "project1");

                var project1 = new TestBuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);
                project1.ProjectClosure = new List<ExternalProjectReference>()
                {
                    CreateReference(projectConfigA.FullName, "a/project.json", new string[] { }),
                    CreateReference(projectConfigB.FullName, "b/project.json", new string[] { }),
                    CreateReference(projectConfigC.FullName, "c/project.json", new string[] { }),
                };

                // Project A
                var msBuildNuGetProjectSystemA = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathA,
                    "a");

                var projectA = new TestBuildIntegratedNuGetProject(projectConfigA.FullName, msBuildNuGetProjectSystemA);
                projectA.ProjectClosure = new List<ExternalProjectReference>()
                {
                    CreateReference(projectConfigB.FullName, "b/project.json", new string[] { }),
                    CreateReference(projectConfigC.FullName, "c/project.json", new string[] { }),
                };

                // Project B
                var msBuildNuGetProjectSystemB = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathB,
                    "b");

                var projectB = new TestBuildIntegratedNuGetProject(projectConfigB.FullName, msBuildNuGetProjectSystemB);
                projectB.ProjectClosure = new List<ExternalProjectReference>()
                {
                    CreateReference(projectConfigC.FullName, "c/project.json", new string[] { }),
                };

                var msBuildNuGetProjectSystemC = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathC,
                    "c");

                var projectC = new TestBuildIntegratedNuGetProject(projectConfigC.FullName, msBuildNuGetProjectSystemC);
                projectC.ProjectClosure = new List<ExternalProjectReference>() { };

                var projects = new List<BuildIntegratedNuGetProject>();
                projects.Add(project1);
                projects.Add(projectA);
                projects.Add(projectB);
                projects.Add(projectC);

                var orderedChilds = new List<BuildIntegratedNuGetProject>();
                var uniqueProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                uniqueProjects.Add(project1.ProjectName);

                var cache = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects,
                        GetExternalProjectReferenceContext());

                // Act
                BuildIntegratedRestoreUtility.GetChildProjectsInClosure(project1, projects, orderedChilds,
                        uniqueProjects, cache);

                // Assert
                Assert.Equal(4, orderedChilds.Count);
                Assert.Equal(projectC.ProjectName, orderedChilds[0].ProjectName, StringComparer.OrdinalIgnoreCase);
                Assert.Equal(projectB.ProjectName, orderedChilds[1].ProjectName, StringComparer.OrdinalIgnoreCase);
                Assert.Equal(projectA.ProjectName, orderedChilds[2].ProjectName, StringComparer.OrdinalIgnoreCase);
                Assert.Equal(project1.ProjectName, orderedChilds[3].ProjectName, StringComparer.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_GetChildProjectsInClosure_SingleChild()
        {
            // Arrange
            using (var randomProjectFolderPathProject1 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathA = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathB = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathC = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectConfig = new FileInfo(Path.Combine(randomProjectFolderPathProject1, "project.json"));
                var projectConfigA = new FileInfo(Path.Combine(randomProjectFolderPathA, "project.json"));
                var projectConfigB = new FileInfo(Path.Combine(randomProjectFolderPathB, "project.json"));
                var projectConfigC = new FileInfo(Path.Combine(randomProjectFolderPathC, "project.json"));
                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var testNuGetProjectContext = new TestNuGetProjectContext();

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathProject1,
                    "project1");

                var project1 = new TestBuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);
                project1.ProjectClosure = new List<ExternalProjectReference>()
                {
                    CreateReference(projectConfigA.FullName, "a/project.json", new string[] { }),
                };

                // Project A
                var msBuildNuGetProjectSystemA = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathA,
                    "a");

                var projectA = new TestBuildIntegratedNuGetProject(projectConfigA.FullName, msBuildNuGetProjectSystemA);
                projectA.ProjectClosure = new List<ExternalProjectReference>() {};

                // Project B
                var msBuildNuGetProjectSystemB = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathB,
                    "b");

                var projectB = new TestBuildIntegratedNuGetProject(projectConfigB.FullName, msBuildNuGetProjectSystemB);
                projectB.ProjectClosure = new List<ExternalProjectReference>()
                {
                    CreateReference(projectConfigC.FullName, "c/project.json", new string[] { }),
                };

                var msBuildNuGetProjectSystemC = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathC,
                    "c");

                var projectC = new TestBuildIntegratedNuGetProject(projectConfigC.FullName, msBuildNuGetProjectSystemC);
                projectC.ProjectClosure = new List<ExternalProjectReference>() { };

                var projects = new List<BuildIntegratedNuGetProject>();
                projects.Add(project1);
                projects.Add(projectA);
                projects.Add(projectB);
                projects.Add(projectC);

                var orderedChilds = new List<BuildIntegratedNuGetProject>();
                var uniqueProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                uniqueProjects.Add(project1.ProjectName);

                var cache = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects,
                        GetExternalProjectReferenceContext());

                // Act
                BuildIntegratedRestoreUtility.GetChildProjectsInClosure(project1, projects, orderedChilds,
                        uniqueProjects, cache);

                // Assert
                Assert.Equal(2, orderedChilds.Count);
                Assert.Equal(projectA.ProjectName, orderedChilds[0].ProjectName, StringComparer.OrdinalIgnoreCase);
                Assert.Equal(project1.ProjectName, orderedChilds[1].ProjectName, StringComparer.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_GetChildProjectsInClosure_NoChild()
        {
            // Arrange
            using (var randomProjectFolderPathProject1 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathA = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathB = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectConfig = new FileInfo(Path.Combine(randomProjectFolderPathProject1, "project.json"));
                var projectConfigA = new FileInfo(Path.Combine(randomProjectFolderPathA, "project.json"));
                var projectConfigB = new FileInfo(Path.Combine(randomProjectFolderPathB, "project.json"));

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var testNuGetProjectContext = new TestNuGetProjectContext();

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathProject1,
                    "project1");

                var project1 = new TestBuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);
                project1.ProjectClosure = new List<ExternalProjectReference>() {};

                // Project A
                var msBuildNuGetProjectSystemA = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathA,
                    "a");

                var projectA = new TestBuildIntegratedNuGetProject(projectConfigA.FullName, msBuildNuGetProjectSystemA);
                projectA.ProjectClosure = new List<ExternalProjectReference>() { };

                // Project B
                var msBuildNuGetProjectSystemB = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathB,
                    "b");

                var projectB = new TestBuildIntegratedNuGetProject(projectConfigB.FullName, msBuildNuGetProjectSystemB);
                projectB.ProjectClosure = new List<ExternalProjectReference>() {};

                var projects = new List<BuildIntegratedNuGetProject>();
                projects.Add(project1);
                projects.Add(projectA);
                projects.Add(projectB);

                var orderedChilds = new List<BuildIntegratedNuGetProject>();
                var uniqueProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                uniqueProjects.Add(project1.ProjectName);

                var cache = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects,
                        GetExternalProjectReferenceContext());

                // Act
                BuildIntegratedRestoreUtility.GetChildProjectsInClosure(project1, projects, orderedChilds,
                        uniqueProjects, cache);

                // Assert
                Assert.Equal(1, orderedChilds.Count);
                Assert.Equal(project1.ProjectName, orderedChilds[0].ProjectName, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void CreateConfigJson(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                writer.Write(BasicConfig.ToString());
            }
        }

        private static JObject BasicConfig
        {
            get
            {
                var json = new JObject();

                var frameworks = new JObject();
                frameworks["uap10.0"] = new JObject();

                json["frameworks"] = frameworks;

                json.Add("runtimes", JObject.Parse("{ \"uap10-x86\": { }, \"uap10-x86-aot\": { } }"));

                return json;
            }
        }

        private const string ProjectJsonWithPackage = @"{
  'dependencies': {
    'EntityFramework': '5.0.0'
  },
  'frameworks': {
                'net46': { }
            }
}";

        private static ExternalProjectReference CreateReference(
            string name,
            string path,
            IEnumerable<string> references)
        {
            var spec = new PackageSpec(new JObject());
            spec.FilePath = name;

            return new ExternalProjectReference(
                name,
                spec,
                msbuildProjectPath: null,
                projectReferences: references);
        }

        private static ExternalProjectReferenceContext GetExternalProjectReferenceContext()
        {
            var logger = new TestLogger();

            return new ExternalProjectReferenceContext(logger);
        }

        private class TestBuildIntegratedNuGetProject : BuildIntegratedNuGetProject
        {
            public IReadOnlyList<ExternalProjectReference> ProjectClosure { get; set; }

            public TestBuildIntegratedNuGetProject(
                string jsonConfig,
                IMSBuildNuGetProjectSystem msbuildProjectSystem) : base(
                    jsonConfig,
                    Path.Combine(
                        msbuildProjectSystem.ProjectFullPath,
                        $"{msbuildProjectSystem.ProjectName}.csproj"),
                    msbuildProjectSystem)
            {
                InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, msbuildProjectSystem.ProjectName);
            }

            public override Task<IReadOnlyList<ExternalProjectReference>> GetProjectReferenceClosureAsync(
                ExternalProjectReferenceContext context)
            {
                return Task.FromResult(ProjectClosure);
            }
        }
    }
}
