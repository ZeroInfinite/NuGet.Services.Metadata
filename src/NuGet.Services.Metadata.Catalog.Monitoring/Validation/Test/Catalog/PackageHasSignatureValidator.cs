﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Exceptions;

namespace NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Catalog
{
    /// <summary>
    /// Validates that the package is signed by verifying the presence of the "package signature file"
    /// in the nupkg. See: https://github.com/NuGet/Home/wiki/Package-Signatures-Technical-Details#-the-package-signature-file
    /// </summary>
    public sealed class PackageHasSignatureValidator : Validator
    {
        private const string NupkgSignatureFile = ".signature.p7s";

        private readonly ILogger<PackageHasSignatureValidator> _logger;

        public PackageHasSignatureValidator(
            IDictionary<FeedType, SourceRepository> feedToSource,
            ILogger<PackageHasSignatureValidator> logger)
          : base(feedToSource, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task<bool> ShouldRunAsync(ValidationContext context)
        {
            if (!ShouldRunValidator(context))
            {
                return false;
            }

            return await base.ShouldRunAsync(context);
        }

        protected override async Task RunInternalAsync(ValidationContext context)
        {
            await RunValidatorAsync(context);
        }

        public bool ShouldRunValidator(ValidationContext context)
        {
            var latest = context.Entries
                .OrderByDescending(e => e.CommitTimeStamp)
                .FirstOrDefault();

            if (latest == null)
            {
                _logger.LogInformation(
                    "Skipping package {PackageId} {PackageVersion} as it had no catalog entries",
                    context.Package.Id,
                    context.Package.Version);

                return false;
            }

            // We don't need to validate the package if the latest entry indicates deletion.
            if (latest.IsDelete())
            {
                _logger.LogInformation(
                    "Skipping package {PackageId} {PackageVersion} as its latest catalog entry is a delete",
                    context.Package.Id,
                    context.Package.Version);

                return false;
            }

            return true;
        }

        public async Task RunValidatorAsync(ValidationContext context)
        {
            var latest = context.Entries
                .OrderByDescending(e => e.CommitTimeStamp)
                .FirstOrDefault();

            _logger.LogInformation(
                "Validating that catalog entry {CatalogEntry} for package {PackageId} {PackageVersion} has a package signature file...",
                latest.Uri,
                context.Package.Id,
                context.Package.Version);

            var leaf = await context.Client.GetJObjectAsync(latest.Uri, context.CancellationToken);

            if (!LeafHasSignatureFile(leaf))
            {
                _logger.LogWarning(
                    "Catalog entry {CatalogEntry} for package {PackageId} {PackageVersion} is missing a package signature file",
                    latest.Uri,
                    context.Package.Id,
                    context.Package.Version);

                throw new MissingPackageSignatureFileException(
                    latest.Uri,
                    $"Catalog entry {latest.Uri} for package {context.Package.Id} {context.Package.Version} is missing a package signature file");
            }

            _logger.LogInformation(
                "Validated that catalog entry {CatalogEntry} for package {PackageId} {PackageVersion} has a package signature",
                latest.Uri,
                context.Package.Id,
                context.Package.Version);
        }

        private bool LeafHasSignatureFile(JObject leaf)
        {
            var packageEntries = leaf["packageEntries"];

            if (packageEntries == null)
            {
                throw new InvalidOperationException("Catalog leaf is missing the 'packageEntries' property");
            }

            if (!(packageEntries is JArray files))
            {
                throw new InvalidOperationException("Catalog leaf's 'packageEntries' property is malformed");
            }

            return files.Any(file => (string)file["fullName"] == NupkgSignatureFile);
        }
    }
}
