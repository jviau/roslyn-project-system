﻿// Copyright(c) Microsoft.All Rights Reserved.Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.VisualStudio.ProjectSystem
{
    /// <summary>
    ///     Force loads the active <see cref="ConfiguredProject"/> objects so that any configured project-level 
    ///     services, such as evaluation and build services, are started.
    /// </summary>
    internal class ActiveConfiguredProjectsLoader : OnceInitializedOnceDisposed
    {
        private readonly UnconfiguredProject _project;
        private readonly IActiveConfigurationGroupService _activeConfigurationGroupService;
        private readonly IUnconfiguredProjectTasksService _tasksService;
        private ActionBlock<IProjectVersionedValue<IConfigurationGroup<ProjectConfiguration>>> _targetBlock;
        private IDisposable _subscription;

        [ImportingConstructor]
        public ActiveConfiguredProjectsLoader(UnconfiguredProject project, IActiveConfigurationGroupService activeConfigurationGroupService, IUnconfiguredProjectTasksService tasksService)
            : base(synchronousDisposal: true)
        {
            _project = project;
            _activeConfigurationGroupService = activeConfigurationGroupService;
            _tasksService = tasksService;
            _targetBlock = new ActionBlock<IProjectVersionedValue<IConfigurationGroup<ProjectConfiguration>>>(OnActiveConfigurationsChanged);
        }

        [ProjectAutoLoad(ProjectLoadCheckpoint.ProjectInitialCapabilitiesEstablished)]
        [AppliesTo(ProjectCapability.DotNetLanguageService)]
        public Task InitializeAsync()
        {
            EnsureInitialized();
            return Task.CompletedTask;
        }

        public ITargetBlock<IProjectVersionedValue<IConfigurationGroup<ProjectConfiguration>>> TargetBlock => _targetBlock;

        protected override void Initialize()
        {
            _subscription = _activeConfigurationGroupService.ActiveConfigurationGroupSource.SourceBlock.LinkTo(
                target: _targetBlock,
                linkOptions: new DataflowLinkOptions() { PropagateCompletion = true });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _subscription?.Dispose();
                _targetBlock.Complete();
            }
        }

        private Task OnActiveConfigurationsChanged(IProjectVersionedValue<IConfigurationGroup<ProjectConfiguration>> e)
        {
            // foreach (ProjectConfiguration configuration in e.Value)
            // {
            //     // Make sure we aren't currently unloading, or we don't unload while we load the configuration
            //     await _tasksService.LoadedProjectAsync(() =>
            //     {
            //         return _project.LoadConfiguredProjectAsync(configuration);

            //     }).ConfigureAwait(false);
            // }

            return Task.CompletedTask;
        }
    }
}
