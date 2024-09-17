﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

extern alias RLSP;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.GoToDefinition;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using DefinitionResult = RLSP::Roslyn.LanguageServer.Protocol.SumType<
    RLSP::Roslyn.LanguageServer.Protocol.VSInternalLocation,
    RLSP::Roslyn.LanguageServer.Protocol.VSInternalLocation[],
    RLSP::Roslyn.LanguageServer.Protocol.DocumentLink[]>;
using SyntaxKind = Microsoft.AspNetCore.Razor.Language.SyntaxKind;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Definition;

[RazorLanguageServerEndpoint(Methods.TextDocumentDefinitionName)]
internal sealed class DefinitionEndpoint(
    IRazorComponentDefinitionService componentDefinitionService,
    IDocumentMappingService documentMappingService,
    LanguageServerFeatureOptions languageServerFeatureOptions,
    IClientConnection clientConnection,
    ILoggerFactory loggerFactory)
    : AbstractRazorDelegatingEndpoint<TextDocumentPositionParams, DefinitionResult?>(
        languageServerFeatureOptions,
        documentMappingService,
        clientConnection,
        loggerFactory.GetOrCreateLogger<DefinitionEndpoint>()), ICapabilitiesProvider
{
    private readonly IRazorComponentDefinitionService _componentDefinitionService = componentDefinitionService;
    private readonly IDocumentMappingService _documentMappingService = documentMappingService;

    protected override bool PreferCSharpOverHtmlIfPossible => true;

    protected override IDocumentPositionInfoStrategy DocumentPositionInfoStrategy => PreferAttributeNameDocumentPositionInfoStrategy.Instance;

    protected override string CustomMessageTarget => CustomMessageNames.RazorDefinitionEndpointName;

    public void ApplyCapabilities(VSInternalServerCapabilities serverCapabilities, VSInternalClientCapabilities clientCapabilities)
    {
        serverCapabilities.DefinitionProvider = new DefinitionOptions();
    }

    protected async override Task<DefinitionResult?> TryHandleAsync(
        TextDocumentPositionParams request,
        RazorRequestContext requestContext,
        DocumentPositionInfo positionInfo,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation($"Starting go-to-def endpoint request.");

        var documentContext = requestContext.DocumentContext;
        if (documentContext is null)
        {
            return null;
        }

        // If single server support is on, then we ignore attributes, as they are better handled by delegating to Roslyn
        return await _componentDefinitionService
            .GetDefinitionAsync(documentContext.Snapshot, positionInfo, ignoreAttributes: SingleServerSupport, cancellationToken)
            .ConfigureAwait(false);
    }

    protected override Task<IDelegatedParams?> CreateDelegatedParamsAsync(
        TextDocumentPositionParams request,
        RazorRequestContext requestContext,
        DocumentPositionInfo positionInfo,
        CancellationToken cancellationToken)
    {
        var documentContext = requestContext.DocumentContext;
        if (documentContext is null)
        {
            return SpecializedTasks.Null<IDelegatedParams>();
        }

        return Task.FromResult<IDelegatedParams?>(new DelegatedPositionParams(
            documentContext.GetTextDocumentIdentifierAndVersion(),
            positionInfo.Position,
            positionInfo.LanguageKind));
    }

    protected async override Task<DefinitionResult?> HandleDelegatedResponseAsync(
        DefinitionResult? response,
        TextDocumentPositionParams originalRequest,
        RazorRequestContext requestContext,
        DocumentPositionInfo positionInfo,
        CancellationToken cancellationToken)
    {
        if (response is not DefinitionResult result)
        {
            return null;
        }

        if (result.TryGetFirst(out var location))
        {
            (location.Uri, location.Range) = await _documentMappingService.MapToHostDocumentUriAndRangeAsync(location.Uri, location.Range, cancellationToken).ConfigureAwait(false);
        }
        else if (result.TryGetSecond(out var locations))
        {
            foreach (var loc in locations)
            {
                (loc.Uri, loc.Range) = await _documentMappingService.MapToHostDocumentUriAndRangeAsync(loc.Uri, loc.Range, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (result.TryGetThird(out var links))
        {
            foreach (var link in links)
            {
                if (link.Target is not null)
                {
                    (link.Target, link.Range) = await _documentMappingService.MapToHostDocumentUriAndRangeAsync(link.Target, link.Range, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return result;
    }
}
