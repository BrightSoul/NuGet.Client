// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.PackageManagement.Test;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
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
