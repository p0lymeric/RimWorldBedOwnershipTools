using System;
using System.Linq;
using System.Reflection;
// using BedOwnershipTools.Whathecode.System.Linq;

// https://github.com/Whathecode/NET-Standard-Library-Extension/blob/1f296b0ff7f44eedadf39854e3c94e6002c5775a/src/Whathecode.System/Reflection/Extensions.TypeInfo.cs

// .NET Standard Library Extension
// Copyright (c) 2016 Steven Jeuris
// The library is distributed under the terms of the MIT license (http://opensource.org/licenses/mit-license).
// More information can be found in "LICENSE"

namespace BedOwnershipTools.Whathecode.System.Reflection
{
    public static partial class Extensions
    {
        /// <summary>
        /// Get the first found matching generic type. The type parameters of the generic type are optional: e.g., Dictionary&lt;,&gt;.
        /// When full (generic or non-generic) type is known (e.g., Dictionary&lt;string,string&gt;),
        /// the "is" operator is most likely more performant, but this function will still work correctly.
        /// </summary>
        /// <param name = "source">The source for this extension method.</param>
        /// <param name = "type">The type to check for.</param>
        /// <returns>The first found matching complete generic type, or null when no matching type found.</returns>
        public static TypeInfo GetMatchingGenericType( this TypeInfo source, TypeInfo type )
        {
            if ( source == null || type == null )
            {
                throw new ArgumentNullException( "All arguments should be non-null." );
            }

            Type[] genericArguments = type.GetGenericArguments();
            Type rawType = type.IsGenericType ? type.GetGenericTypeDefinition() : type.AsType();

            // Used to compare type arguments and see whether they match.
            Func<Type[], bool> argumentsMatch
                = arguments => genericArguments
                    .Zip( arguments, Tuple.Create )
                    .All( t => t.Item1.IsGenericParameter // No type specified.
                        || t.Item1 == t.Item2 );

            TypeInfo matchingType = null;
            if ( type.IsInterface && !source.IsInterface )
            {
                // Traverse across all interfaces to find a matching interface.
                matchingType = (
                    from t in source.GetInterfaces().Select( i => i.GetTypeInfo() )
                    let rawInterface = t.IsGenericType ? t.GetGenericTypeDefinition() : t.AsType()
                    where rawInterface == rawType && argumentsMatch( t.GetGenericArguments() )
                    select t
                    ).FirstOrDefault();
            }
            else
            {
                // Traverse across the type, and all it's base types.
                Type baseType = source.AsType();
                while ( baseType != null )
                {
                    TypeInfo info = baseType.GetTypeInfo();
                    Type rawCurrent = info.IsGenericType ? baseType.GetGenericTypeDefinition() : baseType;
                    if ( rawType == rawCurrent )
                    {
                        // Same raw generic type, compare type arguments.
                        if ( argumentsMatch( info.GetGenericArguments() ) )
                        {
                            matchingType = info;
                            break;
                        }
                    }
                    baseType = info.BaseType;
                }
            }

            return matchingType;
        }

        /// <summary>
        /// Verify whether the type is a delegate.
        /// </summary>
        /// <param name = "source">The source of this extension method.</param>
        /// <returns>True when the given type is a delegate, false otherwise.</returns>
        public static bool IsDelegate( this TypeInfo source )
        {
            if ( source == null )
            {
                throw new ArgumentNullException( nameof( source ) );
            }

            return source.IsSubclassOf( typeof( Delegate ) );
        }

        /// <summary>
        /// Does the type implement a given interface or not.
        /// </summary>
        /// <param name = "source">The source of this extension method.</param>
        /// <param name = "interfaceType">The interface type to check for.</param>
        /// <returns>True when the type implements the given interface, false otherwise.</returns>
        public static bool ImplementsInterface( this TypeInfo source, TypeInfo interfaceType )
        {
            if ( source == null || interfaceType == null )
            {
                throw new ArgumentNullException( "All arguments should be non-null." );
            }
            if ( !interfaceType.IsInterface )
            {
                throw new ArgumentException( "The passed type is not an interface.", nameof( interfaceType ) );
            }

            return source.GetInterfaces().Contains( interfaceType.AsType() );
        }
    }
}
