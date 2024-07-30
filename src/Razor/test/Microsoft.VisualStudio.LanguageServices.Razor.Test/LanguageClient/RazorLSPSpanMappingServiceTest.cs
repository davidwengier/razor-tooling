﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Editor;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.DocumentMapping;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Razor.LanguageClient.DocumentMapping;
using Moq;
using Xunit;
using Xunit.Abstractions;
using LspRange = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.VisualStudio.Razor.LanguageClient;

public class RazorLSPSpanMappingServiceTest : ToolingTestBase
{
    private readonly Uri _mockDocumentUri = new("C://project/path/document.razor");

    private static readonly string s_mockGeneratedContent = """
            Hello
             This is the source text in the generated C# file.
             This is some more sample text for demo purposes.
            """;
    private static readonly string s_mockRazorContent = """
            Hello
             This is the
             source text
             in the generated C# file.
             This is some more sample text for demo purposes.
            """;

    private readonly SourceText _sourceTextGenerated;
    private readonly SourceText _sourceTextRazor;

    public RazorLSPSpanMappingServiceTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _sourceTextGenerated = SourceText.From(s_mockGeneratedContent);
        _sourceTextRazor = SourceText.From(s_mockRazorContent);
    }

    [Fact]
    public async Task MapSpans_WithinRange_ReturnsMapping()
    {
        // Arrange
        var called = false;

        var textSpan = new TextSpan(1, 10);
        var spans = new TextSpan[] { textSpan };

        var documentSnapshot = new Mock<LSPDocumentSnapshot>(MockBehavior.Strict);
        documentSnapshot.SetupGet(doc => doc.Uri).Returns(_mockDocumentUri);

        var textSnapshot = new StringTextSnapshot(s_mockGeneratedContent, 1);

        var textSpanAsRange = _sourceTextGenerated.GetRange(textSpan);
        var mappedRange = LspFactory.CreateSingleLineRange(2, character: 1, length: 10);

        var documentMappingProvider = new Mock<LSPDocumentMappingProvider>(MockBehavior.Strict);
        var mappingResult = new RazorMapToDocumentRangesResponse()
        {
            Ranges = [mappedRange]
        };
        documentMappingProvider.Setup(dmp => dmp.MapToDocumentRangesAsync(It.IsAny<RazorLanguageKind>(), It.IsAny<Uri>(), It.IsAny<LspRange[]>(), It.IsAny<CancellationToken>()))
            .Callback<RazorLanguageKind, Uri, LspRange[], CancellationToken>((languageKind, uri, ranges, ct) =>
            {
                Assert.Equal(RazorLanguageKind.CSharp, languageKind);
                Assert.Equal(_mockDocumentUri, uri);
                Assert.Single(ranges, textSpanAsRange);
                called = true;
            })
            .ReturnsAsync(mappingResult);

        var service = new RazorLSPSpanMappingService(documentMappingProvider.Object, documentSnapshot.Object, textSnapshot);

        var expectedSpan = _sourceTextRazor.GetTextSpan(mappedRange);
        var expectedLinePosition = _sourceTextRazor.GetLinePositionSpan(expectedSpan);
        var expectedFilePath = _mockDocumentUri.LocalPath;
        var expectedResult = (expectedFilePath, expectedLinePosition, expectedSpan);

        // Act
        var result = await service.MapSpansAsyncTest(spans, _sourceTextGenerated, _sourceTextRazor);

        // Assert
        Assert.True(called);
        Assert.Single(result, expectedResult);
    }

    [Fact]
    public async Task MapSpans_OutsideRange_ReturnsEmpty()
    {
        // Arrange
        var called = false;

        var textSpan = new TextSpan(10, 10);
        var spans = new TextSpan[] { textSpan };

        var documentSnapshot = new Mock<LSPDocumentSnapshot>(MockBehavior.Strict);
        documentSnapshot.SetupGet(doc => doc.Uri).Returns(_mockDocumentUri);

        var textSnapshot = new StringTextSnapshot(s_mockGeneratedContent, 1);

        var textSpanAsRange = _sourceTextGenerated.GetRange(textSpan);

        var documentMappingProvider = new Mock<LSPDocumentMappingProvider>(MockBehavior.Strict);
        documentMappingProvider.Setup(dmp => dmp.MapToDocumentRangesAsync(It.IsAny<RazorLanguageKind>(), It.IsAny<Uri>(), It.IsAny<LspRange[]>(), It.IsAny<CancellationToken>()))
            .Callback<RazorLanguageKind, Uri, LspRange[], CancellationToken>((languageKind, uri, ranges, ct) =>
            {
                Assert.Equal(RazorLanguageKind.CSharp, languageKind);
                Assert.Equal(_mockDocumentUri, uri);
                Assert.Single(ranges, textSpanAsRange);
                called = true;
            })
            .ReturnsAsync(value: null);

        var service = new RazorLSPSpanMappingService(documentMappingProvider.Object, documentSnapshot.Object, textSnapshot);

        // Act
        var result = await service.MapSpansAsyncTest(spans, _sourceTextGenerated, _sourceTextRazor);

        // Assert
        Assert.True(called);
        Assert.Empty(result);
    }

    [Fact]
    public void MapSpans_GetMappedSpanResults_MappingErrorReturnsDefaultMappedSpan()
    {
        // Arrange
        var sourceTextRazor = SourceText.From("");
        var response = new RazorMapToDocumentRangesResponse { Ranges = new LspRange[] { LspFactory.UndefinedRange } };

        // Act
        var results = RazorLSPSpanMappingService.GetMappedSpanResults(_mockDocumentUri.LocalPath, sourceTextRazor, response);

        // Assert
        Assert.True(results.Single().IsDefault);
    }
}
