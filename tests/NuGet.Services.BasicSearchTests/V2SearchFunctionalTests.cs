﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.BasicSearchTests.Models;
using NuGet.Services.BasicSearchTests.TestSupport;
using Xunit;

namespace NuGet.Services.BasicSearchTests
{
    public class V2SearchFunctionalTests
    {
        [Fact]
        public async Task CanReturnEmptyResult()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("Newtonsoft.Json", "7.0.1")
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                // Act
                var response = await app.Client.GetAsync(new V2SearchBuilder { Query = "something else" }.RequestUri);
                var s = await response.Content.ReadAsStringAsync();
                var result = await response.Content.ReadAsAsync<V2SearchResult>(Serialization.MediaTypeFormatters);

                // Assert
                Assert.Equal(0, result.TotalHits);
                Assert.Empty(result.Data);
            }
        }

        [Fact]
        public async Task TreatsFirstIdTermAsExactMatch()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("WebGrease", "1.6.0")
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                // Act
                var partialIdResponse = await app.Client.GetAsync(new V2SearchBuilder
                {
                    Query = "version:1.6.0 id:Web",
                    Prerelease = true,
                }.RequestUri);
                var partialId = await partialIdResponse.Content.ReadAsAsync<V2SearchResult>();

                var wrongExactIdResponse = await app.Client.GetAsync(new V2SearchBuilder
                {
                    Query = "id:Web version:1.6.0",
                    Prerelease = true,
                }.RequestUri);
                var wrongExactId = await wrongExactIdResponse.Content.ReadAsAsync<V2SearchResult>();

                var correctExactIdResponse = await app.Client.GetAsync(new V2SearchBuilder
                {
                    Query = "id:WebGrease version:1.6.0",
                    Prerelease = true,
                }.RequestUri);
                var correctExactId = await correctExactIdResponse.Content.ReadAsAsync<V2SearchResult>();

                // Assert
                Assert.Equal("1.6.0", partialId.GetPackageVersion("WebGrease"));
                Assert.False(wrongExactId.ContainsPackage("WebGrease"));
                Assert.Equal("1.6.0", correctExactId.GetPackageVersion("WebGrease"));
            }
        }

        [Fact]
        public async Task SupportsPrereleaseParameter()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("Antlr", "3.1.3.42154"),
                new PackageVersion("Antlr", "3.4.1.9004-pre"),
                new PackageVersion("angularjs", "1.2.0-RC1"),
                new PackageVersion("WebGrease", "1.6.0")
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                string query = "angularjs Antlr WebGrease";

                // Act
                var withPrereleaseResponse = await app.Client.GetAsync(new V2SearchBuilder { Query = query, Prerelease = true }.RequestUri);
                var withPrerelease = await withPrereleaseResponse.Content.ReadAsAsync<V2SearchResult>();

                var withoutPrereleaseResponse = await app.Client.GetAsync(new V2SearchBuilder { Query = query, Prerelease = false }.RequestUri);
                var withoutPrerelease = await withoutPrereleaseResponse.Content.ReadAsAsync<V2SearchResult>();

                // Assert
                Assert.Equal("1.2.0-RC1", withPrerelease.GetPackageVersion("angularjs"));  // the only version available is prerelease
                Assert.Equal("3.4.1.9004-pre", withPrerelease.GetPackageVersion("Antlr")); // the latest version is prerelease
                Assert.Equal("1.6.0", withPrerelease.GetPackageVersion("WebGrease"));      // the only version available is non-prerelease

                Assert.False(withoutPrerelease.ContainsPackage("angularjs"));              // the only version available is prerelease and is therefore excluded
                Assert.Equal("3.1.3.42154", withoutPrerelease.GetPackageVersion("Antlr")); // this is the latest non-release version
                Assert.Equal("1.6.0", withoutPrerelease.GetPackageVersion("WebGrease"));   // the only version available is non-prerelease
            }
        }

        [Fact]
        public async Task SupportsCaseInsensitiveVersionSearch()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("angularjs", "1.2.0-RC1"),
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                // Act
                var mismatchedCaseResponse = await app.Client.GetAsync(new V2SearchBuilder
                {
                    Query = "packageid:angularjs version:1.2.0-rc1",
                    Prerelease = true,
                }.RequestUri);
                var mismatchedCase = await mismatchedCaseResponse.Content.ReadAsAsync<V2SearchResult>();

                var matchingCaseResponse = await app.Client.GetAsync(new V2SearchBuilder
                {
                    Query = "packageid:angularjs version:1.2.0-RC1",
                    Prerelease = true,
                }.RequestUri);
                var matchingCase = await matchingCaseResponse.Content.ReadAsAsync<V2SearchResult>();

                // Assert
                Assert.Equal("1.2.0-RC1", mismatchedCase.GetPackageVersion("angularjs"));
                Assert.Equal("1.2.0-RC1", matchingCase.GetPackageVersion("angularjs"));
            }
        }

        [Fact]
        public async Task ReturnsIdWithOriginalCase()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("Newtonsoft.Json", "7.0.1"),
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                // Act
                var response = await app.Client.GetAsync(new V2SearchBuilder
                {
                    Query = "packageid:Newtonsoft.Json",
                    Prerelease = true,
                }.RequestUri);
                var result = await response.Content.ReadAsAsync<V2SearchResult>();

                // Assert
                Assert.Equal("Newtonsoft.Json", result.Data[0].PackageRegistration.Id);
                Assert.Equal("7.0.1", result.Data[0].NormalizedVersion);
            }
        }

        [Fact]
        public async Task ReturnsVersionsWithOriginalCase()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("angularjs", "1.2.0-RC1"),
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                // Act
                var response = await app.Client.GetAsync(new V2SearchBuilder
                {
                    Query = "packageid:angularjs",
                    Prerelease = true,
                }.RequestUri);
                var result = await response.Content.ReadAsAsync<V2SearchResult>();

                // Assert
                Assert.Equal("angularjs", result.Data[0].PackageRegistration.Id);
                Assert.Equal("1.2.0-RC1", result.Data[0].NormalizedVersion);
            }
        }

        [Fact]
        public async Task SortsResultsByDownload()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("Newtonsoft.Json", "7.0.1", 10),
                new PackageVersion("EntityFramework", "6.1.3", 20),
                new PackageVersion("bootstrap", "3.3.6", 5)
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                // Act
                var response = await app.Client.GetAsync(new V2SearchBuilder().RequestUri);
                var result = await response.Content.ReadAsAsync<V2SearchResult>(Serialization.MediaTypeFormatters);

                // Assert
                Assert.Equal("EntityFramework", result.Data[0].PackageRegistration.Id);
                Assert.Equal("Newtonsoft.Json", result.Data[1].PackageRegistration.Id);
                Assert.Equal("bootstrap", result.Data[2].PackageRegistration.Id);
            }
        }

        [Theory]
        [InlineData("Izmir", "Izmir", 1)]
        [InlineData("izmir", "Izmir", 1)]
        [InlineData("İzmir", "İzmir", 1)]
        [InlineData("ızmir", null, 0)]
        public async Task UsesLowerInvariantForCaseInsensitivity(string query, string expectedId, int expectedCount)
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("Izmir", "1.0.0"),
                new PackageVersion("İzmir", "1.0.0"),
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                // Act
                var response = await app.Client.GetAsync(new V2SearchBuilder
                {
                    Query = query
                }.RequestUri);
                var result = await response.Content.ReadAsAsync<V2SearchResult>(Serialization.MediaTypeFormatters);

                // Assert
                Assert.Equal(expectedCount, result.Data.Count);
                Assert.Equal(expectedCount, result.TotalHits);
                if (expectedCount > 0)
                {
                    Assert.Equal(expectedId, result.Data[0].PackageRegistration.Id);
                }
            }
        }

        [Fact]
        public async Task LatestAndPrereleaseFiltersAreIgnoredTheWayTheOldV2ServiceDidThings()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("bootstrap", "3.3.5"),
                new PackageVersion("bootstrap", "4.0.0-alpha",  listed: false),
                new PackageVersion("bootstrap", "4.0.0-alpha2", listed: true),
                new PackageVersion("bootstrap", "3.3.6"),
                new PackageVersion("semverA", "1.0.0")
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                string query = "";

                // Act
                var withPrereleaseResponse = await app.Client.GetAsync(new V2SearchBuilder { Query = query, Prerelease = true, IgnoreFilter = true }.RequestUri);
                var withPrerelease = await withPrereleaseResponse.Content.ReadAsAsync<V2SearchResult>();

                var withoutPrereleaseResponse = await app.Client.GetAsync(new V2SearchBuilder { Query = query, Prerelease = false, IgnoreFilter = true }.RequestUri);
                var withoutPrerelease = await withoutPrereleaseResponse.Content.ReadAsAsync<V2SearchResult>();

                var withPrereleaseResponseSemVer2 = await app.Client.GetAsync(new V2SearchBuilder { Query = query, Prerelease = true, IgnoreFilter = true, SemVerLevel = "2.0.0" }.RequestUri);
                var withPrereleaseSemVer2 = await withPrereleaseResponseSemVer2.Content.ReadAsAsync<V2SearchResult>();

                var withoutPrereleaseResponseSemVer2 = await app.Client.GetAsync(new V2SearchBuilder { Query = query, Prerelease = false, IgnoreFilter = true, SemVerLevel = "2.0.0" }.RequestUri);
                var withoutPrereleaseSemVer2 = await withoutPrereleaseResponseSemVer2.Content.ReadAsAsync<V2SearchResult>();

                // Assert
                Assert.True(withPrerelease.ContainsPackage("bootstrap"));                       // bootstrap is in the results
                Assert.True(withPrerelease.ContainsPackage("bootstrap", "3.3.5"));              // stable version is in the results
                Assert.True(withPrerelease.ContainsPackage("bootstrap", "3.3.6"));              // stable version is in the results
                Assert.True(withPrerelease.ContainsPackage("bootstrap", "4.0.0-alpha"));        // prerelease version is in the results
                Assert.True(withPrerelease.ContainsPackage("bootstrap", "4.0.0-alpha2"));       // prerelease version is in the results
                Assert.False(withPrerelease.ContainsPackage("semverA", "1.0.0"));               // SemVerLevel 2 packages are filtered
                var prerelease1 = withPrerelease.GetPackage("bootstrap", "4.0.0-alpha");
                Assert.False(prerelease1.Listed);                                               // unlisted version is in the results

                Assert.True(withoutPrerelease.ContainsPackage("bootstrap"));                    // bootstrap is in the results
                Assert.True(withoutPrerelease.ContainsPackage("bootstrap", "3.3.5"));           // stable version is in the results
                Assert.True(withoutPrerelease.ContainsPackage("bootstrap", "3.3.6"));           // stable version is in the results
                Assert.True(withoutPrerelease.ContainsPackage("bootstrap", "4.0.0-alpha"));     // prerelease version is in the results
                Assert.True(withoutPrerelease.ContainsPackage("bootstrap", "4.0.0-alpha2"));    // prerelease version is in the results
                Assert.False(withPrerelease.ContainsPackage("semverA", "1.0.0"));               // SemVerLevel 2 packages are filtered
                var prerelease2 = withoutPrerelease.GetPackage("bootstrap", "4.0.0-alpha");
                Assert.False(prerelease2.Listed);                                               // unlisted version is in the results

                Assert.True(withPrereleaseSemVer2.ContainsPackage("bootstrap"));                       // bootstrap is in the results
                Assert.True(withPrereleaseSemVer2.ContainsPackage("bootstrap", "3.3.5"));              // stable version is in the results
                Assert.True(withPrereleaseSemVer2.ContainsPackage("bootstrap", "3.3.6"));              // stable version is in the results
                Assert.True(withPrereleaseSemVer2.ContainsPackage("bootstrap", "4.0.0-alpha"));        // prerelease version is in the results
                Assert.True(withPrereleaseSemVer2.ContainsPackage("bootstrap", "4.0.0-alpha2"));       // prerelease version is in the results
                Assert.True(withPrereleaseSemVer2.ContainsPackage("semverA", "1.0.0"));                // SemVerLevel 2 packages are included
                var prerelease1SemVer2 = withPrereleaseSemVer2.GetPackage("bootstrap", "4.0.0-alpha");
                Assert.False(prerelease1SemVer2.Listed);                                               // unlisted version is in the results

                Assert.True(withoutPrereleaseSemVer2.ContainsPackage("bootstrap"));                    // bootstrap is in the results
                Assert.True(withoutPrereleaseSemVer2.ContainsPackage("bootstrap", "3.3.5"));           // stable version is in the results
                Assert.True(withoutPrereleaseSemVer2.ContainsPackage("bootstrap", "3.3.6"));           // stable version is in the results
                Assert.True(withoutPrereleaseSemVer2.ContainsPackage("bootstrap", "4.0.0-alpha"));     // prerelease version is in the results
                Assert.True(withoutPrereleaseSemVer2.ContainsPackage("bootstrap", "4.0.0-alpha2"));    // prerelease version is in the results
                Assert.True(withoutPrereleaseSemVer2.ContainsPackage("semverA", "1.0.0"));             // SemVerLevel 2 packages are included
                var prerelease2SemVer2 = withoutPrereleaseSemVer2.GetPackage("bootstrap", "4.0.0-alpha");
                Assert.False(prerelease2SemVer2.Listed);                                               // unlisted version is in the results
            }
        }

        [Fact]
        public async Task ReturnsCorrectSchema()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("EntityFramework", "6.1.3-beta1", 30), // EntityFramework has all of the fields populated.
                new PackageVersion("EntityFramework", "6.1.2", 20),
                new PackageVersion("angularjs", "1.4.8", 10, verified: true)              // AngularJS a) has multiple authors and b) has no summary.
            };
            var expectedPath = Path.Combine("Data", "V2SearchFunctionalTests.ReturnsCorrectSchema.json");
            var expectedJsonText = File.ReadAllText(expectedPath);
            var expected = JsonConvert.DeserializeObject<JObject>(expectedJsonText, new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.DateTimeOffset
            });
            var before = DateTimeOffset.UtcNow;

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                // Act
                var response = await app.Client.GetAsync(new V2SearchBuilder { Prerelease = true }.RequestUri);
                var actual = await response.Content.ReadAsAsync<JObject>();

                // Assert
                // The actual result's index timestamp is formatted using the current culture. Verify the timestamp is as expected,
                // and then set the result to the expected dummy value.
                Assert.Equal(DateTime.MinValue.ToUniversalTime().ToString("G"), actual["indexTimestamp"]);

                actual["indexTimestamp"] = expected["indexTimestamp"];

                /// The results' "Published" and "LastUpdated" properties are set to the time that the package's
                /// metadata was extracted by the test infrastructure's <see cref="NuGet.Indexing.NupkgPackageMetadataExtraction"/>.
                /// We'll do a sanity check for these properties and set them to the expected response's dummy values.
                foreach (var actualData in actual["data"])
                {
                    var published = actualData["Published"].Value<DateTime>();
                    var lastUpdated = actualData["LastUpdated"].Value<DateTime>();

                    Assert.True(published >= before);
                    Assert.True(lastUpdated >= before);

                    actualData["Published"] = expected["data"].First["Published"];
                    actualData["LastUpdated"] = expected["data"].First["LastUpdated"];
                }

                // Validate the rest of the payload.
                Assert.Equal(expected, actual);
            }
        }
    }
}