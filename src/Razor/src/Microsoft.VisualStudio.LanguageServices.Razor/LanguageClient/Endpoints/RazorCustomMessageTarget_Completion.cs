﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.Completion;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.Razor.Snippets;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Endpoints;

internal partial class RazorCustomMessageTarget
{
    // Called by the Razor Language Server to provide inline completions from the platform.
    [JsonRpcMethod(CustomMessageNames.RazorInlineCompletionEndpoint, UseSingleObjectParameterDeserialization = true)]
    public async Task<VSInternalInlineCompletionList?> ProvideInlineCompletionAsync(RazorInlineCompletionRequest inlineCompletionParams, CancellationToken cancellationToken)
    {
        if (inlineCompletionParams is null)
        {
            throw new ArgumentNullException(nameof(inlineCompletionParams));
        }

        var hostDocumentUri = inlineCompletionParams.TextDocument.Uri;
        if (!_documentManager.TryGetDocument(hostDocumentUri, out var documentSnapshot))
        {
            return null;
        }

        // TODO: Support multiple C# documents per Razor document.
        if (!documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpDoc))
        {
            return null;
        }

        var csharpRequest = new VSInternalInlineCompletionRequest
        {
            Context = inlineCompletionParams.Context,
            Position = inlineCompletionParams.Position,
            TextDocument = inlineCompletionParams.TextDocument.WithUri(csharpDoc.Uri),
            Options = inlineCompletionParams.Options,
        };

        var textBuffer = csharpDoc.Snapshot.TextBuffer;
        var request = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalInlineCompletionRequest, VSInternalInlineCompletionList?>(
            textBuffer,
            VSInternalMethods.TextDocumentInlineCompletionName,
            RazorLSPConstants.RazorCSharpLanguageServerName,
            csharpRequest,
            cancellationToken).ConfigureAwait(false);

