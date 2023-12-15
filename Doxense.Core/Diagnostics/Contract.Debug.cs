#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Diagnostics.Contracts
{
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using JetBrains.Annotations;

	public static partial class Contract
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
