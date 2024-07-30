﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Roslyn.LanguageServer.Protocol;

internal static partial class LspExtensions
{
    public static VSProjectContext? GetProjectContext(this TextDocumentIdentifier textDocumentIdentifier)
        => textDocumentIdentifier is VSTextDocumentIdentifier vsIdentifier
            ? vsIdentifier.ProjectContext
            : null;
}
