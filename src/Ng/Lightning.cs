﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;
using VDS.RDF;

namespace Ng
{
    class Lightning
    {
        private static void PrintLightning()
        {
            var currentColor = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("                ,/");
            Console.WriteLine("              ,'/");
            Console.WriteLine("            ,' /");
            Console.WriteLine("          ,'  /_____,");
            Console.WriteLine("        .'____    ,'        NuGet - ng.exe lightning");
            Console.WriteLine("             /  ,'");
            Console.WriteLine("            / ,'            The lightning fast catalog2registration.");
            Console.WriteLine("           /,'");
            Console.WriteLine("          /'");
            Console.ForegroundColor = currentColor;
            Console.WriteLine();
        }

        public static void PrintUsage()
        {
            Console.WriteLine("Usage: ng lightning -command prepare|strike");
            Console.WriteLine();
            Console.WriteLine("The prepare command:");
            Console.WriteLine("  ng lightning -command prepare -outputFolder <output-folder> -catalogIndex <catalog-index-url> -contentBaseAddress <content-address> -templateFile <template-file> -batchSize 2000 -storageContainer <connection-string> -container <container> -storageBaseAddress <storage-base-address> -compress true|false [-verbose true|false]");
            Console.WriteLine();
            Console.WriteLine("  -outputFolder <output-folder>");
            Console.WriteLine("      The folder to generate files in.");
            Console.WriteLine("  -catalogIndex <catalog-index-url>");
            Console.WriteLine("      The catalog index.json URL to work with.");
            Console.WriteLine("  -contentBaseAddress <content-address>");
            Console.WriteLine("      The base address for package contents.");
            Console.WriteLine("  -templateFile <template-file>");
            Console.WriteLine("      The lightning-template.txt that calls the strike command per batch.");
            Console.WriteLine("  -batchSize 2000");
            Console.WriteLine("      The batch size.");
            Console.WriteLine("  -storageAccount <connection-string>");
            Console.WriteLine("      Azure Storage connection string.");
            Console.WriteLine("  -storageContainer <container>");
            Console.WriteLine("      Container to generate registrations in.");
            Console.WriteLine("  -storageBaseAddress <base-address>");
            Console.WriteLine("      Base address to write into registration blobs.");
            Console.WriteLine("  -compress true|false");
            Console.WriteLine("      Compress blobs?");
            Console.WriteLine("  -verbose true|false");
            Console.WriteLine("      Switch output verbosity on/off.");
            Console.WriteLine();
            Console.WriteLine("Traverses the given catalog and, using a template file and batch size,");
            Console.WriteLine("generates executable commands that can be run in parallel.");
            Console.WriteLine("The generated index.txt contains an alphabetical listing of all packages");
            Console.WriteLine("in the catalog with their entries.");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("The strike command:");
            Console.WriteLine("  ng lightning -command strike -indexFile <index-file> -cursorFile <cursor-file> -contentBaseAddress <content-address> -storageAccount <connection-string> -storageContainer <container> -storageBaseAddress <base-address> -compress true|false [-verbose true|false]");
            Console.WriteLine();
            Console.WriteLine("  -indexFile <index-file>");
            Console.WriteLine("      Index file generated by the lightning prepare command.");
            Console.WriteLine("  -cursorFile <cursor-file>");
            Console.WriteLine("      Cursor file containing range of the batch.");
            Console.WriteLine("  -contentBaseAddress <content-address>");
            Console.WriteLine("      The base address for package contents.");
            Console.WriteLine("  -storageAccount <connection-string>");
            Console.WriteLine("      Azure Storage connection string.");
            Console.WriteLine("  -storageContainer <container>");
            Console.WriteLine("      Container to generate registrations in.");
            Console.WriteLine("  -storageBaseAddress <base-address>");
            Console.WriteLine("      Base address to write into registration blobs.");
            Console.WriteLine("  -compress true|false");
            Console.WriteLine("      Compress blobs?");
            Console.WriteLine("  -verbose true|false");
            Console.WriteLine("      Switch output verbosity on/off.");
            Console.WriteLine();
            Console.WriteLine("The lightning strike command is used by the batch files generated with");
            Console.WriteLine("the prepare command. It creates registrations for a given batch of catalog");
            Console.WriteLine("entries.");
        }

        public static void Run(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            PrintLightning();

            string command = CommandHelpers.Get(arguments, "command");
            bool verbose = CommandHelpers.GetVerbose(arguments, required: false);

            var log = verbose ? Console.Out : new StringWriter();

            switch (command.ToLowerInvariant())
            {
                case "charge":
                case "prepare":
                    PrepareAsync(log, arguments).Wait(cancellationToken);
                    break;
                case "strike":
                    StrikeAsync(log, arguments).Wait(cancellationToken);
                    break;
                default:
                    PrintUsage();
                    break;
            }
        }

