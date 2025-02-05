﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common.VisualStudio;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.ProjectSystem;

public class WorkspaceProjectStateChangeDetectorTest(ITestOutputHelper testOutput) : VisualStudioWorkspaceTestBase(testOutput)
{
    private static readonly HostProject s_hostProject1 = TestHostProject.Create(PathUtilities.CreateRootedPath("path", "One", "One.csproj"));
    private static readonly HostProject s_hostProject2 = TestHostProject.Create(PathUtilities.CreateRootedPath("path", "Two", "Two.csproj"));
    private static readonly HostProject s_hostProject3 = TestHostProject.Create(PathUtilities.CreateRootedPath("path", "Three", "Three.csproj"));

#nullable disable
    private Solution _emptySolution;
    private Solution _solutionWithOneProject;
    private Solution _solutionWithTwoProjects;
    private Solution _solutionWithDependentProject;
    private Project _projectNumberOne;
    private Project _projectNumberTwo;
    private Project _projectNumberThree;

    private DocumentId _cshtmlDocumentId;
    private DocumentId _razorDocumentId;
    private DocumentId _backgroundVirtualCSharpDocumentId;
    private DocumentId _partialComponentClassDocumentId;
    private DocumentId _razorDocumentIdForProjectThree;
#nullable enable

    protected override Task InitializeAsync()
    {
        _emptySolution = Workspace.CurrentSolution;

        var projectInfo1 = s_hostProject1.ToProjectInfo();
        var projectInfo2 = s_hostProject2.ToProjectInfo();
        var projectInfo3 = s_hostProject3.ToProjectInfo();

        var cshtmlDocumentInfo = projectInfo1.CreateDocumentInfo("file.cshtml.g.cs");
        _cshtmlDocumentId = cshtmlDocumentInfo.Id;

        var razorDocumentInfo = projectInfo1.CreateDocumentInfo("file.razor.g.cs");
        _razorDocumentId = razorDocumentInfo.Id;

        var backgroundDocumentInfo = projectInfo1.CreateDocumentInfo("file.razor__bg__virtual.cs");
        _backgroundVirtualCSharpDocumentId = backgroundDocumentInfo.Id;

        var partialComponentClassDocumentInfo = projectInfo1.CreateDocumentInfo("file.razor.cs");
        _partialComponentClassDocumentId = partialComponentClassDocumentInfo.Id;

        var razorDocumentInfoForProjectThree = projectInfo3.CreateDocumentInfo("file.razor.g.cs");
        _razorDocumentIdForProjectThree = razorDocumentInfoForProjectThree.Id;

        _solutionWithTwoProjects = _emptySolution
            .AddProject(projectInfo1
                .WithDocuments([cshtmlDocumentInfo, razorDocumentInfo, partialComponentClassDocumentInfo, backgroundDocumentInfo]))
            .AddProject(projectInfo2);

        _solutionWithOneProject = _emptySolution
            .AddProject(projectInfo3);

        _solutionWithDependentProject = _emptySolution
            .AddProject(projectInfo1
                .WithDocuments([cshtmlDocumentInfo, razorDocumentInfo, partialComponentClassDocumentInfo, backgroundDocumentInfo])
                .WithProjectReferences(projectInfo2.Id))
            .AddProject(projectInfo2
                .WithProjectReferences(projectInfo3.Id))
            .AddProject(projectInfo3
                .WithDocuments([razorDocumentInfoForProjectThree]));

        _projectNumberOne = _solutionWithTwoProjects.GetRequiredProject(projectInfo1.Id);
        _projectNumberTwo = _solutionWithTwoProjects.GetRequiredProject(projectInfo2.Id);
        _projectNumberThree = _solutionWithOneProject.GetRequiredProject(projectInfo3.Id);

        return Task.CompletedTask;
    }

    private WorkspaceProjectStateChangeDetector CreateDetector(IProjectWorkspaceStateGenerator generator, ProjectSnapshotManager projectManager)
    {
        var detector = new WorkspaceProjectStateChangeDetector(generator, projectManager, TestLanguageServerFeatureOptions.Instance, WorkspaceProvider, TimeSpan.FromMilliseconds(10));
        AddDisposable(detector);

        return detector;
    }

