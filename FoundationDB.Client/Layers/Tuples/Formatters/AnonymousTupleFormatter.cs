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

namespace Doxense.Collections.Tuples
{
	using System;
	using JetBrains.Annotations;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Customer formatter that will called the provided lambda functions to convert to and from a tuple</summary>
	public sealed class AnonymousTupleFormatter<T> : ITupleFormatter<T>
	{
		private readonly Func<T, ITuple> m_to;
		private readonly Func<ITuple, T> m_from;

		public AnonymousTupleFormatter([NotNull] Func<T, ITuple> to, [NotNull] Func<ITuple, T> from)
		{
			Contract.NotNull(to, nameof(to));
			Contract.NotNull(from, nameof(from));

			m_to = to;
			m_from = from;
		}

		public ITuple ToTuple(T key)
		{
			return m_to(key);
		}

		public T FromTuple(ITuple tuple)
		{
			Contract.NotNull(tuple, nameof(tuple));
			return m_from(tuple);
		}
	}

}
