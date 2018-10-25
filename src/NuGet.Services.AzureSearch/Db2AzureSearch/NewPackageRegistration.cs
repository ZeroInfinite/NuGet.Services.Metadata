// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class NewPackageRegistration
    {
        public NewPackageRegistration(
            string packageId,
            long totalDownloadCount,
            string[] owners,
            IReadOnlyList<Package> packages)
        {
            PackageId = packageId ?? throw new ArgumentNullException(packageId);
            TotalDownloadCount = totalDownloadCount;
            Owners = owners ?? throw new ArgumentNullException(nameof(owners));
            Packages = packages ?? throw new ArgumentNullException(nameof(packages));
        }

        public string PackageId { get; }
        public long TotalDownloadCount { get; }
        public string[] Owners { get; }
        public IReadOnlyList<Package> Packages { get; }
    }
}
