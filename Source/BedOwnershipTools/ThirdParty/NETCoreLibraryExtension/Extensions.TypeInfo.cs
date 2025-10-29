using System;
using System.Linq;
using System.Reflection;
using BedOwnershipTools.Whathecode.System.Linq;

// https://github.com/Whathecode/NET-Core-Library-Extension/blob/ba69ef355557bf0abc13a188c74557fbcb99ffba/src/Whathecode.System/Reflection/Extensions.TypeInfo.cs

// .NET Core Library Extension
// Copyright (c) 2016 Steven Jeuris
// The library is distributed under the terms of the MIT license (http://opensource.org/licenses/mit-license).
// More information can be found in "LICENSE"

namespace BedOwnershipTools.Whathecode.System.Reflection
{
    public static partial class Extensions
    {
        /// <summary>
        /// Determines whether a conversion from one type to another is possible.
        /// This uses .NET rules. E.g., short is not implicitly convertible to int, while this is possible in C#.
        /// TODO: Support constraints, custom implicit conversion operators? Unit tests for explicit converts.
        /// TODO: The following seems the inverse of 'IsAssignableFrom'
        /// </summary>
        /// <param name = "fromType">The type to convert from.</param>
        /// <param name = "targetType">The type to convert to.</param>
        /// <param name = "castType">Specifies what types of casts should be considered.</param>
        /// <returns>true when a conversion to the target type is possible, false otherwise.</returns>
        public static bool CanConvertTo( this TypeInfo fromType, TypeInfo targetType, CastType castType = CastType.Implicit )
        {
            return CanConvertTo( fromType, targetType, castType, false );
        }

        static bool CanConvertTo( this TypeInfo fromType, TypeInfo targetType, CastType castType, bool switchVariance )
        {
            bool sameHierarchy = castType == CastType.SameHierarchy;

            Func<TypeInfo, TypeInfo, bool> covarianceCheck = sameHierarchy
                ? (Func<TypeInfo, TypeInfo, bool>)IsInHierarchy
                : ( from, to ) => from == to || from.IsSubclassOf( to.AsType() );
            Func<TypeInfo, TypeInfo, bool> contravarianceCheck = sameHierarchy
                ? (Func<TypeInfo, TypeInfo, bool>)IsInHierarchy
                : ( from, to ) => from == to || to.IsSubclassOf( from.AsType() );

            if ( switchVariance )
            {
                Variable.Swap( ref covarianceCheck, ref contravarianceCheck );
            }

            // Simple hierarchy check.
            if ( covarianceCheck( fromType, targetType ) )
            {
                return true;
            }

            // Interface check.
            if ( (targetType.IsInterface && fromType.ImplementsInterface( targetType ))
                || (sameHierarchy && fromType.IsInterface && targetType.ImplementsInterface( fromType )) )
            {
                return true;
            }

            // Explicit value type conversions (including enums).
            if ( sameHierarchy && (fromType.IsValueType && targetType.IsValueType) )
            {
                return true;
            }

            // Recursively verify when it is a generic type.
            if ( targetType.IsGenericType )
            {
                TypeInfo genericDefinition = targetType.GetGenericTypeDefinition().GetTypeInfo();
                TypeInfo sourceGeneric = fromType.GetMatchingGenericType( genericDefinition );

                // Delegates never support casting in the 'opposite' direction than their varience type parameters dictate.
                CastType cast = fromType.IsDelegate() ? CastType.Implicit : castType;

                if ( sourceGeneric != null ) // Same generic types.
                {
                    // Check whether parameters correspond, taking into account variance rules.
                    return sourceGeneric.GetGenericArguments().Select( s => s.GetTypeInfo() ).Zip(
                        targetType.GetGenericArguments().Select( t => t.GetTypeInfo() ), genericDefinition.GetGenericArguments().Select( g => g.GetTypeInfo() ),
                        ( from, to, generic )
                            => !(from.IsValueType || to.IsValueType)    // Variance applies only to reference types.
                                ? generic.GenericParameterAttributes.HasFlag( GenericParameterAttributes.Covariant )
                                    ? CanConvertTo( from, to, cast, false )
                                    : generic.GenericParameterAttributes.HasFlag( GenericParameterAttributes.Contravariant )
                                        ? CanConvertTo( from, to, cast, true )
                                        : false
                                : false )
                        .All( match => match );
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether one type is in the same inheritance hierarchy than another.
        /// TODO: This apparently does not work for interfaces which inherent from each other: https://msdn.microsoft.com/en-us/library/system.type.issubclassof(v=vs.110).aspx
        /// </summary>
        /// <param name = "source">The source for this extension method.</param>
        /// <param name = "type">The type the check whether it is in the same inheritance hierarchy.</param>
        /// <returns>true when both types are in the same inheritance hierarchy, false otherwise.</returns>
        public static bool IsInHierarchy( this TypeInfo source, TypeInfo type )
        {
            return source == type || source.IsSubclassOf( type.AsType() ) || type.IsSubclassOf( source.AsType() );
        }
    }
}
