// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Composition;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.Test.Common.Workspaces;

public abstract class WorkspaceTestBase(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    private bool _initialized;
    private ExportProvider? _exportProvider;
    private Workspace? _workspace;
    private IWorkspaceProvider? _workspaceProvider;
    private IProjectEngineFactoryProvider? _projectEngineFactoryProvider;

    protected ExportProvider ExportProvider
    {
        get
        {
            EnsureInitialized();
            return _exportProvider;
        }
    }

    protected Workspace Workspace
    {
        get
        {
            EnsureInitialized();
            return _workspace;
        }
    }

    private protected IWorkspaceProvider WorkspaceProvider
    {
        get
        {
            EnsureInitialized();
            return _workspaceProvider;
        }
    }

    private protected IProjectEngineFactoryProvider ProjectEngineFactoryProvider
    {
        get
        {
            EnsureInitialized();
            return _projectEngineFactoryProvider;
        }
    }

    private protected TestProjectSnapshotManager CreateProjectSnapshotManager()
        => CreateProjectSnapshotManager(ProjectEngineFactoryProvider);

    private protected TestProjectSnapshotManager CreateProjectSnapshotManager(IProjectEngineFactoryProvider projectEngineFactoryProvider)
        => new(projectEngineFactoryProvider, LoggerFactory, DisposalToken);

    protected virtual void ConfigureWorkspaceServices(List<IWorkspaceService> services)
    {
    }

    protected virtual TestComposition ConfigureComposition(TestComposition composition)
    {
        return composition;
    }

    protected virtual void ConfigureWorkspace(AdhocWorkspace workspace)
    {
    }

    protected virtual void ConfigureProjectEngine(RazorProjectEngineBuilder builder)
    {
    }

    [MemberNotNull(nameof(_exportProvider), nameof(_workspace), nameof(_workspaceProvider), nameof(_projectEngineFactoryProvider))]
    private void EnsureInitialized()
    {
        if (_initialized)
        {
            _exportProvider.AssumeNotNull();
            _workspace.AssumeNotNull();
            _workspaceProvider.AssumeNotNull();
            _projectEngineFactoryProvider.AssumeNotNull();
            return;
        }

        _projectEngineFactoryProvider = new TestProjectEngineFactoryProvider()
        {
            Configure = ConfigureProjectEngine,
        };

        var composition = ConfigureComposition(TestComposition.Empty);

        _exportProvider = composition.ExportProviderFactory.CreateExportProvider();

        var hostServices = MefHostServices.Create(_exportProvider.AsCompositionContext());

        _workspace = TestWorkspace.Create(hostServices, ConfigureWorkspace);
        AddDisposable(_workspace);
        _workspaceProvider = new TestWorkspaceProvider(_workspace);
        _initialized = true;
    }

    private sealed class TestWorkspaceProvider(Workspace workspace) : IWorkspaceProvider
    {
        public Workspace GetWorkspace() => workspace;
    }
}