    [UIFact]
    public async Task SolutionClosing_StopsActiveWork()
    {
        // Arrange
        var generator = new TestProjectWorkspaceStateGenerator();
        var projectManager = CreateProjectSnapshotManager();
        using var detector = CreateDetector(generator, projectManager);
        var detectorAccessor = detector.GetTestAccessor();

        var workspaceChangedTask = detectorAccessor.ListenForWorkspaceChangesAsync(
            WorkspaceChangeKind.ProjectAdded,
            WorkspaceChangeKind.ProjectAdded);

        Workspace.TryApplyChanges(_solutionWithTwoProjects);

        await projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject1);
        });

        await workspaceChangedTask;
        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();

        generator.Clear();

        // Act
        await projectManager.UpdateAsync(updater =>
        {
            updater.SolutionClosed();

            // Trigger a project removed event while solution is closing to clear state.
            updater.RemoveProject(s_hostProject1.Key);
        });

        // Assert

        Assert.Empty(generator.Updates);
    }

    [UITheory]
    [InlineData(WorkspaceChangeKind.DocumentAdded)]
    [InlineData(WorkspaceChangeKind.DocumentChanged)]
    [InlineData(WorkspaceChangeKind.DocumentRemoved)]
    public async Task WorkspaceChanged_DocumentEvents_EnqueuesUpdatesForDependentProjects(WorkspaceChangeKind kind)
    {
        // Arrange
        var generator = new TestProjectWorkspaceStateGenerator();
        var projectManager = CreateProjectSnapshotManager();
        using var detector = CreateDetector(generator, projectManager);
        var detectorAccessor = detector.GetTestAccessor();

        await projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject1);
            updater.AddProject(s_hostProject2);
            updater.AddProject(s_hostProject3);
        });

        // Initialize with a project. This will get removed.
        var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.SolutionAdded, oldSolution: _emptySolution, newSolution: _solutionWithOneProject);
        detectorAccessor.WorkspaceChanged(e);

        e = new WorkspaceChangeEventArgs(kind, oldSolution: _solutionWithOneProject, newSolution: _solutionWithDependentProject);

        var solution = _solutionWithDependentProject.WithProjectAssemblyName(_projectNumberThree.Id, "Changed");

        e = new WorkspaceChangeEventArgs(kind, oldSolution: _solutionWithDependentProject, newSolution: solution, projectId: _projectNumberThree.Id, documentId: _razorDocumentIdForProjectThree);

        // Act
        detectorAccessor.WorkspaceChanged(e);

        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        Assert.Equal(3, generator.Updates.Count);
        Assert.Contains(generator.Updates, u => u.Key.Matches(_projectNumberOne));
        Assert.Contains(generator.Updates, u => u.Key.Matches(_projectNumberTwo));
        Assert.Contains(generator.Updates, u => u.Key.Matches(_projectNumberThree));
    }

    [UITheory]
    [InlineData(WorkspaceChangeKind.ProjectChanged)]
    [InlineData(WorkspaceChangeKind.ProjectAdded)]
    [InlineData(WorkspaceChangeKind.ProjectRemoved)]
    public async Task WorkspaceChanged_ProjectEvents_EnqueuesUpdatesForDependentProjects(WorkspaceChangeKind kind)
    {
        // Arrange
        var generator = new TestProjectWorkspaceStateGenerator();
        var projectManager = CreateProjectSnapshotManager();
        using var detector = CreateDetector(generator, projectManager);
        var detectorAccessor = detector.GetTestAccessor();

        await projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject1);
            updater.AddProject(s_hostProject2);
            updater.AddProject(s_hostProject3);
        });

        // Initialize with a project. This will get removed.
        var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.SolutionAdded, oldSolution: _emptySolution, newSolution: _solutionWithOneProject);
        detectorAccessor.WorkspaceChanged(e);

        e = new WorkspaceChangeEventArgs(kind, oldSolution: _solutionWithOneProject, newSolution: _solutionWithDependentProject);

        var solution = _solutionWithDependentProject.WithProjectAssemblyName(_projectNumberThree.Id, "Changed");

        e = new WorkspaceChangeEventArgs(kind, oldSolution: _solutionWithDependentProject, newSolution: solution, projectId: _projectNumberThree.Id);

        // Act
        detectorAccessor.WorkspaceChanged(e);

        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        Assert.Equal(3, generator.Updates.Count);
        Assert.Contains(generator.Updates, u => u.Key.Matches(_projectNumberOne));
        Assert.Contains(generator.Updates, u => u.Key.Matches(_projectNumberTwo));
        Assert.Contains(generator.Updates, u => u.Key.Matches(_projectNumberThree));
    }

    [UITheory]
    [InlineData(WorkspaceChangeKind.SolutionAdded)]
    [InlineData(WorkspaceChangeKind.SolutionChanged)]
    [InlineData(WorkspaceChangeKind.SolutionCleared)]
    [InlineData(WorkspaceChangeKind.SolutionReloaded)]
    [InlineData(WorkspaceChangeKind.SolutionRemoved)]
    public async Task WorkspaceChanged_SolutionEvents_EnqueuesUpdatesForProjectsInSolution(WorkspaceChangeKind kind)
    {
        // Arrange
        var generator = new TestProjectWorkspaceStateGenerator();
        var projectManager = CreateProjectSnapshotManager();
        using var detector = CreateDetector(generator, projectManager);
        var detectorAccessor = detector.GetTestAccessor();

        await projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject1);
            updater.AddProject(s_hostProject2);
        });

        var e = new WorkspaceChangeEventArgs(kind, oldSolution: _emptySolution, newSolution: _solutionWithTwoProjects);

        // Act
        detectorAccessor.WorkspaceChanged(e);

        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        Assert.Collection(
            generator.Updates,
            update => Assert.Equal(_projectNumberOne.Id, update.Id),
            update => Assert.Equal(_projectNumberTwo.Id, update.Id));
    }

    [UITheory]
    [InlineData(WorkspaceChangeKind.SolutionAdded)]
    [InlineData(WorkspaceChangeKind.SolutionChanged)]
    [InlineData(WorkspaceChangeKind.SolutionCleared)]
    [InlineData(WorkspaceChangeKind.SolutionReloaded)]
    [InlineData(WorkspaceChangeKind.SolutionRemoved)]
    public async Task WorkspaceChanged_SolutionEvents_EnqueuesStateClear_EnqueuesSolutionProjectUpdates(WorkspaceChangeKind kind)
    {
        // Arrange
        var generator = new TestProjectWorkspaceStateGenerator();
        var projectManager = CreateProjectSnapshotManager();
        using var detector = CreateDetector(generator, projectManager);
        var detectorAccessor = detector.GetTestAccessor();

        await projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject1);
            updater.AddProject(s_hostProject2);
            updater.AddProject(s_hostProject3);
        });

        // Initialize with a project. This will get removed.
        var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.SolutionAdded, oldSolution: _emptySolution, newSolution: _solutionWithOneProject);
        detectorAccessor.WorkspaceChanged(e);

        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();

        e = new WorkspaceChangeEventArgs(kind, oldSolution: _solutionWithOneProject, newSolution: _solutionWithTwoProjects);

        // Act
        detectorAccessor.WorkspaceChanged(e);

        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        Assert.Collection(
            generator.Updates,
            update => Assert.Equal(_projectNumberThree.Id, update.Id),
            update => Assert.Null(update.Id),
            update => Assert.Equal(_projectNumberOne.Id, update.Id),
            update => Assert.Equal(_projectNumberTwo.Id, update.Id));
    }

    [UITheory]
    [InlineData(WorkspaceChangeKind.ProjectChanged)]
    [InlineData(WorkspaceChangeKind.ProjectReloaded)]
    public async Task WorkspaceChanged_ProjectChangeEvents_UpdatesProjectState_AfterDelay(WorkspaceChangeKind kind)
    {
        // Arrange
        var generator = new TestProjectWorkspaceStateGenerator();
        var projectManager = CreateProjectSnapshotManager();
        using var detector = CreateDetector(generator, projectManager);
        var detectorAccessor = detector.GetTestAccessor();

        await projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject1);
        });

        // Stop any existing work and clear out any updates that we might have received.
        detectorAccessor.CancelExistingWork();
        generator.Clear();

        // Create a listener for the workspace change we're about to send.
        var listenerTask = detectorAccessor.ListenForWorkspaceChangesAsync(kind);

        var solution = _solutionWithTwoProjects.WithProjectAssemblyName(_projectNumberOne.Id, "Changed");
        var e = new WorkspaceChangeEventArgs(kind, oldSolution: _solutionWithTwoProjects, newSolution: solution, projectId: _projectNumberOne.Id);

        // Act
        detectorAccessor.WorkspaceChanged(e);

        await listenerTask;
        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        var update = Assert.Single(generator.Updates);
        Assert.Equal(_projectNumberOne.Id, update.Id);
        Assert.Equal(s_hostProject1.Key, update.Key);
    }

    [UIFact]
    public async Task WorkspaceChanged_DocumentChanged_BackgroundVirtualCS_UpdatesProjectState_AfterDelay()
    {
        // Arrange
        var generator = new TestProjectWorkspaceStateGenerator();
        var projectManager = CreateProjectSnapshotManager();
        using var detector = CreateDetector(generator, projectManager);
        var detectorAccessor = detector.GetTestAccessor();

        Workspace.TryApplyChanges(_solutionWithTwoProjects);

        await projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject1);
        });

        generator.Clear();

        var solution = _solutionWithTwoProjects.WithDocumentText(_backgroundVirtualCSharpDocumentId, SourceText.From("public class Foo{}"));
        var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.DocumentChanged, oldSolution: _solutionWithTwoProjects, newSolution: solution, projectId: _projectNumberOne.Id, _backgroundVirtualCSharpDocumentId);

        // Act
        detectorAccessor.WorkspaceChanged(e);

        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        var update = Assert.Single(generator.Updates);
        Assert.Equal(_projectNumberOne.Id, update.Id);
        Assert.Equal(s_hostProject1.Key, update.Key);
    }

    [UIFact]
    public async Task WorkspaceChanged_DocumentChanged_CSHTML_UpdatesProjectState_AfterDelay()
    {
        // Arrange
        var generator = new TestProjectWorkspaceStateGenerator();
        var projectManager = CreateProjectSnapshotManager();
        using var detector = CreateDetector(generator, projectManager);
        var detectorAccessor = detector.GetTestAccessor();

        Workspace.TryApplyChanges(_solutionWithTwoProjects);

        await projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject1);
        });

        generator.Clear();

        var solution = _solutionWithTwoProjects.WithDocumentText(_cshtmlDocumentId, SourceText.From("Hello World"));
        var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.DocumentChanged, oldSolution: _solutionWithTwoProjects, newSolution: solution, projectId: _projectNumberOne.Id, _cshtmlDocumentId);

        // Act
        detectorAccessor.WorkspaceChanged(e);

        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        var update = Assert.Single(generator.Updates);
        Assert.Equal(_projectNumberOne.Id, update.Id);
        Assert.Equal(s_hostProject1.Key, update.Key);
    }

    [UIFact]
    public async Task WorkspaceChanged_DocumentChanged_Razor_UpdatesProjectState_AfterDelay()
    {
        // Arrange
        var generator = new TestProjectWorkspaceStateGenerator();
        var projectManager = CreateProjectSnapshotManager();
        using var detector = CreateDetector(generator, projectManager);
        var detectorAccessor = detector.GetTestAccessor();

        Workspace.TryApplyChanges(_solutionWithTwoProjects);

        await projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject1);
        });

        generator.Clear();

        var solution = _solutionWithTwoProjects.WithDocumentText(_razorDocumentId, SourceText.From("Hello World"));
        var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.DocumentChanged, oldSolution: _solutionWithTwoProjects, newSolution: solution, projectId: _projectNumberOne.Id, _razorDocumentId);

        // Act
        detectorAccessor.WorkspaceChanged(e);

        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        var update = Assert.Single(generator.Updates);
        Assert.Equal(_projectNumberOne.Id, update.Id);
        Assert.Equal(s_hostProject1.Key, update.Key);
    }

    [UIFact]
    public async Task WorkspaceChanged_DocumentChanged_PartialComponent_UpdatesProjectState_AfterDelay()
    {
        // Arrange
        var generator = new TestProjectWorkspaceStateGenerator();
        var projectManager = CreateProjectSnapshotManager();
        using var detector = CreateDetector(generator, projectManager);
        var detectorAccessor = detector.GetTestAccessor();

        Workspace.TryApplyChanges(_solutionWithTwoProjects);

        await projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject1);
        });

        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();
        generator.Clear();

        var sourceText = SourceText.From($$"""
            public partial class TestComponent : {{ComponentsApi.IComponent.MetadataName}} {}
            namespace Microsoft.AspNetCore.Components
            {
                public interface IComponent {}
            }
            """);
        var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
        var solution = _solutionWithTwoProjects
            .WithDocumentText(_partialComponentClassDocumentId, sourceText)
            .WithDocumentSyntaxRoot(_partialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
        var document = solution.GetRequiredDocument(_partialComponentClassDocumentId);

        // The change detector only operates when a semantic model / syntax tree is available.
        await document.GetSyntaxRootAsync();
        await document.GetSemanticModelAsync();

        var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.DocumentChanged, oldSolution: solution, newSolution: solution, projectId: _projectNumberOne.Id, _partialComponentClassDocumentId);

        // Act
        detectorAccessor.WorkspaceChanged(e);

        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        var update = Assert.Single(generator.Updates);
        Assert.Equal(_projectNumberOne.Id, update.Id);
        Assert.Equal(s_hostProject1.Key, update.Key);
    }

    [UIFact]
    public async Task WorkspaceChanged_ProjectRemovedEvent_QueuesProjectStateRemoval()
    {
        // Arrange
        var generator = new TestProjectWorkspaceStateGenerator();
        var projectManager = CreateProjectSnapshotManager();
        using var detector = CreateDetector(generator, projectManager);
        var detectorAccessor = detector.GetTestAccessor();

        await projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject1);
            updater.AddProject(s_hostProject2);
        });

        var solution = _solutionWithTwoProjects.RemoveProject(_projectNumberOne.Id);
        var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.ProjectRemoved, oldSolution: _solutionWithTwoProjects, newSolution: solution, projectId: _projectNumberOne.Id);

        // Act
        detectorAccessor.WorkspaceChanged(e);
        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        Assert.Single(
            generator.Updates,
            update => update.Id is null);
    }

    [UIFact]
    public async Task WorkspaceChanged_ProjectAddedEvent_AddsProject()
    {
        // Arrange
        var generator = new TestProjectWorkspaceStateGenerator();
        var projectManager = CreateProjectSnapshotManager();
        using var detector = CreateDetector(generator, projectManager);
        var detectorAccessor = detector.GetTestAccessor();

        await projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject3);
        });

        var solution = _solutionWithOneProject;
        var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.ProjectAdded, oldSolution: _emptySolution, newSolution: solution, projectId: _projectNumberThree.Id);

        // Act
        var listenerTask = detectorAccessor.ListenForWorkspaceChangesAsync(WorkspaceChangeKind.ProjectAdded);
        detectorAccessor.WorkspaceChanged(e);
        await listenerTask;
        await detectorAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        Assert.Single(
            generator.Updates,
            update => update.Id == _projectNumberThree.Id);
    }

    [Fact]
    public async Task IsPartialComponentClass_NoIComponent_ReturnsFalse()
    {
        // Arrange
        var sourceText = SourceText.From("""
            public partial class TestComponent{}
            """);
        var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
        var solution = _solutionWithTwoProjects
            .WithDocumentText(_partialComponentClassDocumentId, sourceText)
            .WithDocumentSyntaxRoot(_partialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
        var document = solution.GetRequiredDocument(_partialComponentClassDocumentId);

        // Initialize document
        await document.GetSyntaxRootAsync();
        await document.GetSemanticModelAsync();

        // Act
        var result = WorkspaceProjectStateChangeDetector.IsPartialComponentClass(document);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsPartialComponentClass_InitializedDocument_ReturnsTrue()
    {
        // Arrange
        var sourceText = SourceText.From($$"""
            public partial class TestComponent : {{ComponentsApi.IComponent.MetadataName}} {}
            namespace Microsoft.AspNetCore.Components
            {
                public interface IComponent {}
            }
            """);
        var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
        var solution = _solutionWithTwoProjects
            .WithDocumentText(_partialComponentClassDocumentId, sourceText)
            .WithDocumentSyntaxRoot(_partialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
        var document = solution.GetRequiredDocument(_partialComponentClassDocumentId);

        // Initialize document
        await document.GetSyntaxRootAsync();
        await document.GetSemanticModelAsync();

        // Act
        var result = WorkspaceProjectStateChangeDetector.IsPartialComponentClass(document);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPartialComponentClass_Uninitialized_ReturnsFalse()
    {
        // Arrange
        var sourceText = SourceText.From($$"""
            public partial class TestComponent : {{ComponentsApi.IComponent.MetadataName}} {}
            namespace Microsoft.AspNetCore.Components
            {
                public interface IComponent {}
            }
            """);
        var syntaxTreeRoot = CSharpSyntaxTree.ParseText(sourceText).GetRoot();
        var solution = _solutionWithTwoProjects
            .WithDocumentText(_partialComponentClassDocumentId, sourceText)
            .WithDocumentSyntaxRoot(_partialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
        var document = solution.GetRequiredDocument(_partialComponentClassDocumentId);

        // Act
        var result = WorkspaceProjectStateChangeDetector.IsPartialComponentClass(document);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsPartialComponentClass_UninitializedSemanticModel_ReturnsFalse()
    {
        // Arrange
        var sourceText = SourceText.From($$"""
            public partial class TestComponent : {{ComponentsApi.IComponent.MetadataName}} {}
            namespace Microsoft.AspNetCore.Components
            {
                public interface IComponent {}
            }
            """);
        var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
        var solution = _solutionWithTwoProjects
            .WithDocumentText(_partialComponentClassDocumentId, sourceText)
            .WithDocumentSyntaxRoot(_partialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
        var document = solution.GetRequiredDocument(_partialComponentClassDocumentId);

        await document.GetSyntaxRootAsync();

        // Act
        var result = WorkspaceProjectStateChangeDetector.IsPartialComponentClass(document);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsPartialComponentClass_NonClass_ReturnsFalse()
    {
        // Arrange
        var sourceText = SourceText.From(string.Empty);
        var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
        var solution = _solutionWithTwoProjects
            .WithDocumentText(_partialComponentClassDocumentId, sourceText)
            .WithDocumentSyntaxRoot(_partialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
        var document = solution.GetRequiredDocument(_partialComponentClassDocumentId);

        // Initialize document
        await document.GetSyntaxRootAsync();
        await document.GetSemanticModelAsync();

        // Act
        var result = WorkspaceProjectStateChangeDetector.IsPartialComponentClass(document);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsPartialComponentClass_MultipleClassesOneComponentPartial_ReturnsTrue()
    {
        // Arrange
        var sourceText = SourceText.From($$"""
            public partial class NonComponent1 {}
            public class NonComponent2 {}
            public partial class TestComponent : {{ComponentsApi.IComponent.MetadataName}} {}
            public partial class NonComponent3 {}
            public class NonComponent4 {}
            namespace Microsoft.AspNetCore.Components
            {
                public interface IComponent {}
            }
            """);
        var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
        var solution = _solutionWithTwoProjects
            .WithDocumentText(_partialComponentClassDocumentId, sourceText)
            .WithDocumentSyntaxRoot(_partialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
        var document = solution.GetRequiredDocument(_partialComponentClassDocumentId);

        // Initialize document
        await document.GetSyntaxRootAsync();
        await document.GetSemanticModelAsync();

        // Act
        var result = WorkspaceProjectStateChangeDetector.IsPartialComponentClass(document);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsPartialComponentClass_NonComponents_ReturnsFalse()
    {
        // Arrange
        var sourceText = SourceText.From("""
            public partial class NonComponent1 {}
            public class NonComponent2 {}
            public partial class NonComponent3 {}
            public class NonComponent4 {}
            namespace Microsoft.AspNetCore.Components
            {
                public interface IComponent {}
            }
            """);
        var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
        var solution = _solutionWithTwoProjects
            .WithDocumentText(_partialComponentClassDocumentId, sourceText)
            .WithDocumentSyntaxRoot(_partialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
        var document = solution.GetRequiredDocument(_partialComponentClassDocumentId);

        // Initialize document
        await document.GetSyntaxRootAsync();
        await document.GetSemanticModelAsync();

        // Act
        var result = WorkspaceProjectStateChangeDetector.IsPartialComponentClass(document);

        // Assert
        Assert.False(result);
    }
}
