﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Linq;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

public class RazorFormattingServiceTest(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    [Fact]
    public void MergeEdits_ReturnsSingleEditAsExpected()
    {
        // Arrange
        var source = @"
@code {
public class Foo{}
}
";
        var sourceText = SourceText.From(source);
        var edits = new[]
        {
            LspFactory.CreateTextEdit(LspFactory.CreateSingleLineRange(line: 2, character: 13, length: 3), "Bar"),
            LspFactory.CreateTextEdit(2, 0, "    ")
        };

        // Act
        var collapsedEdit = RazorFormattingService.MergeEdits(edits, sourceText);

        // Assert
        var multiEditChange = sourceText.WithChanges(edits.Select(sourceText.GetTextChange));
        var singleEditChange = sourceText.WithChanges(sourceText.GetTextChange(collapsedEdit));

        Assert.Equal(multiEditChange.ToString(), singleEditChange.ToString());
    }
}
