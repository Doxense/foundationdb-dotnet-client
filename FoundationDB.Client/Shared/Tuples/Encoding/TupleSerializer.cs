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

namespace Doxense.Collections.Tuples.Encoding
{
	using System;
	using JetBrains.Annotations;

	public sealed class TupleSerializer<T1> : ITupleSerializer<STuple<T1>>
	{
		public static TupleSerializer<T1> Default { [NotNull] get; } = new TupleSerializer<T1>();

		public void PackTo(ref TupleWriter writer, in STuple<T1> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
		}

		public void UnpackFrom(ref TupleReader reader, out STuple<T1> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}
	}

	public sealed class TupleSerializer<T1, T2> : ITupleSerializer<STuple<T1, T2>>
	{
		public static TupleSerializer<T1, T2> Default { [NotNull] get; } = new TupleSerializer<T1, T2>();

		public void PackTo(ref TupleWriter writer, in STuple<T1, T2> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
			TuplePackers.SerializeTo(ref writer, tuple.Item2);
		}

		public void UnpackFrom(ref TupleReader reader, out STuple<T1, T2> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}
	}

	public sealed class TupleSerializer<T1, T2, T3> : ITupleSerializer<STuple<T1, T2, T3>>
	{
		public static TupleSerializer<T1, T2, T3> Default { [NotNull] get; } = new TupleSerializer<T1, T2, T3>();

		public void PackTo(ref TupleWriter writer, in STuple<T1, T2, T3> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
			TuplePackers.SerializeTo(ref writer, tuple.Item2);
			TuplePackers.SerializeTo(ref writer, tuple.Item3);
		}

		public void UnpackFrom(ref TupleReader reader, out STuple<T1, T2, T3> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}
	}

	public sealed class TupleSerializer<T1, T2, T3, T4> : ITupleSerializer<STuple<T1, T2, T3, T4>>
	{
		public static TupleSerializer<T1, T2, T3, T4> Default { [NotNull] get; } = new TupleSerializer<T1, T2, T3, T4>();

		public void PackTo(ref TupleWriter writer, in STuple<T1, T2, T3, T4> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
			TuplePackers.SerializeTo(ref writer, tuple.Item2);
			TuplePackers.SerializeTo(ref writer, tuple.Item3);
			TuplePackers.SerializeTo(ref writer, tuple.Item4);
		}

		public void UnpackFrom(ref TupleReader reader, out STuple<T1, T2, T3, T4> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}
	}

	public sealed class TupleSerializer<T1, T2, T3, T4, T5> : ITupleSerializer<STuple<T1, T2, T3, T4, T5>>
	{
		public static TupleSerializer<T1, T2, T3, T4, T5> Default { [NotNull] get; } = new TupleSerializer<T1, T2, T3, T4, T5>();

		public void PackTo(ref TupleWriter writer, in STuple<T1, T2, T3, T4, T5> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
			TuplePackers.SerializeTo(ref writer, tuple.Item2);
			TuplePackers.SerializeTo(ref writer, tuple.Item3);
			TuplePackers.SerializeTo(ref writer, tuple.Item4);
			TuplePackers.SerializeTo(ref writer, tuple.Item5);
		}

		public void UnpackFrom(ref TupleReader reader, out STuple<T1, T2, T3, T4, T5> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}
	}

	public sealed class TupleSerializer<T1, T2, T3, T4, T5, T6> : ITupleSerializer<STuple<T1, T2, T3, T4, T5, T6>>
	{
		public static TupleSerializer<T1, T2, T3, T4, T5, T6> Default { [NotNull] get; } = new TupleSerializer<T1, T2, T3, T4, T5, T6>();

		public void PackTo(ref TupleWriter writer, in STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
			TuplePackers.SerializeTo(ref writer, tuple.Item2);
			TuplePackers.SerializeTo(ref writer, tuple.Item3);
			TuplePackers.SerializeTo(ref writer, tuple.Item4);
			TuplePackers.SerializeTo(ref writer, tuple.Item5);
			TuplePackers.SerializeTo(ref writer, tuple.Item6);
		}

		public void UnpackFrom(ref TupleReader reader, out STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}
	}
}
