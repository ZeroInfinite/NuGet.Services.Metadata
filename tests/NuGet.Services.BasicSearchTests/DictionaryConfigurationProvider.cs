﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Configuration;

namespace NuGet.Services.BasicSearchTests
{
    internal class DictionaryConfigurationProvider : ConfigurationProvider
    {
        private readonly IDictionary<string, string> _configuration;

        public DictionaryConfigurationProvider(IDictionary<string, string> configuration)
        {
            _configuration = configuration;
        }

        protected override Task<string> Get(string key)
        {
            if (_configuration.TryGetValue(key, out var value))
            {
                return Task.FromResult(value);
            }

            return Task.FromResult<string>(null);
        }
    }
}
