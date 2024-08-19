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

namespace FoundationDB.Filters.Logging
{
	using System;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	/// <summary>Set of extension methods that add logging support on transactions</summary>
	public static class FdbLoggingExtensions
	{

		/// <summary>Annotate a logged transaction</summary>
		/// <remarks>
		/// This method only applies to transactions created from a logged database instance.
		/// Calling this method on regular transaction is a no-op.
		/// You can call <see cref="IFdbReadOnlyTransaction.IsLogged"/> first, if you don't want to pay the cost of formatting the message when logging not enabled.
		/// </remarks>
		[StringFormatMethod("format")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Annotate(this IFdbReadOnlyTransaction trans, string format, object? arg0)
		{
			if (trans.IsLogged()) AnnotateCore(trans, format, arg0);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void AnnotateCore(IFdbReadOnlyTransaction trans, string format, object? arg0)
			{
				trans.Annotate(string.Format(CultureInfo.InvariantCulture, format, arg0));
			}
		}

		/// <summary>Annotate a logged transaction</summary>
		/// <remarks>
		/// This method only applies to transactions created from a logged database instance.
		/// Calling this method on regular transaction is a no-op.
		/// You can call <see cref="IFdbReadOnlyTransaction.IsLogged"/> first, if you don't want to pay the cost of formatting the message when logging not enabled.
		/// </remarks>
		[StringFormatMethod("format")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Annotate(this IFdbReadOnlyTransaction trans, string format, object? arg0, object? arg1)
		{
			if (trans.IsLogged()) AnnotateCore(trans, format, arg0, arg1);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void AnnotateCore(IFdbReadOnlyTransaction trans, string format, object? arg0, object? arg1)
			{
				trans.Annotate(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1));
			}
		}

		/// <summary>Annotate a logged transaction</summary>
		/// <remarks>
		/// This method only applies to transactions created from a logged database instance.
		/// Calling this method on regular transaction is a no-op.
		/// You can call <see cref="IFdbReadOnlyTransaction.IsLogged"/> first, if you don't want to pay the cost of formatting the message when logging not enabled.
		/// </remarks>
		[StringFormatMethod("format")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Annotate(this IFdbReadOnlyTransaction trans, string format, object? arg0, object? arg1, object? arg2)
		{
			if (trans.IsLogged()) AnnotateCore(trans, format, arg0, arg1, arg2);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void AnnotateCore(IFdbReadOnlyTransaction trans, string format, object? arg0, object? arg1, object? arg2)
			{
				trans.Annotate(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1, arg2));
			}

		}

		/// <summary>Annotate a logged transaction</summary>
		/// <remarks>
		/// This method only applies to transactions created from a logged database instance.
		/// Calling this method on regular transaction is a no-op.
		/// You can call <see cref="IFdbReadOnlyTransaction.IsLogged"/> first, if you don't want to pay the cost of formatting the message when logging not enabled.
		/// </remarks>
		[StringFormatMethod("format")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Annotate(this IFdbReadOnlyTransaction trans, string format, params object?[] args)
		{
			if (trans.IsLogged()) AnnotateCore(trans, format, args);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void AnnotateCore(IFdbReadOnlyTransaction trans, string format, object?[] args)
			{
				trans.Annotate(string.Format(CultureInfo.InvariantCulture, format, args));
			}
		}

		/// <summary>Annotate a logged transaction</summary>
		/// <remarks>
		/// This method only applies to transactions created from a logged database instance.
		/// Calling this method on regular transaction is a no-op.
		/// You can call <see cref="IFdbReadOnlyTransaction.IsLogged"/> first, if you don't want to pay the cost of formatting the message when logging not enabled.
		/// </remarks>
		[StringFormatMethod("format")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Annotate(this IFdbReadOnlyTransaction trans, ref DefaultInterpolatedStringHandler message)
		{
			if (trans.IsLogged()) trans.Annotate(message.ToStringAndClear());
		}

#if NET9_0_OR_GREATER

		/// <summary>Annotate a logged transaction</summary>
		/// <remarks>
		/// This method only applies to transactions created from a logged database instance.
		/// Calling this method on regular transaction is a no-op.
		/// You can call <see cref="IFdbReadOnlyTransaction.IsLogged"/> first, if you don't want to pay the cost of formatting the message when logging not enabled.
		/// </remarks>
		[StringFormatMethod("format")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Annotate(this IFdbReadOnlyTransaction trans, string format, params ReadOnlySpan<object?> args)
		{
			if (trans.IsLogged()) AnnotateCore(trans, format, args);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void AnnotateCore(IFdbReadOnlyTransaction trans, string format, ReadOnlySpan<object?> args)
			{
				trans.Annotate(string.Format(CultureInfo.InvariantCulture, format, args));
			}
		}

#endif

	}

}
