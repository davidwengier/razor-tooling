﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation;

internal class TextEditResponseRewriter : DelegatedCompletionResponseRewriter
{
    public override int Order => ExecutionBehaviorOrder.ChangesCompletionItems;

    public override async Task<VSInternalCompletionList> RewriteAsync(
        VSInternalCompletionList completionList,
        int hostDocumentIndex,
        DocumentContext hostDocumentContext,
        DelegatedCompletionParams delegatedParameters,
        CancellationToken cancellationToken)
    {
        if (delegatedParameters.ProjectedKind != RazorLanguageKind.CSharp)
        {
            return completionList;
        }

        var sourceText = await hostDocumentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);

        var hostDocumentPosition = sourceText.GetPosition(hostDocumentIndex);
        completionList = TranslateTextEdits(hostDocumentPosition, delegatedParameters.ProjectedPosition, completionList);

        if (completionList.ItemDefaults?.EditRange is { } editRange)
        {
            if (editRange.TryGetFirst(out var range))
            {
                completionList.ItemDefaults.EditRange = TranslateRange(hostDocumentPosition, delegatedParameters.ProjectedPosition, range);
            }
            else if (editRange.TryGetSecond(out var insertReplaceRange))
            {
                insertReplaceRange.Insert = TranslateRange(hostDocumentPosition, delegatedParameters.ProjectedPosition, insertReplaceRange.Insert);
                insertReplaceRange.Replace = TranslateRange(hostDocumentPosition, delegatedParameters.ProjectedPosition, insertReplaceRange.Replace);
            }
        }

        return completionList;
    }

    private static VSInternalCompletionList TranslateTextEdits(
        Position hostDocumentPosition,
        Position projectedPosition,
        VSInternalCompletionList completionList)
    {
        // The TextEdit positions returned to us from the C#/HTML language servers are positions correlating to the virtual document.
        // We need to translate these positions to apply to the Razor document instead. Performance is a big concern here, so we want to
        // make the logic as simple as possible, i.e. no asynchronous calls.
        // The current logic takes the approach of assuming the original request's position (Razor doc) correlates directly to the positions
        // returned by the C#/HTML language servers. We use this assumption (+ math) to map from the virtual (projected) doc positions ->
        // Razor doc positions.

        foreach (var item in completionList.Items)
        {
            if (item.TextEdit is { } edit)
            {
                if (edit.TryGetFirst(out var textEdit))
                {
                    var translatedRange = TranslateRange(hostDocumentPosition, projectedPosition, textEdit.Range);
                    textEdit.Range = translatedRange;
                }
                else if (edit.TryGetSecond(out var insertReplaceEdit))
                {
                    insertReplaceEdit.Insert = TranslateRange(hostDocumentPosition, projectedPosition, insertReplaceEdit.Insert);
                    insertReplaceEdit.Replace = TranslateRange(hostDocumentPosition, projectedPosition, insertReplaceEdit.Replace);
                }
            }
            else if (item.AdditionalTextEdits is not null)
            {
                // Additional text edits should typically only be provided at resolve time. We don't support them in the normal completion flow.
                item.AdditionalTextEdits = null;
            }
        }

        return completionList;
    }

    private static Range TranslateRange(Position hostDocumentPosition, Position projectedPosition, Range textEditRange)
    {
        var offset = projectedPosition.Character - hostDocumentPosition.Character;

        var translatedStartPosition = TranslatePosition(offset, hostDocumentPosition, textEditRange.Start);
        var translatedEndPosition = TranslatePosition(offset, hostDocumentPosition, textEditRange.End);

        return VsLspFactory.CreateRange(translatedStartPosition, translatedEndPosition);

        static Position TranslatePosition(int offset, Position hostDocumentPosition, Position editPosition)
        {
            var translatedCharacter = editPosition.Character - offset;

            // Note: If this completion handler ever expands to deal with multi-line TextEdits, this logic will likely need to change since
            // it assumes we're only dealing with single-line TextEdits.
            return VsLspFactory.CreatePosition(hostDocumentPosition.Line, translatedCharacter);
        }
    }
}
