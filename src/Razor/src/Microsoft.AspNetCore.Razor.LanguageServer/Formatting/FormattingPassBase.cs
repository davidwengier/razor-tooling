// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.TextDifferencing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

internal abstract class FormattingPassBase : IFormattingPass
{
    protected static readonly int DefaultOrder = 1000;

    public FormattingPassBase(
        RazorDocumentMappingService documentMappingService,
        ClientNotifierServiceBase server)
    {
        if (documentMappingService is null)
        {
            throw new ArgumentNullException(nameof(documentMappingService));
        }

        if (server is null)
        {
            throw new ArgumentNullException(nameof(server));
        }

        DocumentMappingService = documentMappingService;
    }

    public abstract bool IsValidationPass { get; }

    public virtual int Order => DefaultOrder;

    protected RazorDocumentMappingService DocumentMappingService { get; }

    public abstract Task<FormattingResult> ExecuteAsync(FormattingContext context, FormattingResult result, CancellationToken cancellationToken);

    protected TextEdit[] NormalizeTextEdits(SourceText originalText, TextEdit[] edits, out SourceText originalTextWithChanges, bool lineDiff = false)
    {
        var changes = edits.Select(e => e.AsTextChange(originalText));
        originalTextWithChanges = originalText.WithChanges(changes);
        var cleanChanges = SourceTextDiffer.GetMinimalTextChanges(originalText, originalTextWithChanges, lineDiff ? DiffKind.Line : DiffKind.Char);
        var cleanEdits = cleanChanges.Select(c => c.AsTextEdit(originalText)).ToArray();
        return cleanEdits;
    }

    protected TextEdit[] RemapTextEdits(IRazorGeneratedDocument generatedDocument, TextEdit[] projectedTextEdits, RazorLanguageKind projectedKind)
    {
        if (generatedDocument is null)
        {
            throw new ArgumentNullException(nameof(generatedDocument));
        }

        if (projectedTextEdits is null)
        {
            throw new ArgumentNullException(nameof(projectedTextEdits));
        }

        if (projectedKind == RazorLanguageKind.Razor)
        {
            // Non C# projections map directly to Razor. No need to remap.
            return projectedTextEdits;
        }

        if (generatedDocument.CodeDocument?.IsUnsupported() ?? false)
        {
            return Array.Empty<TextEdit>();
        }

        var edits = DocumentMappingService.GetProjectedDocumentEdits(generatedDocument, projectedTextEdits);

        return edits;
    }
}