        return request?.Response;
    }

    [JsonRpcMethod(LanguageServerConstants.RazorCompletionEndpointName, UseSingleObjectParameterDeserialization = true)]
    public async Task<VSInternalCompletionList?> ProvideCompletionsAsync(
        DelegatedCompletionParams request,
        CancellationToken cancellationToken)
    {
        var hostDocumentUri = request.Identifier.TextDocumentIdentifier.Uri;

        string languageServerName;
        bool synchronized;
        VirtualDocumentSnapshot virtualDocumentSnapshot;
        if (request.ProjectedKind == RazorLanguageKind.Html)
        {
            (synchronized, virtualDocumentSnapshot) = await TrySynchronizeVirtualDocumentAsync<HtmlVirtualDocumentSnapshot>(
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken);
            languageServerName = RazorLSPConstants.HtmlLanguageServerName;
        }
        else if (request.ProjectedKind == RazorLanguageKind.CSharp)
        {
            (synchronized, virtualDocumentSnapshot) = await TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken);
            languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
        }
        else
        {
            Debug.Fail("Unexpected RazorLanguageKind. This shouldn't really happen in a real scenario.");
            return null;
        }

        if (!synchronized || virtualDocumentSnapshot is null)
        {
            return null;
        }

        var completionParams = new CompletionParams()
        {
            Context = request.Context,
            Position = request.ProjectedPosition,
            TextDocument = request.Identifier.TextDocumentIdentifier.WithUri(virtualDocumentSnapshot.Uri),
        };

        var continueOnCapturedContext = false;
        var provisionalTextEdit = request.ProvisionalTextEdit;
        if (provisionalTextEdit is not null)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var provisionalChange = new VisualStudioTextChange(
                provisionalTextEdit.Range.Start.Line,
                provisionalTextEdit.Range.Start.Character,
                provisionalTextEdit.Range.End.Line,
                provisionalTextEdit.Range.End.Character,
                virtualDocumentSnapshot.Snapshot,
                provisionalTextEdit.NewText);
            UpdateVirtualDocument(provisionalChange, request.ProjectedKind, request.Identifier.Version, hostDocumentUri, virtualDocumentSnapshot.Uri);

            // We want the delegation to continue on the captured context because we're currently on the `main` thread and we need to get back to the
            // main thread in order to update the virtual buffer with the reverted text edit.
            continueOnCapturedContext = true;
        }

        try
        {
            var textBuffer = virtualDocumentSnapshot.Snapshot.TextBuffer;
            var lspMethodName = Methods.TextDocumentCompletion.Name;
            ReinvocationResponse<VSInternalCompletionList?>? response;
            using (_telemetryReporter.TrackLspRequest(lspMethodName, languageServerName, request.CorrelationId))
            {
                response = await _requestInvoker.ReinvokeRequestOnServerAsync<CompletionParams, VSInternalCompletionList?>(
                    textBuffer,
                    lspMethodName,
                    languageServerName,
                    completionParams,
                    cancellationToken).ConfigureAwait(continueOnCapturedContext);
            }

            var completionList = response?.Response;
            using var builder = new PooledArrayBuilder<CompletionItem>();

            if (completionList is not null)
            {
                builder.AddRange(completionList.Items);
            }
            else
            {
                completionList = new VSInternalCompletionList()
                {
                    // If we don't get a response from the delegated server, we have to make sure to return an incomplete completion
                    // list. When a user is typing quickly, the delegated request from the first keystroke will fail to synchronize,
                    // so if we return a "complete" list then the query won't re-query us for completion once the typing stops/slows
                    // so we'd only ever return Razor completion items.
                    IsIncomplete = true,
                };
            }

            AddSnippetCompletions(request, ref builder.AsRef());
            completionList.Items = builder.ToArray();

            completionList.Data = JsonHelpers.TryConvertFromJObject(completionList.Data);
            ConvertJsonElementToJObject(completionList);

            return completionList;
        }
        finally
        {
            if (provisionalTextEdit is not null)
            {
                var revertedProvisionalTextEdit = BuildRevertedEdit(provisionalTextEdit);
                var revertedProvisionalChange = new VisualStudioTextChange(
                    revertedProvisionalTextEdit.Range.Start.Line,
                    revertedProvisionalTextEdit.Range.Start.Character,
                    revertedProvisionalTextEdit.Range.End.Line,
                    revertedProvisionalTextEdit.Range.End.Character,
                    virtualDocumentSnapshot.Snapshot,
                    revertedProvisionalTextEdit.NewText);
                UpdateVirtualDocument(revertedProvisionalChange, request.ProjectedKind, request.Identifier.Version, hostDocumentUri, virtualDocumentSnapshot.Uri);
            }
        }
    }

    private void ConvertJsonElementToJObject(VSInternalCompletionList completionList)
    {
        foreach (var item in completionList.Items)
        {
            item.Data = JsonHelpers.TryConvertFromJObject(item.Data);
        }
    }

    private static TextEdit BuildRevertedEdit(TextEdit provisionalTextEdit)
    {
        TextEdit? revertedProvisionalTextEdit;

        var range = provisionalTextEdit.Range;

        if (range.Start == range.End)
        {
            // Insertion
            revertedProvisionalTextEdit = LspFactory.CreateTextEdit(
                range: LspFactory.CreateSingleLineRange(
                    range.Start,
                    length: range.End.Character + provisionalTextEdit.NewText.Length),
                newText: string.Empty);
        }
        else
        {
            // Replace
            revertedProvisionalTextEdit = LspFactory.CreateTextEdit(range, string.Empty);
        }

        return revertedProvisionalTextEdit;
    }

    private void UpdateVirtualDocument(
        VisualStudioTextChange textChange,
        RazorLanguageKind virtualDocumentKind,
        int hostDocumentVersion,
        Uri documentSnapshotUri,
        Uri virtualDocumentUri)
    {
        if (_documentManager is not TrackingLSPDocumentManager trackingDocumentManager)
        {
            return;
        }

        if (virtualDocumentKind == RazorLanguageKind.CSharp)
        {
            trackingDocumentManager.UpdateVirtualDocument<CSharpVirtualDocument>(
                documentSnapshotUri,
                virtualDocumentUri,
                new[] { textChange },
                hostDocumentVersion,
                state: null);
        }
        else if (virtualDocumentKind == RazorLanguageKind.Html)
        {
            trackingDocumentManager.UpdateVirtualDocument<HtmlVirtualDocument>(
                documentSnapshotUri,
                virtualDocumentUri,
                new[] { textChange },
                hostDocumentVersion,
                state: null);
        }
    }

    [JsonRpcMethod(LanguageServerConstants.RazorCompletionResolveEndpointName, UseSingleObjectParameterDeserialization = true)]
    public async Task<CompletionItem?> ProvideResolvedCompletionItemAsync(DelegatedCompletionItemResolveParams request, CancellationToken cancellationToken)
    {
        // Check if we're completing a snippet item that we provided
        if (SnippetCompletionData.TryParse(request.CompletionItem.Data, out var snippetCompletionData) &&
            _snippetCache.TryResolveSnippetString(snippetCompletionData) is { } snippetInsertText)
        {
            request.CompletionItem.InsertText = snippetInsertText;
            return request.CompletionItem;
        }

        string languageServerName;
        bool synchronized;
        VirtualDocumentSnapshot virtualDocumentSnapshot;
        if (request.OriginatingKind == RazorLanguageKind.Html)
        {
            (synchronized, virtualDocumentSnapshot) = await TrySynchronizeVirtualDocumentAsync<HtmlVirtualDocumentSnapshot>(
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken);
            languageServerName = RazorLSPConstants.HtmlLanguageServerName;
        }
        else if (request.OriginatingKind == RazorLanguageKind.CSharp)
        {
            // TODO this is a partial workaround to fix prefix completion by avoiding sync (which times out during resolve endpoint) if we are currently at a higher version value
            // this does not fix postfix completion and should be superseded by eventual synchronization fix

            var futureDataSyncResult = TryReturnPossiblyFutureSnapshot<CSharpVirtualDocumentSnapshot>(request.Identifier.Version, request.Identifier.TextDocumentIdentifier);
            if (futureDataSyncResult?.Synchronized == true)
            {
                (synchronized, virtualDocumentSnapshot) = futureDataSyncResult;
            }
            else
            {
                (synchronized, virtualDocumentSnapshot) = await TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                    request.Identifier.Version,
                    request.Identifier.TextDocumentIdentifier,
                    cancellationToken);
            }

            languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
        }
        else
        {
            Debug.Fail("Unexpected RazorLanguageKind. This can't really happen in a real scenario.");
            return null;
        }

        if (!synchronized || virtualDocumentSnapshot is null)
        {
            // Document was not synchronized
            return null;
        }

        var completionResolveParams = request.CompletionItem;

        completionResolveParams.Data = JsonHelpers.TryConvertBackToJObject(completionResolveParams.Data);

        var textBuffer = virtualDocumentSnapshot.Snapshot.TextBuffer;
        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalCompletionItem, CompletionItem?>(
            textBuffer,
            Methods.TextDocumentCompletionResolve.Name,
            languageServerName,
            completionResolveParams,
            cancellationToken).ConfigureAwait(false);

        var item = response?.Response;
        if (item is not null)
        {
            item.Data = JsonHelpers.TryConvertFromJObject(item.Data);
        }

        return item;
    }

    [JsonRpcMethod(LanguageServerConstants.RazorGetFormattingOptionsEndpointName, UseSingleObjectParameterDeserialization = true)]
    public Task<FormattingOptions?> GetFormattingOptionsAsync(TextDocumentIdentifierAndVersion document, CancellationToken _)
    {
        var formattingOptions = _formattingOptionsProvider.GetOptions(document.TextDocumentIdentifier.Uri);

        if (formattingOptions is null)
        {
            return SpecializedTasks.Null<FormattingOptions>();
        }

        var roslynFormattingOptions = new FormattingOptions()
        {
            TabSize = formattingOptions.TabSize,
            InsertSpaces = formattingOptions.InsertSpaces,
            OtherOptions = formattingOptions.OtherOptions,
        };
        return Task.FromResult<FormattingOptions?>(roslynFormattingOptions);
    }

    private void AddSnippetCompletions(DelegatedCompletionParams request, ref PooledArrayBuilder<CompletionItem> builder)
    {
        if (!request.ShouldIncludeSnippets)
        {
            return;
        }

        // Temporary fix: snippets are broken in CSharp. We're investigating
        // but this is very disruptive. This quick fix unblocks things.
        // TODO: Add an option to enable this.
        if (request.ProjectedKind != RazorLanguageKind.Html)
        {
            return;
        }

        // Don't add snippets for deletion of a character
        if (request.Context.InvokeKind == VSInternalCompletionInvokeKind.Deletion)
        {
            return;
        }

        // Don't add snippets if the trigger characters contain whitespace
        if (request.Context.TriggerCharacter is not null
            && request.Context.TriggerCharacter.Contains(' '))
        {
            return;
        }

        var snippets = _snippetCache.GetSnippets(ConvertLanguageKind(request.ProjectedKind));
        if (snippets.IsDefaultOrEmpty)
        {
            return;
        }

        builder.AddRange(snippets
            .Select(s => new CompletionItem()
            {
                Label = s.Shortcut,
                Detail = s.Description,
                InsertTextFormat = InsertTextFormat.Snippet,
                InsertText = s.Shortcut,
                Data = s.CompletionData,
                Kind = CompletionItemKind.Snippet,
                CommitCharacters = []
            }));
    }

    private static SnippetLanguage ConvertLanguageKind(RazorLanguageKind languageKind)
        => languageKind switch
        {
            RazorLanguageKind.CSharp => SnippetLanguage.CSharp,
            RazorLanguageKind.Html => SnippetLanguage.Html,
            RazorLanguageKind.Razor => SnippetLanguage.Razor,
            _ => throw new InvalidOperationException($"Unexpected value {languageKind}")
        };
}
