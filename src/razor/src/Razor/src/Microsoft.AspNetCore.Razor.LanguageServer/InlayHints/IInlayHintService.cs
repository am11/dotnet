﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.InlayHints;

internal interface IInlayHintService
{
    Task<InlayHint[]?> GetInlayHintsAsync(IClientConnection clientConnection, VersionedDocumentContext documentContext, Range range, CancellationToken cancellationToken);

    Task<InlayHint?> ResolveInlayHintAsync(IClientConnection clientConnection, InlayHint inlayHint, CancellationToken cancellationToken);
}
