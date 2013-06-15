#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of the <organization> nor the
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

namespace FoundationDb.Client.Utils
{
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;

	internal static class Contract
	{

		#region Requires

		[DebuggerStepThrough]
		[Conditional("DEBUG")]
#if NET_4_5
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public static void Requires(bool condition)
		{
			if (!condition) RaiseContractFailure(true, null, "A pre-requisite was not met");
		}

		[DebuggerStepThrough]
		[Conditional("DEBUG")]
#if NET_4_5
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public static void Requires(bool condition, string test, string message)
		{
			if (!condition) RaiseContractFailure(true, test, message);
		}

		#endregion

		#region Assert

		[DebuggerStepThrough]
		[Conditional("DEBUG")]
#if NET_4_5
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public static void Assert(bool condition)
		{
			if (!condition) RaiseContractFailure(true, null, "An assertion was not met");
		}

		[DebuggerStepThrough]
		[Conditional("DEBUG")]
#if NET_4_5
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public static void Assert(bool condition, string test, string message)
		{
			if (!condition) RaiseContractFailure(true, test, message);
		}

		#endregion

		#region Requires

		[DebuggerStepThrough]
		[Conditional("DEBUG")]
#if NET_4_5
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public static void Ensures(bool condition)
		{
			if (!condition) RaiseContractFailure(true, null, "A post-condition was not met");
		}

		[DebuggerStepThrough]
		[Conditional("DEBUG")]
#if NET_4_5
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public static void Ensures(bool condition, string test, string message)
		{
			if (!condition) RaiseContractFailure(true, test, message);
		}

		#endregion

		[DebuggerStepThrough]
		public static void RaiseContractFailure(bool assertion, string test, string message)
		{
			if (assertion)
			{
#if DEBUG
				if (Debugger.IsAttached) Debugger.Break();
#endif
				Debug.Fail(message, test);
			}
			else
			{
				throw new InvalidOperationException(message);
			}
		}

	}
}
