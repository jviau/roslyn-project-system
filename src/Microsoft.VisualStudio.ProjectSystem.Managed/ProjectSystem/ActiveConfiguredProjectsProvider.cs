﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.ProjectSystem.Configuration;

namespace Microsoft.VisualStudio.ProjectSystem
{
    [Export(typeof(IActiveConfiguredProjectsProvider))]
    internal class ActiveConfiguredProjectsProvider : IActiveConfiguredProjectsProvider
    {
        // A project configuration is considered active if its dimensions matches the active solution configuration skipping 
        // any ignored dimensions names (provided by IActiveConfiguredProjectsDimensionProvider instances):
        //
        // For example, given the following cross-targeting project:
        //
        //      -> All known project configurations:
        //
        //          Configuration   Platform    TargetFramework
        //          -------------------------------------------
        //                  Debug |   AnyCPU  |           net45
        //                  Debug |   AnyCPU  |           net46
        //                Release |   AnyCPU  |           net45
        //                Release |   AnyCPU  |           net46
        //
        //      -> Active solution configuration: 
        //
        //                  Debug |   AnyCPU  |           net45
        //
        //      -> Active configurations return by this class:
        //
        //                  Debug |   AnyCPU  |           net45
        //                  Debug |   AnyCPU  |           net46
        //
        // Whereas, given the following non-cross-targeting project:
        //
        //      -> All known project configurations:
        //
        //          Configuration   Platform
        //          ------------------------
        //                  Debug |   AnyCPU
        //                Release |   AnyCPU
        //
        //      -> Active solution configuration: 
        //
        //                  Debug |   AnyCPU
        //
        //      -> Active configurations return by this class:
        //
        //                  Debug |   AnyCPU

        private readonly IUnconfiguredProjectServices _services;
        private readonly UnconfiguredProject _project;

        [ImportingConstructor]
        public ActiveConfiguredProjectsProvider(IUnconfiguredProjectServices services, UnconfiguredProject project)
        {
            _services = services;
            _project = project;

            DimensionProviders = new OrderPrecedenceImportCollection<IActiveConfiguredProjectsDimensionProvider>(projectCapabilityCheckProvider: project);
        }

        [ImportMany]
        public OrderPrecedenceImportCollection<IActiveConfiguredProjectsDimensionProvider> DimensionProviders
        {
            get;
        }

        public async Task<ImmutableDictionary<string, ConfiguredProject>> GetActiveConfiguredProjectsMapAsync()
        {
            ImmutableDictionary<string, ConfiguredProject>.Builder builder = ImmutableDictionary.CreateBuilder<string, ConfiguredProject>();

            ActiveConfiguredObjects<ConfiguredProject> projects = await GetActiveConfiguredProjectsAsync().ConfigureAwait(false);

            bool isCrossTargeting = projects.Objects.All(project => project.ProjectConfiguration.IsCrossTargeting());
            if (isCrossTargeting)
            {
                foreach (ConfiguredProject project in projects.Objects)
                {
                    string targetFramework = project.ProjectConfiguration.Dimensions[ConfigurationGeneral.TargetFrameworkProperty];
                    builder.Add(targetFramework, project);
                }
            }
            else
            {
                builder.Add(string.Empty, projects.Objects[0]);
            }

            return builder.ToImmutable();
        }

        public async Task<ActiveConfiguredObjects<ConfiguredProject>> GetActiveConfiguredProjectsAsync()
        {
            ActiveConfiguredObjects<ProjectConfiguration> configurations = await GetActiveProjectConfigurationsAsync().ConfigureAwait(false);
            if (configurations == null)
                return null;

            ImmutableArray<ConfiguredProject>.Builder builder = ImmutableArray.CreateBuilder<ConfiguredProject>(configurations.Objects.Count);

            foreach (ProjectConfiguration configuration in configurations.Objects)
            {
                ConfiguredProject project = await _project.LoadConfiguredProjectAsync(configuration)
                                                          .ConfigureAwait(false);

                builder.Add(project);
            }

            return new ActiveConfiguredObjects<ConfiguredProject>(builder.MoveToImmutable(), configurations.DimensionNames);
        }

        public async Task<ActiveConfiguredObjects<ProjectConfiguration>> GetActiveProjectConfigurationsAsync()
        {
            ProjectConfiguration activeSolutionConfiguration = _services.ActiveConfiguredProjectProvider.ActiveProjectConfiguration;
            if (activeSolutionConfiguration == null)
                return null;

            // IImmutableSet<ProjectConfiguration> configurations = await _services.ProjectConfigurationsService.GetKnownProjectConfigurationsAsync()
            //                                                                                                  .ConfigureAwait(false);

            ImmutableArray<ProjectConfiguration>.Builder builder = ImmutableArray.CreateBuilder<ProjectConfiguration>(1);
            IImmutableSet<string> dimensionNames = GetDimensionNames();

            builder.Add(_project.Services.ActiveConfiguredProjectProvider.ActiveProjectConfiguration);

            // foreach (ProjectConfiguration configuration in configurations)
            // {
            //     if (IsActiveConfigurationCandidate(activeSolutionConfiguration, configuration, dimensionNames))
            //     {
            //         builder.Add(configuration);
            //     }
            // }

            Assumes.True(builder.Count > 0, "We have an active configuration that isn't one of the known configurations");
            return new ActiveConfiguredObjects<ProjectConfiguration>(builder.ToImmutable(), dimensionNames);
        }

        private IImmutableSet<string> GetDimensionNames()
        {
            ImmutableHashSet<string>.Builder builder = ImmutableHashSet.CreateBuilder(StringComparers.ConfigurationDimensionNames);

            foreach (Lazy<IActiveConfiguredProjectsDimensionProvider> dimensionProvider in DimensionProviders)
            {
                builder.Add(dimensionProvider.Value.DimensionName);
            }

            return builder.ToImmutable();
        }

        private static bool IsActiveConfigurationCandidate(ProjectConfiguration activeSolutionConfiguration, ProjectConfiguration configuration, IImmutableSet<string> ignoredDimensionNames)
        {
            foreach (KeyValuePair<string, string> dimension in activeSolutionConfiguration.Dimensions)
            {
                if (ignoredDimensionNames.Contains(dimension.Key))
                    continue;

                if (!configuration.Dimensions.TryGetValue(dimension.Key, out string otherDimensionValue) ||
                    !string.Equals(dimension.Value, otherDimensionValue, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
