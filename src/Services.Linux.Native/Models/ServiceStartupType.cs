// Copyright (c) peppekerstens.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.Commands
{
    public enum ServiceStartupType
    {
        InvalidValue = -1,
        Automatic = 2,
        Manual = 3,
        Disabled = 4,
        AutomaticDelayedStart = 10
    }
}
