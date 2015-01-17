#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Client.Utils
{
	using JetBrains.Annotations;
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using SDC = System.Diagnostics.Contracts;

	internal static class Contract
	{

		#region Requires

		[DebuggerStepThrough, DebuggerHidden]
		[Conditional("DEBUG")]
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		[AssertionMethod]
		public static void Requires([AssertionCondition(AssertionConditionType.IS_TRUE)] bool condition, [CallerLineNumber] int _line = 0, [CallerFilePath] string _path = "")
		{
			if (!condition) RaiseContractFailure(SDC.ContractFailureKind.Precondition, null, _path, _line);
		}

		[DebuggerStepThrough, DebuggerHidden]
		[Conditional("DEBUG")]
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		[AssertionMethod]
		public static void Requires([AssertionCondition(AssertionConditionType.IS_TRUE)] bool condition, string message, [CallerLineNumber] int _line = 0, [CallerFilePath] string _path = "")
		{
			if (!condition) RaiseContractFailure(SDC.ContractFailureKind.Precondition, message, _path, _line);
		}

		#endregion

		#region Assert

		/// <summary>Assert that a condition is verified, at debug time</summary>
		/// <param name="condition">Condition that must be true</param>
		/// <param name="_line">Line number of the calling source file</param>
		/// <param name="_path">Path of the calling source file</param>
		/// <remarks>This method is not compiled on Release builds</remarks>
		[DebuggerStepThrough, DebuggerHidden]
		[Conditional("DEBUG")]
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		[AssertionMethod]
		public static void Assert([AssertionCondition(AssertionConditionType.IS_TRUE)] bool condition, [CallerLineNumber] int _line = 0, [CallerFilePath] string _path = "")
		{
			if (!condition) RaiseContractFailure(SDC.ContractFailureKind.Assert, null, _path, _line);
		}

		/// <summary>Assert that a condition is verified, at debug time</summary>
		/// <param name="condition">Condition that must be true</param>
		/// <param name="message">Error message if the condition does not pass</param>
		/// <param name="_line">Line number of the calling source file</param>
		/// <param name="_path">Path of the calling source file</param>
		/// <remarks>This method is not compiled on Release builds</remarks>
		[DebuggerStepThrough, DebuggerHidden]
		[Conditional("DEBUG")]
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		[AssertionMethod]
		public static void Assert([AssertionCondition(AssertionConditionType.IS_TRUE)] bool condition, string message, [CallerLineNumber] int _line = 0, [CallerFilePath] string _path = "")
		{
			if (!condition) RaiseContractFailure(SDC.ContractFailureKind.Assert, message, _path, _line);
		}

		#endregion

		#region Ensures

		/// <summary>Assert that a condition is verified, at debug time</summary>
		/// <param name="condition">Condition that must be true</param>
		/// <param name="_line">Line number of the calling source file</param>
		/// <param name="_path">Path of the calling source file</param>
		/// <remarks>This method is not compiled on Release builds</remarks>
		[DebuggerStepThrough, DebuggerHidden]
		[Conditional("DEBUG")]
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		[AssertionMethod]
		public static void Ensures([AssertionCondition(AssertionConditionType.IS_TRUE)] bool condition, [CallerLineNumber] int _line = 0, [CallerFilePath] string _path = "")
		{
			if (!condition) RaiseContractFailure(SDC.ContractFailureKind.Postcondition, null, _path, _line);
		}

		/// <summary>Assert that a condition is verified, at debug time</summary>
		/// <param name="condition">Condition that must be true</param>
		/// <param name="message">Error message if the condition does not pass</param>
		/// <param name="_line">Line number of the calling source file</param>
		/// <param name="_path">Path of the calling source file</param>
		/// <remarks>This method is not compiled on Release builds</remarks>
		[DebuggerStepThrough, DebuggerHidden]
		[Conditional("DEBUG")]
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		[AssertionMethod]
		public static void Ensures([AssertionCondition(AssertionConditionType.IS_TRUE)] bool condition, string message, [CallerLineNumber] int _line = 0, [CallerFilePath] string _path = "")
		{
			if (!condition) RaiseContractFailure(SDC.ContractFailureKind.Postcondition, message, _path, _line);
		}

		#endregion

		[DebuggerStepThrough, DebuggerHidden]
		internal static void RaiseContractFailure(SDC.ContractFailureKind kind, string message, string file, int line)
		{
			if (message == null)
			{
				switch(kind)
				{
					case SDC.ContractFailureKind.Assert: message = "An assertion was not met"; break;
					case SDC.ContractFailureKind.Precondition: message = "A pre-requisite was not met"; break;
					case SDC.ContractFailureKind.Postcondition: message = "A post-condition was not met"; break;
					default: message = "An expectation was not met"; break;
				}
			}
			if (file != null)
			{ // add the caller infos
				message = String.Format("{0} in {1}:line {2}", message, file, line);
			}

			//TODO: check if we are running under NUnit, and map to an Assert.Fail() instead ?

			Debug.Fail(message);
			// If you break here, that means that an assertion failed somewhere up the stack.
			// TODO: find a way to have the debugger break, but show the caller of Contract.Assert(..) method, instead of here ?
			if (Debugger.IsAttached) Debugger.Break();

			throw new InvalidOperationException(message);
		}

	}
}
