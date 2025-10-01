using System;
using System.Collections.Generic;
using System.Linq;

// https://github.com/Whathecode/NET-Standard-Library-Extension/blob/1f296b0ff7f44eedadf39854e3c94e6002c5775a/src/Whathecode.System/Linq/Extensions.IEnumerable.cs

// .NET Standard Library Extension
// Copyright (c) 2016 Steven Jeuris
// The library is distributed under the terms of the MIT license (http://opensource.org/licenses/mit-license).
// More information can be found in "LICENSE"

namespace BedOwnershipTools.Whathecode.System.Linq
{
    /// <summary>
    /// TODO: Create ExceptionHelper to build and throw common exceptions.
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// Merges three sequences by using the specified predicate function.
        /// </summary>
        /// <typeparam name = "TFirst">The type of the elements of the first input sequence.</typeparam>
        /// <typeparam name = "TSecond">The type of the elements of the second input sequence.</typeparam>
        /// <typeparam name = "TThird">The type of the elements of the third input sequence.</typeparam>
        /// <typeparam name = "TResult">The type of the elements of the result sequence.</typeparam>
        /// <param name = "first">The first sequence to merge.</param>
        /// <param name = "second">The second sequence to merge.</param>
        /// <param name = "third">The third sequence to merge.</param>
        /// <param name = "resultSelector">A function that specifies how to merge the elements from the three sequences.</param>
        /// <returns>An <see cref="IEnumerable{T}" /> that contains merged elements of three input sequences.</returns>
        public static IEnumerable<TResult> Zip<TFirst, TSecond, TThird, TResult>(
            this IEnumerable<TFirst> first,
            IEnumerable<TSecond> second,
            IEnumerable<TThird> third,
            Func<TFirst, TSecond, TThird, TResult> resultSelector )
        {
            if ( first == null || second == null || third == null || resultSelector == null )
            {
                throw new ArgumentNullException( "All arguments should be non-null." );
            }

            using ( IEnumerator<TFirst> iterator1 = first.GetEnumerator() )
            using ( IEnumerator<TSecond> iterator2 = second.GetEnumerator() )
            using ( IEnumerator<TThird> iterator3 = third.GetEnumerator() )
            {
                while ( iterator1.MoveNext() && iterator2.MoveNext() && iterator3.MoveNext() )
                {
                    yield return resultSelector( iterator1.Current, iterator2.Current, iterator3.Current );
                }
            }
        }
    }
}
