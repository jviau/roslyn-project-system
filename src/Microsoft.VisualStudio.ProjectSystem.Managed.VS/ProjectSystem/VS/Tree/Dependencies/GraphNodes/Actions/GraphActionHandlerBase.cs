﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.GraphModel;
using Microsoft.VisualStudio.GraphModel.Schemas;
using Microsoft.VisualStudio.ProjectSystem.VS.Tree.Dependencies.GraphNodes.ViewProviders;
using Microsoft.VisualStudio.ProjectSystem.VS.Tree.Dependencies.Snapshot;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Tree.Dependencies.GraphNodes.Actions
{
    internal class GraphActionHandlerBase : IDependenciesGraphActionHandler
    {
        public GraphActionHandlerBase(IDependenciesGraphBuilder builder,
                                      IAggregateDependenciesSnapshotProvider aggregateSnapshotProvider)
        {
            Builder = builder;
            AggregateSnapshotProvider = aggregateSnapshotProvider;
            ViewProviders = new OrderPrecedenceImportCollection<IDependenciesGraphViewProvider>(
                                    ImportOrderPrecedenceComparer.PreferenceOrder.PreferredComesFirst);
        }

        protected IDependenciesGraphBuilder Builder { get; }
        protected IAggregateDependenciesSnapshotProvider AggregateSnapshotProvider { get; }

        [ImportMany]
        protected OrderPrecedenceImportCollection<IDependenciesGraphViewProvider> ViewProviders { get; }
       
        public virtual bool CanHandleRequest(IGraphContext graphContext)
        {
            return false;
        }

        public virtual bool CanHandleChanges()
        {
            return false;
        }

        public virtual bool HandleRequest(IGraphContext graphContext)
        {
            return false;
        }

        public virtual bool HandleChanges(IGraphContext graphContext, SnapshotChangedEventArgs changes)
        {
            return false;
        }

        protected IDependency GetDependency(
            IGraphContext graphContext, 
            GraphNode inputGraphNode, 
            out IDependenciesSnapshot snapshot)
        {
            snapshot = null;

            var projectPath = inputGraphNode.Id.GetValue(CodeGraphNodeIdName.Assembly);
            if (string.IsNullOrEmpty(projectPath))
            {
                return null;
            }

            var projectFolder = Path.GetDirectoryName(projectPath);
            var id = inputGraphNode.GetValue<string>(DependenciesGraphSchema.DependencyIdProperty);
            if (id == null)
            {
                id = inputGraphNode.Id.GetValue(CodeGraphNodeIdName.File);
                if (id.StartsWith(projectFolder, StringComparison.OrdinalIgnoreCase))
                {
                    id = id.Substring(projectFolder.Length).TrimStart('\\');
                }
            }

            if (id == null)
            {
                return null;
            }

            // always refresh
            return GetDependency(projectPath, id, out snapshot);
        }

        protected IDependency GetDependency(
            string projectPath, 
            string dependencyId, 
            out IDependenciesSnapshot snapshot)
        {
            snapshot = GetSnapshot(projectPath);
            return snapshot?.FindDependency(dependencyId);
        }

        protected IDependenciesSnapshot GetSnapshot(string projectPath)
        {
            var snapshotProvider = AggregateSnapshotProvider.GetSnapshotProvider(projectPath);
            return snapshotProvider?.CurrentSnapshot;
        }
    }
}
