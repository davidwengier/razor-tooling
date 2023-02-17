// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.CodeActions.CSharp;

public class CSharpCodeActionEndToEndTest : SingleServerDelegatingEndpointTestBase
{
    public CSharpCodeActionEndToEndTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    [Fact]
    public async Task Handle_GenerateConstructor()
    {
        var input = """

            <div></div>

            @functions
            {
                public class [||]Goo
                {
                }
            }

            """;

        var expected = """
            
            <div></div>
            
            @functions
            {
                public class Goo
                {
                    public Goo()
                    {
                    }
                }
            }

            """;

        await ValidateCodeActionAsync(input, "Generate Default Constructors Code Action Provider", expected);
    }

    [Fact]
    public async Task Handle_GenerateMethod()
    {
        var input = """

            <div></div>

            @functions
            {
                public void M()
                {
                    var x = [|MyFancyMethod|]();
                }

                public void N()
                {
                }
            }

            """;

        var expected = """
            
            <div></div>
            
            @functions
            {
                public void M()
                {
                    var x = MyFancyMethod();
                }

                private object MyFancyMethod()
                {
                    throw new NotImplementedException();
                }

                public void N()
                {
                }
            }

            """;

        await ValidateCodeActionAsync(input, "GenerateMethod", expected);
    }

    private async Task ValidateCodeActionAsync(string input, string codeAction, string expected)
    {
        TestFileMarkupParser.GetSpan(input, out input, out var textSpan);

        var codeDocument = CreateCodeDocument(input);
        var sourceText = codeDocument.GetSourceText();
        var razorFilePath = "file://C:/path/test.razor";
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);

        var razorCodeActionProviders = Array.Empty<RazorCodeActionProvider>();
        var csharpCodeActionProviders = new CSharpCodeActionProvider[]
        {
            new DefaultCSharpCodeActionProvider()
        };
        var htmlCodeActionProviders = Array.Empty<HtmlCodeActionProvider>();

        var endpoint = new CodeActionEndpoint(DocumentMappingService, razorCodeActionProviders, csharpCodeActionProviders, htmlCodeActionProviders, LanguageServer, LanguageServerFeatureOptions);

        // Call GetRegistration, so the endpoint knows we support resolve
        endpoint.GetRegistration(new VSInternalClientCapabilities
        {
            TextDocument = new TextDocumentClientCapabilities
            {
                CodeAction = new CodeActionSetting
                {
                    ResolveSupport = new CodeActionResolveSupportSetting()
                }
            }
        });

        var @params = new VSCodeActionParams
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = uri },
            Range = textSpan.AsRange(sourceText),
            Context = new VSInternalCodeActionContext()
        };

        var requestContext = new RazorRequestContext(documentContext, Logger, null!);

        var result = await endpoint.HandleRequestAsync(@params, requestContext, DisposalToken);

        Assert.NotNull(result);
        if (result.Length == 0)
        {
            Assert.Fail("Was not offered any code actions.");
        }

        var codeActionToRun = (RazorVSInternalCodeAction)result.FirstOrDefault(e => ((RazorVSInternalCodeAction)e.Value!).Name == codeAction);
        if (codeActionToRun is null)
        {
            Assert.Fail($"Could not find code action '{codeAction}' in list:\n\n{string.Join(Environment.NewLine, result.Select(e => ((RazorVSInternalCodeAction)e.Value!).Name))}");
        }

        var formattingService = await TestRazorFormattingService.CreateWithFullSupportAsync();

        var razorCodeActionResolvers = Array.Empty<RazorCodeActionResolver>();
        var csharpCodeActionResolvers = new CSharpCodeActionResolver[]
        {
            new DefaultCSharpCodeActionResolver(DocumentContextFactory, LanguageServer, formattingService)
        };
        var htmlCodeActionResolvers = Array.Empty<HtmlCodeActionResolver>();

        var resolveEndpoint = new CodeActionResolutionEndpoint(razorCodeActionResolvers, csharpCodeActionResolvers, htmlCodeActionResolvers, LoggerFactory);

        var resolveResult = await resolveEndpoint.HandleRequestAsync(codeActionToRun, requestContext, DisposalToken);

        Assert.NotNull(resolveResult.Edit);

        var workspaceEdit = resolveResult.Edit;
        Assert.True(workspaceEdit.TryGetDocumentChanges(out var changes));

        var edits = new List<TextChange>();
        foreach (var change in changes)
        {
            edits.AddRange(change.Edits.Select(e => e.AsTextChange(sourceText)));
        }

        var actual = sourceText.WithChanges(edits).ToString();
        new XUnitVerifier().EqualOrDiff(expected, actual);
    }
}
