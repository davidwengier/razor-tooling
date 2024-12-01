﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Text;
using Roslyn.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostFindAllReferencesEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Theory]
    [CombinatorialData]
    public Task FindCSharpMember(bool supportsVSExtensions)
        => VerifyFindAllReferencesAsync("""
            @{
                string M()
                {
                    return [|MyName|];
                }
            }

            <p>@[|MyName|]</p>

            @code {
                private const string [|$$MyName|] = "David";
            }
            """,
            supportsVSExtensions);

    [Theory]
    [CombinatorialData]
    public async Task ComponentAttribute(bool supportsVSExtensions)
    {
        TestCode input = """
            <SurveyPrompt [|Ti$$tle|]="InputValue" />
            """;

        TestCode surveyPrompt = """
            @namespace SomeProject

            <div></div>

            @code
            {
                [Parameter]
                public string [|Title|] { get; set; }
            }
            """;

        TestCode surveyPromptGeneratedCode = """
            // <auto-generated/>
            #pragma warning disable 1591
            namespace SomeProject
            {
                #line default
                using global::System;
                using global::System.Collections.Generic;
                using global::System.Linq;
                using global::System.Threading.Tasks;
            #nullable restore
            #line 1 "c:\users\example\src\SomeProject\_Imports.razor"
            using Microsoft.AspNetCore.Components;

            #nullable disable
            #nullable restore
            #line 2 "c:\users\example\src\SomeProject\_Imports.razor"
            using Microsoft.AspNetCore.Components.Authorization;

            #nullable disable
            #nullable restore
            #line 3 "c:\users\example\src\SomeProject\_Imports.razor"
            using Microsoft.AspNetCore.Components.Forms;

            #nullable disable
            #nullable restore
            #line 4 "c:\users\example\src\SomeProject\_Imports.razor"
            using Microsoft.AspNetCore.Components.Routing;

            #nullable disable
            #nullable restore
            #line 5 "c:\users\example\src\SomeProject\_Imports.razor"
            using Microsoft.AspNetCore.Components.Web;

            #line default
            #line hidden
            #nullable disable
                #nullable restore
                public partial class SurveyPrompt : global::Microsoft.AspNetCore.Components.ComponentBase
                #nullable disable
                {
                    #pragma warning disable 219
                    private void __RazorDirectiveTokenHelpers__() {
                    ((global::System.Action)(() => {
            #nullable restore
            #line 1 "c:\users\example\src\SomeProject\SurveyPrompt.razor"
            global::System.Object __typeHelper = nameof(SomeProject);

            #line default
            #line hidden
            #nullable disable
                    }
                    ))();
                    }
                    #pragma warning restore 219
                    #pragma warning disable 0414
                    private static object __o = null;
                    #pragma warning restore 0414
                    #pragma warning disable 1998
                    protected override void BuildRenderTree(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
                    {
                    }
                    #pragma warning restore 1998
            #nullable restore
            #line 6 "c:\users\example\src\SomeProject\SurveyPrompt.razor"

                [Parameter]
                public string Title { get; set; }

            #line default
            #line hidden
            #nullable disable
                }
            }
            #pragma warning restore 1591
            """;

        await VerifyFindAllReferencesAsync(input, supportsVSExtensions,
            (FilePath("SurveyPrompt.razor"), surveyPrompt),
            (FilePath("SurveyPrompt.razor.g.cs"), surveyPromptGeneratedCode));
    }

    [Theory]
    [CombinatorialData]
    public async Task OtherCSharpFile(bool supportsVSExtensions)
    {
        TestCode input = """
            @code
            {
                public void M()
                {
                    var x = new OtherClass();
                    x.[|D$$ate|].ToString();
                }
            }
            """;

        TestCode otherClass = """
            using System;

            namespace SomeProject;

            public class OtherClass
            {
                public DateTime [|Date|] => DateTime.Now;
            }
            """;

        await VerifyFindAllReferencesAsync(input, supportsVSExtensions,
            (FilePath("OtherClass.cs"), otherClass));
    }

    private async Task VerifyFindAllReferencesAsync(TestCode input, bool supportsVSExtensions, params (string fileName, TestCode testCode)[]? additionalFiles)
    {
        UpdateClientLSPInitializationOptions(c =>
        {
            c.ClientCapabilities.SupportsVisualStudioExtensions = supportsVSExtensions;
            return c;
        });

        var document = await CreateProjectAndRazorDocumentAsync(input.Text, additionalFiles: additionalFiles.Select(f => (f.fileName, f.testCode.Text)).ToArray());
        var inputText = await document.GetTextAsync(DisposalToken);
        var position = inputText.GetPosition(input.Position);

        var endpoint = new CohostFindAllReferencesEndpoint(RemoteServiceInvoker);

        var textDocumentPositionParams = new TextDocumentPositionParams
        {
            Position = position,
            TextDocument = new TextDocumentIdentifier { Uri = document.CreateUri() },
        };

        var results = await endpoint.GetTestAccessor().HandleRequestAsync(document, position, DisposalToken);

        Assumes.NotNull(results);

        var totalSpans = input.Spans.Length + additionalFiles.Sum(f => f.testCode.TryGetNamedSpans("", out var spans) ? spans.Length : 0);
        Assert.Equal(totalSpans, results.Length);

        var razorDocumentUri = document.CreateUri();

        foreach (var result in results)
        {
            if (result.TryGetFirst(out var referenceItem))
            {
                Assert.True(supportsVSExtensions);
                if (referenceItem.DisplayPath is not null)
                {
                    Assert.False(referenceItem.DisplayPath.EndsWith(".g.cs"));
                }

                if (referenceItem.DocumentName is not null)
                {
                    Assert.False(referenceItem.DocumentName.EndsWith(".g.cs"));
                }
            }
            else
            {
                Assert.False(supportsVSExtensions);
            }
        }

        foreach (var location in results.Select(GetLocation))
        {
            if (razorDocumentUri.Equals(location.Uri))
            {
                Assert.Single(input.Spans.Where(s => inputText.GetRange(s).Equals(location.Range)));
            }
            else
            {
                var additionalFile = Assert.Single(additionalFiles.Where(f => FilePathNormalizingComparer.Instance.Equals(f.fileName, location.Uri.AbsolutePath)));
                var text = SourceText.From(additionalFile.testCode.Text);
                Assert.Single(additionalFile.testCode.Spans.Where(s => text.GetRange(s).Equals(location.Range)));
            }
        }
    }

    private static Location GetLocation(SumType<VSInternalReferenceItem, Location> r)
    {
        return r.TryGetFirst(out var refItem)
            ? refItem.Location ?? Assumed.Unreachable<Location>()
            : r.Second;
    }
}
