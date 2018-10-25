// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class Db2AzureSearchCommand : IDb2AzureSearchCommand
    {
        private const int MaxAzureSearchBatchSize = 1000;

        private readonly INewPackageRegistrationProducer _producer;
        private readonly IIndexActionBuilder _indexActionBuilder;
        private readonly IOptionsSnapshot<Db2AzureSearchConfiguration> _options;
        private readonly ILogger<Db2AzureSearchCommand> _logger;

        public Db2AzureSearchCommand(
            INewPackageRegistrationProducer producer,
            IIndexActionBuilder indexActionBuilder,
            IOptionsSnapshot<Db2AzureSearchConfiguration> options,
            ILogger<Db2AzureSearchCommand> logger)
        {
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
            _indexActionBuilder = indexActionBuilder ?? throw new ArgumentNullException(nameof(indexActionBuilder));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExecuteAsync()
        {
            var allWork = new ConcurrentBag<NewPackageRegistration>();
            var cancelledCts = new CancellationTokenSource();
            var completedCts = new CancellationTokenSource();

            // Set up the producer and the consumers.
            var producerTask = ProduceWorkAsync(allWork, completedCts, cancelledCts.Token);
            var consumerTasks = Enumerable
                .Range(0, _options.Value.WorkerCount)
                .Select(i => ConsumeWorkAsync(allWork, completedCts.Token, cancelledCts.Token));
            var allTasks = new[] { producerTask }.Concat(consumerTasks);

            // If one of the tasks throws an exception before the work is completed, cancel the work.
            var firstTask = await Task.WhenAny(allTasks);
            if (firstTask.IsFaulted)
            {
                cancelledCts.Cancel();
            }

            await firstTask;
            await Task.WhenAll(allTasks);
        }

        private async Task ProduceWorkAsync(
            ConcurrentBag<NewPackageRegistration> allWork,
            CancellationTokenSource completedCts,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            await _producer.ProduceWorkAsync(allWork, cancellationToken);
            completedCts.Cancel();
        }

        private async Task ConsumeWorkAsync(
            ConcurrentBag<NewPackageRegistration> allWork,
            CancellationToken completedToken,
            CancellationToken cancellationToken)
        {
            await Task.Yield();

            while ((allWork.TryTake(out var work) || !completedToken.IsCancellationRequested)
                && !cancellationToken.IsCancellationRequested)
            {
                // If there's no work to do, wait a bit before checking again.
                if (work == null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    continue;
                }

                try
                {
                    var indexActions = _indexActionBuilder.AddNewPackageRegistration(work);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        0,
                        ex,
                        "An exception was thrown while processing package ID {PackageId}.",
                        work.PackageId);
                    throw;
                }
            }
        }
    }
}
