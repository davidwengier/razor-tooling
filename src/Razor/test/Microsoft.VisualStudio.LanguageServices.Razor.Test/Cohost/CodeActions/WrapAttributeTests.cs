﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost.CodeActions;

public class WrapAttributeTests(FuseTestContext context, ITestOutputHelper testOutputHelper) : CohostCodeActionsEndpointTestBase(context, testOutputHelper)
{
    [FuseFact]
    public async Task WrapAttributes()
    {
        await VerifyCodeActionAsync(
            input: """
                <div>
                    <div [||]bar="Baz" Zip="Zap" checked @onclick="foo" Pop="Pap">
                        <div></div>
                    </div>
                </div>
                """,
            expected: """
                <div>
                    <div bar="Baz"
                         Zip="Zap"
                         checked
                         @onclick="foo"
                         Pop="Pap">
                        <div></div>
                    </div>
                </div>
                """,
            codeActionName: LanguageServerConstants.CodeActions.WrapAttributes);
    }

    [FuseFact]
    public async Task Component()
    {
        await VerifyCodeActionAsync(
            input: """
                <div>
                    <EditForm [||]bar="Baz" Zip="Zap" checked @onclick="foo" Pop="Pap" />
                </div>
                """,
            expected: """
                <div>
                    <EditForm bar="Baz"
                              Zip="Zap"
                              checked
                              @onclick="foo"
                              Pop="Pap" />
                </div>
                """,
            codeActionName: LanguageServerConstants.CodeActions.WrapAttributes);
    }

    [FuseFact]
    public async Task Whitespace()
    {
        await VerifyCodeActionAsync(
            input: """
                <div>
                    <Foo Bar="Baz"        Zip="Za[||]p"               Pop="Pap" />
                </div>
                """,
            expected: """
                <div>
                    <Foo Bar="Baz"
                         Zip="Zap"
                         Pop="Pap" />
                </div>
                """,
            codeActionName: LanguageServerConstants.CodeActions.WrapAttributes);
    }

    [FuseFact]
    public async Task MultiLine()
    {
        await VerifyCodeActionAsync(
            input: """
                <div>
                    <Foo Bar="Baz" Zip="Za[||]p"
                         Pop="Pap" />
                </div>
                """,
            expected: null,
            codeActionName: LanguageServerConstants.CodeActions.WrapAttributes);
    }

    [FuseFact]
    public async Task OneAttribute()
    {
        await VerifyCodeActionAsync(
            input: """
                <div>
                    <Foo Zip="Za[||]p" />
                </div>
                """,
            expected: null,
            codeActionName: LanguageServerConstants.CodeActions.WrapAttributes);
    }
}
