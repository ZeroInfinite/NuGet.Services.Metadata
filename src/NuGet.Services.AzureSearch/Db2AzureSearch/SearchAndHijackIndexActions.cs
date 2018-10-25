// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class SearchAndHijackIndexActions
    {
        public SearchAndHijackIndexActions(
            IReadOnlyList<IndexAction<IKeyedDocument>> search,
            IReadOnlyList<IndexAction<IKeyedDocument>> hijack)
        {
            Search = search ?? throw new ArgumentNullException(nameof(hijack));
            Hijack = hijack ?? throw new ArgumentNullException(nameof(hijack));
        }

        public IReadOnlyList<IndexAction<IKeyedDocument>> Search { get; }
        public IReadOnlyList<IndexAction<IKeyedDocument>> Hijack { get; }
    }
}
