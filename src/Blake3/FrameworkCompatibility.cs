// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

#if NETSTANDARD2_0
// attributes compatible with net5.0 only

namespace System.Diagnostics.CodeAnalysis
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    internal sealed class DoesNotReturnAttribute : Attribute {}
}

namespace System.Runtime.InteropServices
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    internal sealed class SuppressGCTransitionAttribute : Attribute {}
}


namespace System.Runtime.CompilerServices
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Constructor | System.AttributeTargets.Event | System.AttributeTargets.Interface | System.AttributeTargets.Method | System.AttributeTargets.Module | System.AttributeTargets.Property | System.AttributeTargets.Struct, Inherited = false)]
    internal sealed class SkipLocalsInitAttribute : Attribute {}
}
#endif