﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Stores a set of <see cref="AggregateValidator"/>s and runs them.
    /// </summary>
    public class PackageValidator
    {
        public IEnumerable<IAggregateValidator> AggregateValidators { get; }

        private readonly ILogger<PackageValidator> _logger;

        private readonly StorageFactory _auditingStorageFactory;

        public PackageValidator(
            IEnumerable<IAggregateValidator> aggregateValidators,
            StorageFactory auditingStorageFactory,
            ILogger<PackageValidator> logger)
        {
            var validators = aggregateValidators?.ToList();

            if (aggregateValidators == null || !validators.Any())
            {
                throw new ArgumentException("Must supply at least one endpoint!", nameof(aggregateValidators));
            }

            AggregateValidators = validators;
            _auditingStorageFactory = auditingStorageFactory ?? throw new ArgumentNullException(nameof(auditingStorageFactory));
            _logger = logger;
        }

        /// <summary>
        /// Runs <see cref="IValidator"/>s from the <see cref="IAggregateValidator"/>s against a package.
        /// </summary>
        /// <returns>A <see cref="PackageValidationResult"/> generated from the results of the <see cref="IValidator"/>s.</returns>
        public async Task<PackageValidationResult> ValidateAsync(PackageValidatorContext context, CollectorHttpClient client, CancellationToken cancellationToken)
        {
            var package = new PackageIdentity(context.Package.Id, NuGetVersion.Parse(context.Package.Version));

            var deletionAuditEntries = await DeletionAuditEntry.GetAsync(
                _auditingStorageFactory,
                cancellationToken,
                package,
                logger: _logger);

            var validationContext = new ValidationContext(package, context.CatalogEntries, deletionAuditEntries, client, cancellationToken);
            var results = await Task.WhenAll(AggregateValidators.Select(endpoint => endpoint.ValidateAsync(validationContext)).ToList());
            return new PackageValidationResult(validationContext, results);
        }
    }
}