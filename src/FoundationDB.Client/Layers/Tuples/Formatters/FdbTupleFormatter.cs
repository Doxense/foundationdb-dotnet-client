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

namespace FoundationDB.Layers.Tuples
{
	using System;

	/// <summary>Helper class to get or create tuple formatters</summary>
	public static class FdbTupleFormatter<T>
	{
		private static ITupleFormatter<T> s_default;

		/// <summary>Return the default tuple formatter for this type</summary>
		public static ITupleFormatter<T> Default
		{
			get
			{
				var formatter = s_default;
				if (formatter == null)
				{
					formatter = CreateDefaultFormatter();
					s_default = formatter;
				}
				return formatter;
			}
		}

		/// <summary>Create a custom formatter using the provided lambda functions for convert to and from a tuple</summary>
		/// <param name="from">Lambda that is called to convert a value into a tuple. It SHOULD NOT return null.</param>
		/// <param name="to">Lambda that is called to convert a tuple back into a value. It CAN return null.</param>
		/// <returns>Custom formatter</returns>
		public static ITupleFormatter<T> Create(Func<T, IFdbTuple> from, Func<IFdbTuple, T> to)
		{
			return new FdbAnonymousTupleFormatter<T>(from, to);
		}

		/// <summary>Create a formatter that just add or remove a prefix to values</summary>
		public static ITupleFormatter<T> CreateAppender(IFdbTuple prefix)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");

			return new FdbAnonymousTupleFormatter<T>(
				(value) => prefix.Append<T>(value),
				(tuple) =>
				{
					if (tuple.Count != prefix.Count + 1) throw new ArgumentException("Tuple size is invalid", "tuple");
					if (!FdbTuple.StartsWith(tuple, prefix)) throw new ArgumentException("Tuple does not start with the expected prefix", "tuple");
					return tuple.Last<T>();
				}
			);
		}


		/// <summary>Creates and instance of a tuple formatter that is best suited for this type</summary>
		private static ITupleFormatter<T> CreateDefaultFormatter()
		{
			var type = typeof(T);

			if (typeof(IFdbTuple).IsAssignableFrom(type))
			{
				return new FdbAnonymousTupleFormatter<T>((x) => (IFdbTuple)x, (x) => (T)x);
			}

			if (typeof(ITupleFormattable).IsAssignableFrom(type))
			{
				// note: we cannot call directlty 'new FormattableFormatter<T>()' because of the generic type constraints, so we have to use reflection...
				// => this WILL fail if someone implements 'ITupleFormattable' on a class that does not have public parameterless constructor !
				return (ITupleFormatter<T>)Activator.CreateInstance(typeof(FdbFormattableTupleFormatter<>).MakeGenericType(type));
			}

			return new FdbGenericTupleFormatter<T>();
		}

	}

}
