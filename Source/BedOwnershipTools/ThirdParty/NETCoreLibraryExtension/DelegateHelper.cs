using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using BedOwnershipTools.Whathecode.System.Reflection;

// https://github.com/Whathecode/NET-Core-Library-Extension/blob/ba69ef355557bf0abc13a188c74557fbcb99ffba/src/Whathecode.System/DelegateHelper.cs

// .NET Core Library Extension
// Copyright (c) 2016 Steven Jeuris
// The library is distributed under the terms of the MIT license (http://opensource.org/licenses/mit-license).
// More information can be found in "LICENSE"

namespace BedOwnershipTools.Whathecode.System
{
    /// <summary>
    /// A helper class to do common <see cref="Delegate" /> operations.
    /// TODO: Add extra contracts to reenforce correct usage.
    /// TODO: Since Microsoft moved .CreateDelegate to MethodInfo, the helper methods here should be moved there as well.
    /// </summary>
    public static class DelegateHelper
    {
        /// <summary>
        /// Options which specify what type of delegate should be created.
        /// </summary>
        public enum CreateOptions
        {
            None,
            /// <summary>
            /// Makes a delegate that points to the target method like that made by CreateDelegate or a compiler,
            /// but without the type verification normally required for soundness.
            /// Possibly justifiable for use in simple cases where only out refs are "implicitly" upcast.
            /// Ignore the fact this option exists and use the downcasting options instead.
            /// </summary>
            Unchecked,
            /// <summary>
            /// Downcasts of delegate parameter types to the correct types required for the method are done where necessary.
            /// Performs generation-time type verification and inserts runtime casting checks.
            /// Cannot handle ref parameters.
            /// </summary>
            Downcasting,
            /// <summary>
            /// Downcasts of delegate parameter types to the correct types required for the method are done where necessary.
            /// Performs generation-time type verification and inserts runtime casting checks.
            /// Can handle ref parameters, but does not typecheck ref conversions at runtime.
            /// </summary>
            DowncastingILG
        }


        /// <summary>
        /// Holds the expressions for the parameters when creating delegates.
        /// </summary>
        struct ParameterConversionExpressions
        {
            public IEnumerable<ParameterExpression> OriginalParameters;
            public IEnumerable<Expression> ConvertedParameters;
        }


        /// <summary>
        /// The name of the Invoke method of a Delegate.
        /// </summary>
        const string InvokeMethod = "Invoke";


        /// <summary>
        /// Get method info for a specified delegate type.
        /// </summary>
        /// <param name = "delegateType">The delegate type to get info for.</param>
        /// <returns>The method info for the given delegate type.</returns>
        public static MethodInfo MethodInfoFromDelegateType( Type delegateType )
        {
            TypeInfo info = delegateType.GetTypeInfo();
            if ( !info.IsSubclassOf( typeof( MulticastDelegate ) ) )
            {
                throw new ArgumentException( "Given type should be a delegate.", nameof( delegateType ) );
            }

            return info.GetMethod( InvokeMethod );
        }

        /// <summary>
        /// Creates a delegate of a specified type which wraps another similar delegate, doing downcasts where necessary.
        /// The created delegate will only work in case the casts are valid.
        /// </summary>
        /// <typeparam name = "TDelegate">The type for the delegate to create.</typeparam>
        /// <param name = "toWrap">The delegate which needs to be wrapped by another delegate.</param>
        /// <returns>A new delegate which wraps the passed delegate, doing downcasts where necessary.</returns>
        public static TDelegate WrapDelegate<TDelegate>( Delegate toWrap )
            where TDelegate : class
        {
            if ( !typeof( TDelegate ).GetTypeInfo().IsSubclassOf( typeof( MulticastDelegate ) ) )
            {
                throw new ArgumentException( "Specified type should be a delegate." );
            }

            MethodInfo toCreateInfo = MethodInfoFromDelegateType( typeof( TDelegate ) );
            MethodInfo toWrapInfo = toWrap.GetMethodInfo();

            // Create delegate original and converted parameters.
            // TODO: In the unlikely event that someone would create a delegate with a Closure argument, the following logic will fail. Add precondition to check this?
            IEnumerable<Type> toCreateArguments = toCreateInfo.GetParameters().Select( d => d.ParameterType );
            ParameterInfo[] test = toWrapInfo.GetParameters();
            IEnumerable<Type> toWrapArguments = toWrapInfo.GetParameters()
                // Closure argument isn't an actual argument, but added by the compiler for dynamically generated methods (expression trees).
                .SkipWhile( p => p.ParameterType.FullName == "System.Runtime.CompilerServices.Closure" )
                .Select( p => p.ParameterType );
            ParameterConversionExpressions parameterExpressions = CreateParameterConversionExpressions( toCreateArguments, toWrapArguments );

            // Create call to wrapped delegate.
            Expression delegateCall = Expression.Invoke(
                Expression.Constant( toWrap ),
                parameterExpressions.ConvertedParameters );

            return Expression.Lambda<TDelegate>(
                ConvertOrWrapDelegate( delegateCall, toCreateInfo.ReturnType ),
                parameterExpressions.OriginalParameters
                ).Compile();
        }


