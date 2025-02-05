﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost.CodeActions;

public class CSharpCodeActionTests(FuseTestContext context, ITestOutputHelper testOutputHelper) : CohostCodeActionsEndpointTestBase(context, testOutputHelper)
{
    [FuseFact]
    public async Task GenerateConstructor()
    {
        var input = """

            <div></div>

            @code
            {
                public class [||]Goo
                {
                }
            }

            """;

        var expected = """
            
            <div></div>
            
            @code
            {
                public class Goo
                {
                    public Goo()
                    {
                    }
                }
            }

            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.GenerateConstructorFromMembers);
    }

    [FuseFact]
    public async Task UseExpressionBodiedMember()
    {
        var input = """
            @using System.Linq

            <div></div>

            @code
            {
                [|{|selection:|}void M(string[] args)|]
                {
                    args.ToString();
                }
            }

            """;

        var expected = """
            @using System.Linq

            <div></div>
            
            @code
            {
                void M(string[] args) => args.ToString();
            }

            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.UseExpressionBody);
    }

    [FuseFact]
    public async Task IntroduceLocal()
    {
        var input = """
            @using System.Linq

            <div></div>

            @code
            {
                void M(string[] args)
                {
                    if ([|args.First()|].Length > 0)
                    {
                    }
                    if (args.First().Length > 0)
                    {
                    }
                }
            }

            """;

        var expected = """
            @using System.Linq

            <div></div>
            
            @code
            {
                void M(string[] args)
                {
                    string v = args.First();
                    if (v.Length > 0)
                    {
                    }
                    if (args.First().Length > 0)
                    {
                    }
                }
            }

            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.IntroduceVariable);
    }

    [FuseFact]
    public async Task IntroduceLocal_All()
    {
        var input = """
            @using System.Linq

            <div></div>

            @code
            {
                void M(string[] args)
                {
                    if ([|args.First()|].Length > 0)
                    {
                    }
                    if (args.First().Length > 0)
                    {
                    }
                }
            }

            """;

        var expected = """
            @using System.Linq

            <div></div>
            
            @code
            {
                void M(string[] args)
                {
                    string v = args.First();
                    if (v.Length > 0)
                    {
                    }
                    if (v.Length > 0)
                    {
                    }
                }
            }

            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.IntroduceVariable, childActionIndex: 1);
    }

    [FuseFact]
    public async Task ConvertConcatenationToInterpolatedString_CSharpStatement()
    {
        var input = """
            @{
                var x = "he[||]l" + "lo" + Environment.NewLine + "world";
            }
            """;

        var expected = """
            @{
                var x = $"hello{Environment.NewLine}world";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertConcatenationToInterpolatedString);
    }

    [FuseFact]
    public async Task ConvertConcatenationToInterpolatedString_ExplicitExpression()
    {
        var input = """
            @("he[||]l" + "lo" + Environment.NewLine + "world")
            """;

        var expected = """
            @($"hello{Environment.NewLine}world")
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertConcatenationToInterpolatedString);
    }

    [FuseFact]
    public async Task ConvertConcatenationToInterpolatedString_CodeBlock()
    {
        var input = """
            @code
            {
                private string _x = "he[||]l" + "lo" + Environment.NewLine + "world";
            }
            """;

        var expected = """
            @code
            {
                private string _x = $"hello{Environment.NewLine}world";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertConcatenationToInterpolatedString);
    }

    [FuseFact]
    public async Task ConvertBetweenRegularAndVerbatimInterpolatedString_CodeBlock()
    {
        var input = """
            @code
            {
                private string _x = $@"h[||]ello world";
            }
            """;

        var expected = """
            @code
            {
                private string _x = $"hello world";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertBetweenRegularAndVerbatimInterpolatedString);
    }

    [FuseFact]
    public async Task ConvertBetweenRegularAndVerbatimInterpolatedString_CodeBlock2()
    {
        var input = """
            @code
            {
                private string _x = $"h[||]ello\\nworld";
            }
            """;

        var expected = """
            @code
            {
                private string _x = $@"hello\nworld";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertBetweenRegularAndVerbatimInterpolatedString);
    }

    [FuseFact]
    public async Task ConvertBetweenRegularAndVerbatimString_CodeBlock()
    {
        var input = """
            @code
            {
                private string _x = @"h[||]ello world";
            }
            """;

        var expected = """
            @code
            {
                private string _x = "hello world";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertBetweenRegularAndVerbatimString);
    }

    [FuseFact]
    public async Task ConvertBetweenRegularAndVerbatimString_CodeBlock2()
    {
        var input = """
            @code
            {
                private string _x = "h[||]ello\\nworld";
            }
            """;

        var expected = """
            @code
            {
                private string _x = @"hello\nworld";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertBetweenRegularAndVerbatimString);
    }

    [FuseFact]
    public async Task ConvertPlaceholderToInterpolatedString_CodeBlock()
    {
        var input = """
            @code
            {
                private string _x = [|string.Format("hello{0}world", Environment.NewLine)|];
            }
            """;

        var expected = """
            @code
            {
                private string _x = $"hello{Environment.NewLine}world";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertPlaceholderToInterpolatedString);
    }

    [FuseFact]
    public async Task ConvertToInterpolatedString_CodeBlock()
    {
        var input = """
            @code
            {
                private string _x = [||]"hello {";
            }
            """;

        var expected = """
            @code
            {
                private string _x = $"hello {{";
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.ConvertToInterpolatedString);
    }

    [FuseFact]
    public async Task AddDebuggerDisplay()
    {
        var input = """
            @code {
                class Goo[||]
                {
                    
                }
            }
            """;

        var expected = """
            @using System.Diagnostics
            @code {
                [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
                class Goo
                {
                    private string GetDebuggerDisplay()
                    {
                        return ToString();
                    }
                }
            }
            """;

        await VerifyCodeActionAsync(input, expected, RazorPredefinedCodeRefactoringProviderNames.AddDebuggerDisplay);
    }
}
