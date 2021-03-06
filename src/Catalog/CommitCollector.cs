﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class CommitCollector : CollectorBase
    {
        public CommitCollector(
            Uri index,
            ITelemetryService telemetryService,
            Func<HttpMessageHandler> handlerFunc = null,
            TimeSpan? httpClientTimeout = null)
            : base(index, telemetryService, handlerFunc, httpClientTimeout)
        {
        }

        protected override async Task<bool> FetchAsync(
            CollectorHttpClient client,
            ReadWriteCursor front,
            ReadCursor back,
            CancellationToken cancellationToken)
        {
            IEnumerable<CatalogItem> catalogItems = await FetchCatalogItemsAsync(client, front, cancellationToken);

            bool acceptNextBatch = false;

            foreach (CatalogItem catalogItem in catalogItems)
            {
                JObject page = await client.GetJObjectAsync(catalogItem.Uri, cancellationToken);

                JToken context = null;
                page.TryGetValue("@context", out context);

                var batches = await CreateBatchesAsync(page["items"]
                    .Select(item => new CatalogItem((JObject)item))
                    .Where(item => item.CommitTimeStamp > front.Value && item.CommitTimeStamp <= back.Value));

                var orderedBatches = batches
                    .OrderBy(batch => batch.CommitTimeStamp)
                    .ToList();

                var lastBatch = orderedBatches.LastOrDefault();
                DateTime? previousCommitTimeStamp = null;

                foreach (var batch in orderedBatches)
                {
                    // If the commit timestamp has changed from the previous batch, commit. This is important because if
                    // two batches have the same commit timestamp but processing the second fails, we should not
                    // progress the cursor forward.
                    if (previousCommitTimeStamp.HasValue && previousCommitTimeStamp != batch.CommitTimeStamp)
                    {
                        front.Value = previousCommitTimeStamp.Value;
                        await front.SaveAsync(cancellationToken);
                        Trace.TraceInformation("CommitCatalog.Fetch front.Value saved since timestamp changed from previous: {0}", front);
                    }

                    using (_telemetryService.TrackDuration(TelemetryConstants.ProcessBatchSeconds, new Dictionary<string, string>()
                    {
                        { TelemetryConstants.BatchItemCount, batch.Items.Count.ToString() }
                    }))
                    {
                        acceptNextBatch = await OnProcessBatchAsync(
                            client,
                            batch.Items.Select(item => item.Value),
                            context,
                            batch.CommitTimeStamp,
                            batch.CommitTimeStamp == lastBatch.CommitTimeStamp,
                            cancellationToken);
                    }

                    // If this is the last batch, commit the cursor.
                    if (ReferenceEquals(batch, lastBatch))
                    {
                        front.Value = batch.CommitTimeStamp;
                        await front.SaveAsync(cancellationToken);
                        Trace.TraceInformation("CommitCatalog.Fetch front.Value saved due to last batch: {0}", front);
                    }

                    previousCommitTimeStamp = batch.CommitTimeStamp;

                    Trace.TraceInformation("CommitCatalog.Fetch front.Value is: {0}", front);

                    if (!acceptNextBatch)
                    {
                        break;
                    }
                }

                if (!acceptNextBatch)
                {
                    break;
                }
            }

            return acceptNextBatch;
        }

        protected async Task<IEnumerable<CatalogItem>> FetchCatalogItemsAsync(
            CollectorHttpClient client,
            ReadWriteCursor front,
            CancellationToken cancellationToken)
        {
            JObject root;

            using (_telemetryService.TrackDuration(
                TelemetryConstants.CatalogIndexReadDurationSeconds,
                new Dictionary<string, string>() { { TelemetryConstants.Uri, Index.AbsoluteUri } }))
            {
                root = await client.GetJObjectAsync(Index, cancellationToken);
            }

            IEnumerable<CatalogItem> rootItems = root["items"]
                .Select(item => new CatalogItem((JObject)item))
                .Where(item => item.CommitTimeStamp > front.Value)
                .OrderBy(item => item.CommitTimeStamp);

            return rootItems;
        }

        protected virtual Task<IEnumerable<CatalogItemBatch>> CreateBatchesAsync(IEnumerable<CatalogItem> catalogItems)
        {
            var batches = catalogItems
                .GroupBy(item => item.CommitTimeStamp)
                .OrderBy(group => group.Key)
                .Select(group => new CatalogItemBatch(group.Key, group));

            return Task.FromResult(batches);
        }

        protected abstract Task<bool> OnProcessBatchAsync(
            CollectorHttpClient client,
            IEnumerable<JToken> items,
            JToken context,
            DateTime commitTimeStamp,
            bool isLastBatch,
            CancellationToken cancellationToken);

        protected class CatalogItemBatch : IComparable
        {
            public CatalogItemBatch(DateTime commitTimeStamp, IEnumerable<CatalogItem> items)
            {
                CommitTimeStamp = commitTimeStamp;
                Items = items.ToList();
                Items.Sort();
            }

            public DateTime CommitTimeStamp { get; }
            public List<CatalogItem> Items { get; }

            public int CompareTo(object obj)
            {
                return CommitTimeStamp.CompareTo(((CatalogItem)obj).CommitTimeStamp);
            }
        }

        protected class CatalogItem : IComparable
        {
            public CatalogItem(JObject value)
            {
                CommitTimeStamp = value["commitTimeStamp"].ToObject<DateTime>();
                Uri = value["@id"].ToObject<Uri>();
                Value = value;
            }

            public DateTime CommitTimeStamp { get; }
            public Uri Uri { get; }
            public JObject Value { get; }

            public int CompareTo(object obj)
            {
                return CommitTimeStamp.CompareTo(((CatalogItem)obj).CommitTimeStamp);
            }
        }
    }
}