// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

internal class FormattingSpan
{
    public FormattingSpan(
        TextSpan span,
        TextSpan blockSpan,
        FormattingSpanKind spanKind,
        FormattingBlockKind blockKind,
        int razorIndentationLevel,
        int htmlIndentationLevel,
        bool isInClassBody,
        int componentLambdaNestingLevel
#if DEBUG
        , string debugOnlyText
#endif
        )
    {
        Span = span;
        BlockSpan = blockSpan;
        Kind = spanKind;
        BlockKind = blockKind;
        RazorIndentationLevel = razorIndentationLevel;
        HtmlIndentationLevel = htmlIndentationLevel;
        IsInClassBody = isInClassBody;
        ComponentLambdaNestingLevel = componentLambdaNestingLevel;
#if DEBUG
        DebugOnlyTest = debugOnlyText;
#endif
    }

    public TextSpan Span { get; }

    public TextSpan BlockSpan { get; }

    public FormattingBlockKind BlockKind { get; }

    public FormattingSpanKind Kind { get; }

    public int RazorIndentationLevel { get; }

    public int HtmlIndentationLevel { get; }

    public int IndentationLevel => RazorIndentationLevel + HtmlIndentationLevel;

    public bool IsInClassBody { get; }

    public int ComponentLambdaNestingLevel { get; }

#if DEBUG
    public string DebugOnlyTest { get; }
#endif


    public int MinCSharpIndentLevel
    {
        get
        {
            var baseIndent = IsInClassBody ? 2 : 3;

            return baseIndent + ComponentLambdaNestingLevel;
        }
    }
}
