#region Copyright (c) 2013-2022, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

#if !USE_SHARED_FRAMEWORK

#nullable enable

namespace Doxense.Diagnostics.Contracts
{
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using JetBrains.Annotations;

	internal static partial class Contract
	{

		// ReSharper disable MemberHidesStaticFromOuterClass

		/// <summary>Contracts that are only evaluated in Debug builds</summary>
		public static class Debug
		{

			// contains most of the same contracts as the main class, but only for Debug builds.
			// ie: Contract.NotNull(...) will run in both Debug and Release builds, while Contract.Debug.NotNull(...) will NOT be evaluated in Release builds

			#region DEBUG checks...

			/// <summary>[DEBUG ONLY] Vérifie qu'une pré-condition est vrai, lors de l'entrée dans une méthode</summary>
			/// <param name="condition">Condition qui ne doit jamais être fausse</param>
			/// <param name="userMessage">Message décrivant l'erreur (optionnel)</param>
			/// <param name="conditionText">Texte de la condition (optionnel, injecté par le compilateur)</param>
			/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
			[Conditional("DEBUG")]
			[AssertionMethod]
			public static void Requires(
				[AssertionCondition(AssertionConditionType.IS_TRUE)]
#if USE_ANNOTATIONS
				[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
#endif
				bool condition,
				string? userMessage = null,
				[CallerArgumentExpression("condition")] string? conditionText = null
			)
			{
#if DEBUG
				if (!condition) throw RaiseContractFailure(System.Diagnostics.Contracts.ContractFailureKind.Precondition, userMessage, conditionText);
#endif
			}

			/// <summary>[DEBUG ONLY] Vérifie qu'une condition est toujours vrai, dans le body dans une méthode</summary>
			/// <param name="condition">Condition qui ne doit jamais être fausse</param>
			/// <param name="userMessage">Message décrivant l'erreur (optionnel)</param>
			/// <param name="conditionText">Texte de la condition (optionnel, injecté par le compilateur)</param>
			/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
			[Conditional("DEBUG")]
			[AssertionMethod]
			public static void Assert(
				[AssertionCondition(AssertionConditionType.IS_TRUE)]
#if USE_ANNOTATIONS
				[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
#endif
				bool condition,
				string? userMessage = null,
				[CallerArgumentExpression("condition")] string? conditionText = null)
			{
#if DEBUG
				if (!condition) throw RaiseContractFailure(System.Diagnostics.Contracts.ContractFailureKind.Assert, userMessage, conditionText);
#endif
			}

			/// <summary>[DEBUG ONLY] Vérifie qu'une condition est toujours vrai, lors de la sortie d'une méthode</summary>
			/// <param name="condition">Condition qui ne doit jamais être fausse</param>
			/// <param name="userMessage">Message décrivant l'erreur (optionnel)</param>
			/// <param name="conditionText">Texte de la condition (optionnel, injecté par le compilateur)</param>
			/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
			[Conditional("DEBUG")]
			[AssertionMethod]
			public static void Ensures(
				[AssertionCondition(AssertionConditionType.IS_TRUE)]
#if USE_ANNOTATIONS
				[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
#endif
				bool condition,
				string? userMessage = null,
				[CallerArgumentExpression("condition")] string? conditionText = null
			)
			{
#if DEBUG
				if (!condition) throw RaiseContractFailure(System.Diagnostics.Contracts.ContractFailureKind.Postcondition, userMessage, conditionText);
#endif
			}

			/// <summary>[DEBUG ONLY] Vérifie qu'une condition est toujours vrai pendant toute la vie d'une instance</summary>
			/// <param name="condition">Condition qui ne doit jamais être fausse</param>
			/// <param name="userMessage">Message décrivant l'erreur (optionnel)</param>
			/// <param name="conditionText">Texte de la condition (optionnel, injecté par le compilateur)</param>
			/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
			[Conditional("DEBUG")]
			[AssertionMethod]
			public static void Invariant(
				[AssertionCondition(AssertionConditionType.IS_TRUE)]
#if USE_ANNOTATIONS
				[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
#endif
				bool condition,
				string? userMessage = null,
				[CallerArgumentExpression("condition")] string? conditionText = null
			)
			{
#if DEBUG
				if (!condition) throw RaiseContractFailure(System.Diagnostics.Contracts.ContractFailureKind.Invariant, userMessage, conditionText);
#endif
			}

			#endregion

		}

		// ReSharper restore MemberHidesStaticFromOuterClass

	}

}

#if NETFRAMEWORK || NETSTANDARD2_0 || NETSTANDARD2_1

// shim CallerArgumentExpressionAttribute introduced in .NET 6
//note: compiler will recognize it correctly and add the expression string automatically even when building for older frameworks!

namespace System.Runtime.CompilerServices
{
	using System.ComponentModel;

	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
	internal sealed class CallerArgumentExpressionAttribute : Attribute
	{
		public CallerArgumentExpressionAttribute(string parameterName)
		{
			ParameterName = parameterName;
		}

		public string ParameterName { get; }
	}

	/// <summary>
	/// Reserved to be used by the compiler for tracking metadata.
	/// This class should not be used by developers in source code.
	/// This dummy class is required to compile records when targeting .NET Standard
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static class IsExternalInit { }

}

#endif

#endif
