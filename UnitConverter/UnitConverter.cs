// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace UnitConverter;

[Guid("b36eeab6-8289-4174-bc88-83a8763e6da3")]
public sealed partial class UnitConverter : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;

    private readonly UnitConverterCommandsProvider _provider = new();

    public UnitConverter(ManualResetEvent extensionDisposedEvent)
    {
        this._extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null,
        };
    }

    public void Dispose() => this._extensionDisposedEvent.Set();
}
