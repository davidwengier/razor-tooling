// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.MapCode;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;

internal sealed class LspMapCodeService(
    IDocumentMappingService documentMappingService,
    IDocumentContextFactory documentContextFactory,
    IClientConnection clientConnection) : AbstractMapCodeService(documentMappingService)
{
    private readonly IDocumentMappingService _documentMappingService = documentMappingService;
    private readonly IDocumentContextFactory _documentContextFactory = documentContextFactory;
    private readonly IClientConnection _clientConnection = clientConnection;

    protected override bool TryCreateDocumentContext(Uri uri, [NotNullWhen(true)] out DocumentContext? documentContext)
        => _documentContextFactory.TryCreate(uri, out documentContext);

    protected override Task<(Uri MappedDocumentUri, LinePositionSpan MappedRange)> MapToHostDocumentUriAndRangeAsync(Uri generatedDocumentUri, LinePositionSpan generatedDocumentRange, CancellationToken cancellationToken)
        => _documentMappingService.MapToHostDocumentUriAndRangeAsync(generatedDocumentUri, generatedDocumentRange, cancellationToken);

    protected override async Task<WorkspaceEdit?> TryGetCSharpMapCodeEditsAsync(TextDocumentIdentifierAndVersion textDocumentIdentifier, Guid mapCodeCorrelationId, SyntaxNode nodeToMap, Location[][] focusLocations, CancellationToken cancellationToken)
    {
        var delegatedRequest = new DelegatedMapCodeParams(
            textDocumentIdentifier,
            RazorLanguageKind.CSharp,
            mapCodeCorrelationId,
            [nodeToMap.ToFullString()],
            FocusLocations: focusLocations);

        try
        {
            return await _clientConnection.SendRequestAsync<DelegatedMapCodeParams, WorkspaceEdit?>(
                CustomMessageNames.RazorMapCodeEndpoint,
                delegatedRequest,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // C# hasn't implemented + merged their C# code mapper yet.
        }

        return null;
    }
}