        /// <summary>
        /// Creates a delegate of a specified type that represents the specified static or instance method,
        /// with the specified first argument.
        /// </summary>
        /// <typeparam name = "TDelegate">The type for the delegate.</typeparam>
        /// <param name = "method">The MethodInfo describing the static or instance method the delegate is to represent.</param>
        /// <param name = "instance">When method is an instance method, the instance to call this method on. Null for static methods.</param>
        /// <param name = "options">Options which specify what type of delegate should be created.</param>
        public static TDelegate CreateDelegate<TDelegate>(
            MethodInfo method,
            object instance = null,
            CreateOptions options = CreateOptions.None )
            where TDelegate : class
        {
            switch ( options )
            {
                case CreateOptions.None:
                    // Ordinary delegate creation, maintaining variance safety.
                    return method.CreateDelegate( typeof( TDelegate ), instance ) as TDelegate;

                case CreateOptions.Unchecked:
                    {
                        DynamicMethod makerDynMethod = new($"{method.Name}_DelegateHelperUncheckedMaker", typeof(TDelegate), new Type[] {});
                        ILGenerator generator = makerDynMethod.GetILGenerator();

                        // ECMA-335 II.14.6.1 -- delegate construction routine
                        // ECMA-335 III.4.21 -- verification process for invokations of newobj on delegate constructors
                        // Roslyn generates this rough sequence when it knows that a delegate will be bound to a compatible method
                        generator.Emit(OpCodes.Ldnull);
                        generator.Emit(OpCodes.Ldftn, method);
                        generator.Emit(OpCodes.Newobj, typeof(TDelegate).GetConstructor(new[] { typeof(object), typeof(IntPtr) }));
                        generator.Emit(OpCodes.Ret);

                        // The delegate manufactured by this maker behaves in a bounded manner if callers externally
                        // verify type correctness by inspection or insert appropriate runtime checks

                        return makerDynMethod.CreateDelegate(typeof(Func<TDelegate>)).DynamicInvoke() as TDelegate;
                    }

                case CreateOptions.Downcasting:
                    {
                        MethodInfo delegateInfo = MethodInfoFromDelegateType( typeof( TDelegate ) );

                        // Create delegate original and converted arguments.
                        IEnumerable<Type> delegateTypes = delegateInfo.GetParameters().Select( d => d.ParameterType );
                        IEnumerable<Type> methodTypes = method.GetParameters().Select( p => p.ParameterType );
                        ParameterConversionExpressions delegateParameterExpressions = CreateParameterConversionExpressions( delegateTypes, methodTypes );

                        // Create method call.
                        Expression methodCall = Expression.Call(
                            instance == null ? null : Expression.Constant( instance ),
                            method,
                            delegateParameterExpressions.ConvertedParameters );

                        return Expression.Lambda<TDelegate>(
                            ConvertOrWrapDelegate( methodCall, delegateInfo.ReturnType ), // Convert return type when necessary.
                            delegateParameterExpressions.OriginalParameters
                            ).Compile();
                    }

                case CreateOptions.DowncastingILG:
                    {
                        MethodInfo delegateInfo = MethodInfoFromDelegateType( typeof( TDelegate ) );

                        // Create delegate original and converted arguments.
                        Type[] delegateTypes = delegateInfo.GetParameters().Select( d => d.ParameterType ).ToArray();
                        Type[] methodTypes = method.GetParameters().Select( p => p.ParameterType ).ToArray();

                        DynamicMethod dynMethod = new($"{method.Name}_DelegateHelperDowncastingILG", delegateInfo.ReturnType, delegateTypes );
                        ILGenerator generator = dynMethod.GetILGenerator();

                        // should generate identical code as the Downcasting generator in mutually supported cases

                        // ref casting is for the most part unverified and unchecked
                        // but note that it is safe to upcast an out parameter to a less derived type

                        int ldArgIdx = 0;
                        foreach (CastKind conversionKind in delegateTypes.Zip(methodTypes, ClassifyCast)) {
                            if (conversionKind != CastKind.Invalid) {
                                ILGEmitSpaceOptimalLdargVariant(generator, ldArgIdx);
                                if (conversionKind == CastKind.ByVal) {
                                    // This check is not technically necessary if the caller can guarantee type correctness
                                    generator.Emit(OpCodes.Castclass, methodTypes[ldArgIdx]);
                                } else if (conversionKind == CastKind.ByRef) {
                                    // TODO cast check byrefs (with in semantics)

                                    // Not doing anything is similar to substituting the converted reference in high-level code with
                                    // "toParam: ref Unsafe.As<TFrom, TTo>(ref fromParam)"
                                    // so in this case, the caller must guarantee type correctness
                                }
                                ldArgIdx++;
                            } else {
                                throw new InvalidOperationException($"Can't downcast parameter {ldArgIdx}'s type ({methodTypes[ldArgIdx]}) to its desired type ({delegateTypes[ldArgIdx]}).");
                            }
                        }

                        generator.Emit(OpCodes.Call, method);

                        CastKind returnTypeConversionKind = ClassifyCast(method.ReturnType, delegateInfo.ReturnType);
                        if (returnTypeConversionKind != CastKind.Invalid) {
                            if (returnTypeConversionKind == CastKind.ByVal) {
                                // For single-target function pointer generation, it's unlikely we'd intentionally specify a delegate whose
                                // return type is more derived than its wrapped function's return type, but handling is well-defined
                                generator.Emit(OpCodes.Castclass, method.ReturnType);
                            } else if (returnTypeConversionKind == CastKind.ByRef) {
                                // TODO byref return type?
                            }
                        } else {
                            throw new InvalidOperationException($"Can't upcast return type ({method.ReturnType}) to its desired type ({delegateInfo.ReturnType}).");
                        }
                        // TODO cast check byrefs (with out semantics)

                        generator.Emit(OpCodes.Ret);

                        return dynMethod.CreateDelegate(typeof(TDelegate)) as TDelegate;
                    }

                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Creates a delegate of a specified type that represents a method which can be executed on an instance passed as parameter.
        /// </summary>
        /// <typeparam name = "TDelegate">
        /// The type for the delegate. This delegate needs at least one (first) type parameter denoting the type of the instance
        /// which will be passed.
        /// E.g., Action&lt;ExampleObject, object&gt;,
        /// where ExampleObject denotes the instance type and object denotes the desired type of the first parameter of the method.
        /// </typeparam>
        /// <param name = "method">The MethodInfo describing the method of the instance type.</param>
        /// <param name = "options">Options which specify what type of delegate should be created.</param>
        public static TDelegate CreateOpenInstanceDelegate<TDelegate>(
            MethodInfo method,
            CreateOptions options = CreateOptions.None )
            where TDelegate : class
        {
            if ( method.IsStatic )
            {
                throw new ArgumentException( "Since the delegate expects an instance, the method cannot be static.", nameof( method ) );
            }

            switch ( options )
            {
                case CreateOptions.None:
                    // Ordinary delegate creation, maintaining variance safety.
                    return method.CreateDelegate( typeof( TDelegate ) ) as TDelegate;

                case CreateOptions.Unchecked:
                    {
                        DynamicMethod makerDynMethod = new($"{method.Name}_DelegateHelperUncheckedMaker", typeof(TDelegate), new Type[] {});
                        ILGenerator generator = makerDynMethod.GetILGenerator();

                        // ECMA-335 II.14.6.1 -- delegate construction routine
                        // ECMA-335 III.4.21 -- verification process for invokations of newobj on delegate constructors
                        // Roslyn generates this rough sequence when it knows that a delegate will be bound to a compatible method
                        generator.Emit(OpCodes.Ldnull);
                        generator.Emit(OpCodes.Ldftn, method);
                        generator.Emit(OpCodes.Newobj, typeof(TDelegate).GetConstructor(new[] { typeof(object), typeof(IntPtr) }));
                        generator.Emit(OpCodes.Ret);

                        // The delegate manufactured by this maker behaves in a bounded manner if callers externally
                        // verify type correctness by inspection or insert appropriate runtime checks

                        // In the case of an instance method, this will call the most derived variant of a virtual method
                        // ldvirtftn appears to be for closed instance delegates only

                        return makerDynMethod.CreateDelegate(typeof(Func<TDelegate>)).DynamicInvoke() as TDelegate;
                    }

                case CreateOptions.Downcasting:
                    {
                        MethodInfo delegateInfo = MethodInfoFromDelegateType( typeof( TDelegate ) );
                        ParameterInfo[] delegateParameters = delegateInfo.GetParameters();

                        // Convert instance type when necessary.
                        Type delegateInstanceType = delegateParameters.Select( p => p.ParameterType ).First();
                        Type methodInstanceType = method.DeclaringType;
                        ParameterExpression instance = Expression.Parameter( delegateInstanceType );
                        Expression convertedInstance = ConvertOrWrapDelegate( instance, methodInstanceType );

                        // Create delegate original and converted arguments.
                        IEnumerable<Type> delegateTypes = delegateParameters.Select( d => d.ParameterType ).Skip( 1 );
                        IEnumerable<Type> methodTypes = method.GetParameters().Select( m => m.ParameterType );
                        ParameterConversionExpressions delegateParameterExpressions = CreateParameterConversionExpressions( delegateTypes, methodTypes );

                        // Create method call.
                        Expression methodCall = Expression.Call(
                            convertedInstance,
                            method,
                            delegateParameterExpressions.ConvertedParameters );

                        return Expression.Lambda<TDelegate>(
                            ConvertOrWrapDelegate( methodCall, delegateInfo.ReturnType ), // Convert return type when necessary.
                            new[] { instance }.Concat( delegateParameterExpressions.OriginalParameters )
                            ).Compile();
                    }

                case CreateOptions.DowncastingILG:
                    {
                        MethodInfo delegateInfo = MethodInfoFromDelegateType( typeof( TDelegate ) );
                        ParameterInfo[] delegateParameters = delegateInfo.GetParameters();

                        // Convert instance type when necessary.
                        Type delegateInstanceType = delegateParameters.Select( p => p.ParameterType ).First();
                        Type methodInstanceType = method.DeclaringType;

                        // Create delegate original and converted arguments.
                        Type[] delegateTypes = delegateParameters.Select( d => d.ParameterType ).Skip( 1 ).ToArray();
                        Type[] methodTypes = method.GetParameters().Select( m => m.ParameterType ).ToArray();

                        DynamicMethod dynMethod = new($"{method.Name}_DelegateHelperDowncastingILG", delegateInfo.ReturnType, new[] { delegateInstanceType }.Concat(delegateTypes).ToArray() );
                        ILGenerator generator = dynMethod.GetILGenerator();

                        CastKind instanceTypeConversionKind = ClassifyCast(delegateInstanceType, methodInstanceType);
                        if (instanceTypeConversionKind != CastKind.Invalid) {
                            generator.Emit(OpCodes.Ldarg_0);
                            if (instanceTypeConversionKind == CastKind.ByVal) {
                                generator.Emit(OpCodes.Castclass, methodInstanceType);
                            } else if (instanceTypeConversionKind == CastKind.ByRef) {
                                // likely unhittable
                            }
                        } else {
                            throw new InvalidOperationException($"Can't downcast parameter 0's type ({methodInstanceType}) to its desired type ({delegateInstanceType}).");
                        }

                        int ldArgIdx = 1;
                        foreach (CastKind conversionKind in delegateTypes.Zip(methodTypes, ClassifyCast)) {
                            if (conversionKind != CastKind.Invalid) {
                                ILGEmitSpaceOptimalLdargVariant(generator, ldArgIdx);
                                if (conversionKind == CastKind.ByVal) {
                                    generator.Emit(OpCodes.Castclass, methodTypes[ldArgIdx]);
                                } else if (conversionKind == CastKind.ByRef) {
                                    // TODO cast check byrefs (with in semantics)
                                }
                                ldArgIdx++;
                            } else {
                                throw new InvalidOperationException($"Can't downcast parameter {ldArgIdx}'s type ({methodTypes[ldArgIdx]}) to its desired type ({delegateTypes[ldArgIdx]}).");
                            }
                        }

                        generator.Emit(OpCodes.Callvirt, method);

                        CastKind returnTypeConversionKind = ClassifyCast(method.ReturnType, delegateInfo.ReturnType);
                        if (returnTypeConversionKind != CastKind.Invalid) {
                            if (returnTypeConversionKind == CastKind.ByVal) {
                                generator.Emit(OpCodes.Castclass, method.ReturnType);
                            } else if (returnTypeConversionKind == CastKind.ByRef) {
                                // TODO byref return type?
                            }
                        } else {
                            throw new InvalidOperationException($"Can't upcast return type ({method.ReturnType}) to its desired type ({delegateInfo.ReturnType}).");
                        }
                        // TODO cast check byrefs (with out semantics)

                        generator.Emit(OpCodes.Ret);

                        return dynMethod.CreateDelegate(typeof(TDelegate)) as TDelegate;
                    }

                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Creates the expressions for delegate parameters and their conversions
        /// to the corresponding required types where necessary.
        /// </summary>
        /// <param name = "toCreateTypes">The types of the parameters of the delegate to create.</param>
        /// <param name = "toWrapTypes">The types of the parameters of the call to wrap.</param>
        /// <returns>An object containing the delegate expressions.</returns>
        static ParameterConversionExpressions CreateParameterConversionExpressions(
            IEnumerable<Type> toCreateTypes,
            IEnumerable<Type> toWrapTypes )
        {
            ParameterExpression[] originalParameters = toCreateTypes.Select( Expression.Parameter ).ToArray(); // ToArray prevents deferred execution.

            return new ParameterConversionExpressions
            {
                OriginalParameters = originalParameters,

                // Convert the parameters from the delegate parameter type to the required type when necessary.
                ConvertedParameters = originalParameters.Zip( toWrapTypes, ConvertOrWrapDelegate )
            };
        }

        /// <summary>
        /// Converts the result of the given expression to the desired type,
        /// or when it is a delegate, tries to wrap it with a delegate which attempts to do downcasts where necessary.
        /// </summary>
        /// <param name = "expression">The expression of which the result needs to be converted.</param>
        /// <param name = "toType">The type to which the result needs to be converted.</param>
        /// <returns>An expression which converts the given expression to the desired type.</returns>
        static Expression ConvertOrWrapDelegate( Expression expression, Type toType )
        {
            Expression convertedExpression;
            TypeInfo fromTypeInfo = expression.Type.GetTypeInfo();
            TypeInfo toTypeInfo = toType.GetTypeInfo();

            if ( toTypeInfo == fromTypeInfo )
            {
                convertedExpression = expression;    // No conversion of the return type needed.
            }
            else
            {
                // TODO: CanConvertTo is incomplete. For the current purpose it returns the correct result, but might not in all cases.
                if ( fromTypeInfo.CanConvertTo( toTypeInfo, CastType.SameHierarchy ) )
                {
                    convertedExpression = Expression.Convert( expression, toType );
                }
                else
                {
                    // When the return type is a delegate, attempt recursively wrapping it, adding extra conversions where needed. E.g. Func<T>
                    if ( fromTypeInfo.IsDelegate() && fromTypeInfo.IsGenericType )
                    {
                        Func<Delegate, object> wrapDelegateDelegate = WrapDelegate<object>;
                        MethodInfo wrapDelegateMethod = wrapDelegateDelegate.GetMethodInfo().GetGenericMethodDefinition( toType );
                        MethodCallExpression wrapDelegate = Expression.Call( wrapDelegateMethod, expression );
                        convertedExpression = wrapDelegate;
                    }
                    else
                    {
                        throw new InvalidOperationException( "Can't downcast the return type to its desired type." );
                    }
                }
            }

            return convertedExpression;
        }

        public enum CastKind
        {
            Invalid,
            Identical,
            ByVal,
            ByRef
        }

        static CastKind ClassifyCast(Type fromType, Type toType) {
            TypeInfo fromTypeInfo = fromType.GetTypeInfo();
            TypeInfo toTypeInfo = toType.GetTypeInfo();

            if ( toTypeInfo == fromTypeInfo ) {
                return CastKind.Identical;
            } else {
                if (
                    // TODO does not account for in-ness/out-ness of ref
                    fromTypeInfo.IsByRef && toTypeInfo.IsByRef &&
                    fromType.GetElementType().GetTypeInfo().CanConvertTo(toType.GetElementType().GetTypeInfo(), CastType.SameHierarchy)
                ) {
                    return CastKind.ByRef;
                } else if (fromTypeInfo.CanConvertTo( toTypeInfo, CastType.SameHierarchy )) {
                    return CastKind.ByVal;
                }
            }
            return CastKind.Invalid;
        }

        static void ILGEmitSpaceOptimalLdargVariant(ILGenerator generator, int idx) {
            switch (idx) {
                case 0:
                    generator.Emit(OpCodes.Ldarg_0);
                    break;
                case 1:
                    generator.Emit(OpCodes.Ldarg_1);
                    break;
                case 2:
                    generator.Emit(OpCodes.Ldarg_2);
                    break;
                case 3:
                    generator.Emit(OpCodes.Ldarg_3);
                    break;
                case int n when n <= 255:
                    generator.Emit(OpCodes.Ldarg_S, n);
                    break;
                default:
                    generator.Emit(OpCodes.Ldarg, idx);
                    break;
            }
        }
    }
}
