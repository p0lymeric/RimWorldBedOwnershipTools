// https://github.com/Whathecode/NET-Core-Library-Extension/blob/ba69ef355557bf0abc13a188c74557fbcb99ffba/src/Whathecode.System/Reflection/CastType.cs

// .NET Core Library Extension
// Copyright (c) 2016 Steven Jeuris
// The library is distributed under the terms of the MIT license (http://opensource.org/licenses/mit-license).
// More information can be found in "LICENSE"

namespace BedOwnershipTools.Whathecode.System.Reflection
{
    public enum CastType
    {
        /// <summary>
        /// Only consider implicit conversions.
        /// </summary>
        Implicit,
        /// <summary>
        /// Consider all possible conversions within the same hierarchy which are possible using an explicit cast.
        /// </summary>
        SameHierarchy
    }
}