        private static async Task PrepareAsync(TextWriter log, IDictionary<string, string> arguments)
        {
            // Read arguments
            string outputFolder = CommandHelpers.Get(arguments, "outputFolder");
            string catalogIndex = CommandHelpers.Get(arguments, "catalogIndex");
            string templateFile = CommandHelpers.Get(arguments, "templateFile");
            string batchSize = CommandHelpers.Get(arguments, "batchSize");
            string contentBaseAddress = CommandHelpers.Get(arguments, "contentBaseAddress");
            string storageAccount = CommandHelpers.Get(arguments, "storageAccount");
            string storageContainer = CommandHelpers.Get(arguments, "storageContainer");
            string storageBaseAddress = CommandHelpers.Get(arguments, "storageBaseAddress");
            bool compress = CommandHelpers.GetBool(arguments, "compress", defaultValue: false, required: false);

            log.WriteLine("Making sure folder {0} exists.", outputFolder);
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            // Create reindex file
            log.WriteLine("Start preparing lightning reindex file...");

            var latestCommit = DateTime.MinValue;
            int numberOfEntries = 0;
            string indexFile = Path.Combine(outputFolder, "index.txt");
            using (var streamWriter = new StreamWriter(indexFile, false))
            {
                var collectorHttpClient = new CollectorHttpClient();
                var catalogIndexReader = new CatalogIndexReader(new Uri(catalogIndex), collectorHttpClient);

                var catalogIndexEntries = await catalogIndexReader.GetEntries();

                foreach (var packageRegistrationGroup in catalogIndexEntries
                    .OrderBy(x => x.CommitTimeStamp)
                    .ThenBy(x => x.Id)
                    .ThenBy(x => x.Version)
                    .GroupBy(x => x.Id))
                {
                    streamWriter.WriteLine("Element@{0}. {1}", numberOfEntries++, packageRegistrationGroup.Key);

                    var latestCatalogPages = new Dictionary<string, Uri>();

                    foreach (CatalogIndexEntry catalogIndexEntry in packageRegistrationGroup)
                    {
                        string key = catalogIndexEntry.Version.ToNormalizedString();
                        if (latestCatalogPages.ContainsKey(key))
                        {
                            latestCatalogPages[key] = catalogIndexEntry.Uri;
                        }
                        else
                        {
                            latestCatalogPages.Add(key, catalogIndexEntry.Uri);
                        }

                        if (latestCommit < catalogIndexEntry.CommitTimeStamp)
                        {
                            latestCommit = catalogIndexEntry.CommitTimeStamp;
                        }
                    }

                    foreach (var latestCatalogPage in latestCatalogPages)
                    {
                        streamWriter.WriteLine("{0}", latestCatalogPage.Value);
                    }
                }
            }

            log.WriteLine("Finished preparing lightning reindex file. Output file: {0}", indexFile);

            // Write cursor to storage
            log.WriteLine("Start writing new cursor...");

            var account = CloudStorageAccount.Parse(storageAccount);
            var storageFactory = (StorageFactory)new AzureStorageFactory(account, storageContainer);
            var storage = storageFactory.Create();
            var cursor = new DurableCursor(storage.ResolveUri("cursor.json"), storage, latestCommit);
            cursor.Value = latestCommit;
            await cursor.Save(CancellationToken.None);

            log.WriteLine("Finished writing new cursor.");
            
            // Write command files
            log.WriteLine("Start preparing lightning reindex command files...");

            string templateFileContents;
            using (var templateStreamReader = new StreamReader(templateFile))
            {
                templateFileContents = await templateStreamReader.ReadToEndAsync();
            }

            int batchNumber = 0;
            int batchSizeValue = int.Parse(batchSize);
            for (int batchStart = 0; batchStart < numberOfEntries; batchStart += batchSizeValue)
            {
                var batchEnd = (batchStart + batchSizeValue - 1);
                if (batchEnd >= numberOfEntries)
                {
                    batchEnd = numberOfEntries - 1;
                }

                var cursorCommandFileName = "cursor" + batchNumber + ".cmd";
                var cursorTextFileName = "cursor" + batchNumber + ".txt";

                using (var cursorCommandStreamWriter = new StreamWriter(Path.Combine(outputFolder, cursorCommandFileName)))
                {
                    using (var cursorTextStreamWriter = new StreamWriter(Path.Combine(outputFolder, cursorTextFileName)))
                    {
                        var commandStreamContents = templateFileContents
                            .Replace("[index]", indexFile)
                            .Replace("[cursor]", cursorTextFileName)
                            .Replace("[contentbaseaddress]", contentBaseAddress)
                            .Replace("[storageaccount]", storageAccount)
                            .Replace("[storagecontainer]", storageContainer)
                            .Replace("[storagebaseaddress]", storageBaseAddress)
                            .Replace("[compress]", compress.ToString().ToLowerInvariant());

                        await cursorCommandStreamWriter.WriteLineAsync(commandStreamContents);
                        await cursorTextStreamWriter.WriteLineAsync(batchStart + "," + batchEnd);
                    }
                }

                batchNumber++;
            }

            log.WriteLine("Finished preparing lightning reindex command files.");

            log.WriteLine("You can now copy the {0} file and all cursor*.cmd, cursor*.txt", indexFile);
            log.WriteLine("to multiple machines and run the cursor*.cmd files in parallel.");
        }

