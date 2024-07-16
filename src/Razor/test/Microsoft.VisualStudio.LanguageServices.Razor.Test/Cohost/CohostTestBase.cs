// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Razor.LanguageClient.Cohost;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Test.Cohost;

public abstract class CohostTestBase(ITestOutputHelper testOutputHelper) : WorkspaceTestBase(testOutputHelper)
{
    protected async Task<CSharpTestLspServer> CreateLanguageServerAsync()
    {
        var serviceProvider = (ShortCircuitingRemoteServiceProvider)ExportProvider.GetExportedValue<IRemoteServiceProvider>();
        serviceProvider.SetTestOutputHelper(TestOutputHelper);

        return await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(new VSInternalServerCapabilities(), capabilitiesUpdater: null, ExportProvider, Workspace, createCohostServer: true, DisposalToken);
    }

    protected TextDocument CreateRazorDocument(string contents)
    {
        var projectFilePath = TestProjectData.SomeProject.FilePath;
        var documentFilePath = TestProjectData.SomeProjectComponentFile1.FilePath;
        var projectName = Path.GetFileNameWithoutExtension(projectFilePath);
        var projectId = ProjectId.CreateNewId(debugName: projectName);
        var documentId = DocumentId.CreateNewId(projectId, debugName: documentFilePath);

        var solution = Workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            name: projectName,
            assemblyName: projectName,
            LanguageNames.CSharp,
            documentFilePath));

        solution = solution.AddAdditionalDocument(
            documentId,
            documentFilePath,
            SourceText.From(contents),
            filePath: documentFilePath);

        Workspace.TryApplyChanges(solution);

        return solution.GetAdditionalDocument(documentId).AssumeNotNull();
    }

    protected override TestComposition ConfigureComposition(TestComposition composition)
    {
        return composition
            .Add(TestComposition.Roslyn)
            .AddAssemblies(typeof(CohostLinkedEditingRangeEndpoint).Assembly)
            .AddExcludedPartTypes(typeof(IRemoteServiceProvider))
            .AddParts(typeof(ShortCircuitingRemoteServiceProvider));
    }
}
