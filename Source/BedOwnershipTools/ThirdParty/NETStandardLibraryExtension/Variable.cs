// https://github.com/Whathecode/NET-Standard-Library-Extension/blob/1f296b0ff7f44eedadf39854e3c94e6002c5775a/src/Whathecode.System/Variable.cs

// .NET Standard Library Extension
// Copyright (c) 2016 Steven Jeuris
// The library is distributed under the terms of the MIT license (http://opensource.org/licenses/mit-license).
// More information can be found in "LICENSE"

namespace BedOwnershipTools.Whathecode.System
{
    /// <summary>
    /// Helper class to do common operations on variables.
    /// </summary>
    public static class Variable
    {
        /// <summary>
        /// Swaps the value of one variable with another.
        /// </summary>
        /// <typeparam name = "T">The type of the variables.</typeparam>
        /// <param name = "first">The first value.</param>
        /// <param name = "second">The second value.</param>
        public static void Swap<T>( ref T first, ref T second )
        {
            T intermediate = first;
            first = second;
            second = intermediate;
        }
    }
}