        private static async Task StrikeAsync(TextWriter log, IDictionary<string, string> arguments)
        {
            // Read arguments
            string indexFile = CommandHelpers.Get(arguments, "indexFile");
            string cursorFile = CommandHelpers.Get(arguments, "cursorFile");
            string contentBaseAddress = CommandHelpers.Get(arguments, "contentBaseAddress");
            string storageAccount = CommandHelpers.Get(arguments, "storageAccount");
            string storageContainer = CommandHelpers.Get(arguments, "storageContainer");
            string storageBaseAddress = CommandHelpers.Get(arguments, "storageBaseAddress");
            bool compress = CommandHelpers.GetBool(arguments, "compress", defaultValue: false, required: false);

            log.WriteLine("Start lightning strike for {0}...", cursorFile);

            // Get batch range
            int batchStart;
            int batchEnd;
            using (var cursorStreamReader = new StreamReader(cursorFile))
            {
                var batchRange = (await cursorStreamReader.ReadLineAsync()).Split(',');
                batchStart = int.Parse(batchRange[0]);
                batchEnd = int.Parse(batchRange[1]);

                log.WriteLine("Batch range: {0} - {1}", batchStart, batchEnd);
            }
            if (batchStart > batchEnd)
            {
                log.WriteLine("Batch already finished.");
                return;
            }

            // Time to strike
            var collectorHttpClient = new CollectorHttpClient();
            var account = CloudStorageAccount.Parse(storageAccount);
            var storageFactory = (StorageFactory)new AzureStorageFactory(account, storageContainer, null, new Uri(storageBaseAddress))
            {
                CompressContent = compress
            };

            var startElement = string.Format("Element@{0}.", batchStart);
            var endElement = string.Format("Element@{0}.", batchEnd + 1);
            using (var indexStreamReader = new StreamReader(indexFile))
            {
                string line;

                // Skip entries that are not in the current batch bounds
                do
                {
                    line = await indexStreamReader.ReadLineAsync();
                }
                while (!line.Contains(startElement));

                // Run until we're outside the current batch bounds
                while (!string.IsNullOrEmpty(line) &&  !line.Contains(endElement) && !indexStreamReader.EndOfStream)
                {
                    log.WriteLine(line);

                    try
                    {
                        var packageId = line.Split(new[] { ". " }, StringSplitOptions.None).Last().Trim();
                        
                        var sortedGraphs = new Dictionary<string, IGraph>();

                        line = await indexStreamReader.ReadLineAsync();
                        while (!string.IsNullOrEmpty(line) && !line.Contains("Element@") && !indexStreamReader.EndOfStream)
                        {
                            // Fetch graph for package version
                            var url = line.TrimEnd();
                            var graph = await collectorHttpClient.GetGraphAsync(new Uri(url));
                            if (sortedGraphs.ContainsKey(url))
                            {
                                sortedGraphs[url] = graph;
                            }
                            else
                            {
                                sortedGraphs.Add(url, graph);
                            }

                            // To reduce memory footprint, we're flushing out large registrations
                            // in very small batches.
                            if (graph.Nodes.Count() > 3000 && sortedGraphs.Count >= 2)
                            {
                                // Process graphs
                                await ProcessGraphsAsync(packageId, sortedGraphs, storageFactory, contentBaseAddress);

                                // Destroy!
                                sortedGraphs = new Dictionary<string, IGraph>();
                            }

                            // Read next line
                            line = await indexStreamReader.ReadLineAsync();
                        }

                        // Process graphs
                        if (sortedGraphs.Any())
                        {
                            await ProcessGraphsAsync(packageId, sortedGraphs, storageFactory, contentBaseAddress);
                        }

                        // Update cursor file so next time we have less work to do
                        batchStart++;
                        await UpdateCursorFileAsync(cursorFile, batchStart, batchEnd);
                    }
                    catch (Exception)
                    {
                        UpdateCursorFileAsync(cursorFile, batchStart, batchEnd).Wait();
                        throw;
                    }
                }
            }

            await UpdateCursorFileAsync("DONE" + cursorFile, batchStart, batchEnd);
            log.WriteLine("Finished lightning strike for {0}.", cursorFile);
        }

        private static Task UpdateCursorFileAsync(string cursorFileName, int startIndex, int endIndex)
        {
            using (var streamWriter = new StreamWriter(cursorFileName))
            {
                streamWriter.Write(startIndex);
                streamWriter.Write(",");
                streamWriter.Write(endIndex);
            }

            return Task.FromResult(true);
        }

        private static Task ProcessGraphsAsync(string packageId, IDictionary<string, IGraph> sortedGraphs, StorageFactory storageFactory, string contentBaseAddress)
        {
            return RegistrationMaker.Process(new RegistrationKey(packageId), sortedGraphs, storageFactory, new Uri(contentBaseAddress), 64, 128, CancellationToken.None);
        }
    }
}
