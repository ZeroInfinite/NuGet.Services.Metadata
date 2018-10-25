// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Services.AzureSearch.Db2AzureSearch;

namespace NuGet.Jobs
{
    public class Job : JsonConfigurationJob
    {
        private const string Db2AzureSearchSectionName = "Db2AzureSearch";

        public override async Task Run()
        {
            var db2AzureSearch = _serviceProvider.GetRequiredService<IDb2AzureSearchCommand>();
            await db2AzureSearch.ExecuteAsync();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<Db2AzureSearchConfiguration>(configurationRoot.GetSection(Db2AzureSearchSectionName));

            services.AddTransient<IEntitiesContextFactory, EntitiesContextFactory>();
            services.AddTransient<INewPackageRegistrationProducer, NewPackageRegistrationProducer>();
            services.AddTransient<IIndexActionBuilder, IndexActionBuilder>();
            services.AddTransient<IDb2AzureSearchCommand, Db2AzureSearchCommand>();
        }
    }
}
