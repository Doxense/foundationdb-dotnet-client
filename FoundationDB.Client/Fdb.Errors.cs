﻿#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using System;

	public static partial class Fdb
	{

		internal static class Errors
		{

			internal static Exception CannotExecuteOnNetworkThread()
			{
				return new InvalidOperationException("Cannot commit transaction from the Network Thread!");
			}

			#region Keys / Values...

			internal static Exception KeyCannotBeNull(Slice key)
			{
				return new ArgumentException("Key cannot be null.", "key");
			}

			internal static Exception KeyIsTooBig(Slice key)
			{
				return new ArgumentException(String.Format("Key is too big ({0} > {1}).", key.Count, Fdb.MaxKeySize), "key");
			}

			internal static Exception ValueCannotBeNull(Slice value)
			{
				throw new ArgumentException("Value cannot be null", "value");
			}

			internal static Exception ValueIsTooBig(Slice value)
			{
				throw new ArgumentException(String.Format("Value is too big ({0} > {1}).", value.Count, Fdb.MaxValueSize), "value");
			}

			internal static Exception InvalidKeyOutsideDatabaseNamespace(FdbDatabase db, Slice key)
			{
				Contract.Requires(db != null);
				return new FdbException(
					FdbError.KeyOutsideLegalRange,
#if DEBUG
					String.Format("An attempt was made to use a key '{2}' that is outside of the global namespace {0} of database '{1}'", db.GlobalSpace.ToString(), db.Name, key.ToString())
#else
					String.Format("An attempt was made to use a key that is outside of the global namespace {0} of database '{1}'", db.GlobalSpace.ToString(), db.Name)
#endif
				);
			}

			#endregion


			internal static Exception CannotCreateTransactionOnInvalidDatabase()
			{
				return new InvalidOperationException("Cannot create a transaction on an invalid database");
			}

			internal static Exception FailedToRegisterTransactionOnDatabase(IFdbTransaction transaction, FdbDatabase db)
			{
				Contract.Requires(transaction != null && db != null);
				return new InvalidOperationException(String.Format("Failed to register transaction #{0} with this instance of database {1}", transaction.Id, db.Name));
			}

			internal static Exception EndKeyOfRangeCannotBeLessThanBeginKey(FdbKeyRange range)
			{
#if DEBUG
				return new ArgumentException(String.Format("The end key '{0}' of the allowed key space cannot be less than the end key '{1}'", range.Begin.ToString(), range.End.ToString()), "range");
#else
				return new ArgumentException("The end key of the allowed key space cannot be less than the end key", "range");
#endif
			}

			internal static Exception CannotIncrementKey()
			{
				return new OverflowException("Incremented key must contain at least one byte not equal to 0xFF");
			}
		}

	}

}
