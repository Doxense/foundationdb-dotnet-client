#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
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

namespace FoundationDB.Filters.Logging
{
	using System;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	/// <summary>Set of extension methods that add logging support on transactions</summary>
	public static class FdbLoggingExtensions
	{
		/// <summary>Apply the Logging Filter to this database instance</summary>
		/// <param name="database">Original database instance</param>
		/// <param name="handler">Handler that will be called every-time a transaction commits successfully, or gets disposed. The log of all operations performed by the transaction can be accessed via the <see cref="FdbLoggedTransaction.Log"/> property.</param>
		/// <param name="options">Optional logging options</param>
		/// <returns>Database filter, that will monitor all transactions initiated from it. Disposing this wrapper will NOT dispose the inner <paramref name="database"/> database.</returns>
		[NotNull]
		public static FdbLoggedDatabase Logged([NotNull] this IFdbDatabase database, [NotNull] Action<FdbLoggedTransaction> handler, FdbLoggingOptions options = FdbLoggingOptions.Default)
		{
			Contract.NotNull(database);
			Contract.NotNull(handler);

			// prevent multiple logging
			database = WithoutLogging(database);

			return new FdbLoggedDatabase(database, false, false, handler, options);
		}

		/// <summary>Apply the Logging Filter to the database exposed by this provider</summary>
		/// <param name="provider">Original database provider instance</param>
		/// <param name="handler">Handler that will be called every-time a transaction commits successfully, or gets disposed. The log of all operations performed by the transaction can be accessed via the <see cref="FdbLoggedTransaction.Log"/> property.</param>
		/// <param name="options">Optional logging options</param>
		/// <returns>Provider that will that will monitor all transactions initiated from it.</returns>
		[NotNull]
		public static IFdbDatabaseScopeProvider Logged([NotNull] this IFdbDatabaseScopeProvider provider, [NotNull] Action<FdbLoggedTransaction> handler, FdbLoggingOptions options = FdbLoggingOptions.Default)
		{
			Contract.NotNull(provider);
			Contract.NotNull(handler);

			return provider.CreateScope<object>((db, ct) => !ct.IsCancellationRequested ? Task.FromResult<(IFdbDatabase, object)>((Logged(db, handler), null)) : Task.FromCanceled<(IFdbDatabase, object)>(ct));
		}

		/// <summary>Strip the logging behaviour of this database. Use this for boilerplate or test code that would pollute the logs otherwise.</summary>
		/// <param name="database">Database instance (that may or may not be logged)</param>
		/// <returns>Either <paramref name="database"/> itself if it is not logged, or the inner database if it was.</returns>
		[NotNull]
		public static IFdbDatabase WithoutLogging([NotNull] this IFdbDatabase database)
		{
			Contract.NotNull(database);

			return database is FdbLoggedDatabase logged ? logged.GetInnerDatabase() : database;
		}

		[CanBeNull]
		internal static FdbLoggedTransaction GetLogger([NotNull] IFdbReadOnlyTransaction trans)
		{
			//TODO: the logged transaction could also be wrapped in other filters.
			// => we need a recursive "FindFilter<TFilter>" method that would unwrap the filter onion looking for a specific one...

			return trans as FdbLoggedTransaction;
		}

		/// <summary>Test if logging is enabled on this transaction</summary>
		/// <remarks>If <c>false</c>, then there is no point in calling <see cref="Annotate(FoundationDB.Client.IFdbReadOnlyTransaction,string)"/> because logging is disabled.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsLogged([NotNull] this IFdbReadOnlyTransaction trans)
		{
			return trans is FdbLoggedTransaction;
		}

		/// <summary>Annotate a logged transaction</summary>
		/// <remarks>
		/// This method only applies to transactions created from a <see cref="FdbLoggedDatabase">logged database</see> instance.
		/// Calling this method on regular transaction is a no-op.
		/// You can call <see cref="IsLogged"/> first, if you don't want to pay the cost of formatting <paramref name="message"/> when logging not enabled.
		/// </remarks>
		public static void Annotate([NotNull] this IFdbReadOnlyTransaction trans, [NotNull] string message)
		{
			var logged = GetLogger(trans);
			logged?.Log.AddOperation(new FdbTransactionLog.LogCommand(message), countAsOperation: false);
		}

		/// <summary>Annotate a logged transaction</summary>
		/// <remarks>
		/// This method only applies to transactions created from a <see cref="FdbLoggedDatabase">logged database</see> instance.
		/// Calling this method on regular transaction is a no-op.
		/// You can call <see cref="IsLogged"/> first, if you don't want to pay the cost of formatting the message when logging not enabled.
		/// </remarks>
		[StringFormatMethod("format")]
		public static void Annotate([NotNull] this IFdbReadOnlyTransaction trans, [NotNull] string format, object arg0)
		{
			var logged = GetLogger(trans);
			logged?.Log.AddOperation(new FdbTransactionLog.LogCommand(string.Format(CultureInfo.InvariantCulture, format, arg0)), countAsOperation: false);
		}

		/// <summary>Annotate a logged transaction</summary>
		/// <remarks>
		/// This method only applies to transactions created from a <see cref="FdbLoggedDatabase">logged database</see>instance.
		/// Calling this method on regular transaction is a no-op.
		/// You can call <see cref="IsLogged"/> first, if you don't want to pay the cost of formatting the message when logging not enabled.
		/// </remarks>
		[StringFormatMethod("format")]
		public static void Annotate([NotNull] this IFdbReadOnlyTransaction trans, [NotNull] string format, object arg0, object arg1)
		{
			GetLogger(trans)?.Log.AddOperation(new FdbTransactionLog.LogCommand(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1)), countAsOperation: false);
		}

		/// <summary>Annotate a logged transaction</summary>
		/// <remarks>
		/// This method only applies to transactions created from a <see cref="FdbLoggedDatabase">logged database</see> instance.
		/// Calling this method on regular transaction is a no-op.
		/// You can call <see cref="IsLogged"/> first, if you don't want to pay the cost of formatting the message when logging not enabled.
		/// </remarks>
		[StringFormatMethod("format")]
		public static void Annotate([NotNull] this IFdbReadOnlyTransaction trans, [NotNull] string format, object arg0, object arg1, object arg2)
		{
			GetLogger(trans)?.Log.AddOperation(new FdbTransactionLog.LogCommand(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1, arg2)), countAsOperation: false);
		}

		/// <summary>Annotate a logged transaction</summary>
		/// <remarks>
		/// This method only applies to transactions created from a <see cref="FdbLoggedDatabase">logged database</see> instance.
		/// Calling this method on regular transaction is a no-op.
		/// You can call <see cref="IsLogged"/> first, if you don't want to pay the cost of formatting the message when logging not enabled.
		/// </remarks>
		[StringFormatMethod("format")]
		public static void Annotate([NotNull] this IFdbReadOnlyTransaction trans, [NotNull] string format, params object[] args)
		{
			GetLogger(trans)?.Log.AddOperation(new FdbTransactionLog.LogCommand(string.Format(CultureInfo.InvariantCulture, format, args)), countAsOperation: false);
		}

	}

}
