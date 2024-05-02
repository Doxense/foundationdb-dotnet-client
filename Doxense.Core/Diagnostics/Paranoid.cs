#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
	using System.Diagnostics;
	using System.Runtime.CompilerServices;

	/// <summary>Classe helper présente uniquement en mode Paranoid, pour la vérification de pré-requis, invariants, assertions, ...</summary>
	/// <remarks>Les méthodes de cette classes ne sont compilées que si le flag PARANOID_ANDROID est défini</remarks>
	[DebuggerNonUserCode]
	public static class Paranoid
	{
		// https://www.youtube.com/watch?v=rF8khJ7P4Wg

		/// <summary>Retourne false au runtime, et true en mode parano</summary>
		public static bool IsParanoid
		{
			get
			{
#if PARANOID_ANDROID
				return true;
#else
				return false;
#endif
			}
		}

		/// <summary>[PARANOID MODE] Vérifie qu'une pré-condition est vrai, lors de l'entrée dans une méthode</summary>
		/// <param name="condition">Condition qui ne doit jamais être fausse</param>
		/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
		[Conditional("PARANOID_ANDROID")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[JetBrains.Annotations.AssertionMethod]
		public static void Requires([JetBrains.Annotations.AssertionCondition(JetBrains.Annotations.AssertionConditionType.IS_TRUE)] bool condition)
		{
#if PARANOID_ANDROID
			if (!condition) throw Contract.RaiseContractFailure(SDC.ContractFailureKind.Precondition, null);
#endif
		}

		/// <summary>[PARANOID MODE] Vérifie qu'une pré-condition est vrai, lors de l'entrée dans une méthode</summary>
		/// <param name="condition">Condition qui ne doit jamais être fausse</param>
		/// <param name="userMessage">Message décrivant l'erreur (optionnel)</param>
		/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
		[Conditional("PARANOID_ANDROID")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[JetBrains.Annotations.AssertionMethod]
		public static void Requires([JetBrains.Annotations.AssertionCondition(JetBrains.Annotations.AssertionConditionType.IS_TRUE)] bool condition, string userMessage)
		{
#if PARANOID_ANDROID
			if (!condition) throw Contract.RaiseContractFailure(SDC.ContractFailureKind.Precondition, userMessage);
#endif
		}

		/// <summary>[PARANOID MODE] Vérifie qu'une condition est toujours vrai, dans le body dans une méthode</summary>
		/// <param name="condition">Condition qui ne doit jamais être fausse</param>
		/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
		[Conditional("PARANOID_ANDROID")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[JetBrains.Annotations.AssertionMethod]
		public static void Assert([JetBrains.Annotations.AssertionCondition(JetBrains.Annotations.AssertionConditionType.IS_TRUE)] bool condition)
		{
#if PARANOID_ANDROID
			if (!condition) throw Contract.RaiseContractFailure(SDC.ContractFailureKind.Assert, null);
#endif
		}

		/// <summary>[PARANOID MODE] Vérifie qu'une condition est toujours vrai, dans le body dans une méthode</summary>
		/// <param name="condition">Condition qui ne doit jamais être fausse</param>
		/// <param name="userMessage">Message décrivant l'erreur (optionnel)</param>
		/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
		[Conditional("PARANOID_ANDROID")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[JetBrains.Annotations.AssertionMethod]
		public static void Assert([JetBrains.Annotations.AssertionCondition(JetBrains.Annotations.AssertionConditionType.IS_TRUE)] bool condition, string userMessage)
		{
#if PARANOID_ANDROID
			if (!condition) throw Contract.RaiseContractFailure(SDC.ContractFailureKind.Assert, userMessage);
#endif
		}

		/// <summary>[PARANOID MODE] Vérifie qu'une condition est toujours vrai, lors de la sortie d'une méthode</summary>
		/// <param name="condition">Condition qui ne doit jamais être fausse</param>
		/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
		[Conditional("PARANOID_ANDROID")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[JetBrains.Annotations.AssertionMethod]
		public static void Ensures([JetBrains.Annotations.AssertionCondition(JetBrains.Annotations.AssertionConditionType.IS_TRUE)] bool condition)
		{
#if PARANOID_ANDROID
			if (!condition) throw Contract.RaiseContractFailure(SDC.ContractFailureKind.Postcondition, null);
#endif
		}

		/// <summary>[PARANOID MODE] Vérifie qu'une condition est toujours vrai, lors de la sortie d'une méthode</summary>
		/// <param name="condition">Condition qui ne doit jamais être fausse</param>
		/// <param name="userMessage">Message décrivant l'erreur (optionnel)</param>
		/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
		[Conditional("PARANOID_ANDROID")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[JetBrains.Annotations.AssertionMethod]
		public static void Ensures([JetBrains.Annotations.AssertionCondition(JetBrains.Annotations.AssertionConditionType.IS_TRUE)] bool condition, string userMessage)
		{
#if PARANOID_ANDROID
			if (!condition) throw Contract.RaiseContractFailure(SDC.ContractFailureKind.Postcondition, userMessage);
#endif
		}

		/// <summary>[PARANOID MODE] Vérifie qu'une condition est toujours vrai pendant toute la vie d'une instance</summary>
		/// <param name="condition">Condition qui ne doit jamais être fausse</param>
		/// <param name="userMessage">Message décrivant l'erreur (optionnel)</param>
		/// <remarks>Ne fait rien si la condition est vrai. Sinon déclenche une ContractException, après avoir essayé de breakpointer le debugger</remarks>
		[Conditional("PARANOID_ANDROID")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[JetBrains.Annotations.AssertionMethod]
		public static void Invariant([JetBrains.Annotations.AssertionCondition(JetBrains.Annotations.AssertionConditionType.IS_TRUE)] bool condition, string userMessage)
		{
#if PARANOID_ANDROID
			if (!condition) throw Contract.RaiseContractFailure(SDC.ContractFailureKind.Invariant, userMessage);
#endif
		}

		/// <summary>[PARANOID MODE] Vérifie qu'une instance n'est pas null (condition: "value != null")</summary>
		[Conditional("PARANOID_ANDROID")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[JetBrains.Annotations.AssertionMethod]
		public static void NotNull<TValue>([JetBrains.Annotations.AssertionCondition(JetBrains.Annotations.AssertionConditionType.IS_NOT_NULL)] TValue value, [InvokerParameterName] string? paramName = null, string? message = null)
			where TValue : class
		{
#if PARANOID_ANDROID
			if (value == null) throw Contract.FailArgumentNull(paramName!, message!);
#endif
		}

		/// <summary>[PARANOID MODE] Vérifie qu'une string n'est pas null (condition: "value != null")</summary>
		[Conditional("PARANOID_ANDROID")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[JetBrains.Annotations.AssertionMethod]
		public static void NotNull([JetBrains.Annotations.AssertionCondition(JetBrains.Annotations.AssertionConditionType.IS_NOT_NULL)] string value, [InvokerParameterName] string? paramName = null, string? message = null)
		{
#if PARANOID_ANDROID
			if (value == null) throw Contract.FailArgumentNull(paramName!, message!);
#endif
		}

		/// <summary>[PARANOID MODE] Vérifie qu'un buffer n'est pas null (condition: "value != null")</summary>
		[Conditional("PARANOID_ANDROID")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[JetBrains.Annotations.AssertionMethod]
		public static void NotNull([JetBrains.Annotations.AssertionCondition(JetBrains.Annotations.AssertionConditionType.IS_NOT_NULL)] byte[] value, [InvokerParameterName] string? paramName = null, string? message = null)
		{
#if PARANOID_ANDROID
			if (value == null) throw Contract.FailArgumentNull(paramName!, message!);
#endif
		}

	}

}
