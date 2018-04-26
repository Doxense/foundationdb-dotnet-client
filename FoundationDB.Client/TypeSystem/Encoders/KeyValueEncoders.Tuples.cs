#region BSD Licence
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

namespace Doxense.Serialization.Encoders
{
	using System;
	using Doxense.Collections.Tuples;
	using Doxense.Collections.Tuples.Encoding;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Helper class for all key/value encoders</summary>
	public static partial class KeyValueEncoders
	{

		/// <summary>Encoders that use the Tuple Encoding, suitable for keys</summary>
		[PublicAPI]
		public static class Tuples
		{

			internal class TupleEncoder<T> : IKeyEncoder<T>, IValueEncoder<T>
			{
				public static readonly TupleEncoder<T> Default = new TupleEncoder<T>();

				private TupleEncoder() { }

				public IKeyEncoding Encoding => TypeSystem.Tuples;

				public void WriteKeyTo(ref SliceWriter writer, T key)
				{
					TupleEncoder.WriteKeysTo(ref writer, key);
				}

				public void ReadKeyFrom(ref SliceReader reader, out T key)
				{
					key = !reader.HasMore
						? default //BUGBUG
						: TuPack.DecodeKey<T>(reader.ReadToEnd());
				}

				public Slice EncodeValue(T key)
				{
					return TupleEncoder.EncodeKey(key);
				}

				public T DecodeValue(Slice encoded)
				{
					if (encoded.IsNullOrEmpty) return default; //BUGBUG
					return TuPack.DecodeKey<T>(encoded);
				}

			}

			internal class TupleCompositeEncoder<T1, T2> : CompositeKeyEncoder<T1, T2>
			{

				public static readonly TupleCompositeEncoder<T1, T2> Default = new TupleCompositeEncoder<T1, T2>();

				private TupleCompositeEncoder() { }

				public override IKeyEncoding Encoding => TypeSystem.Tuples;

				public override void WriteKeyPartsTo(ref SliceWriter writer, int count, ref STuple<T1, T2> key)
				{
					switch (count)
					{
						case 2: TupleEncoder.WriteKeysTo(ref writer, key.Item1, key.Item2); break;
						case 1: TupleEncoder.WriteKeysTo(ref writer, key.Item1); break;
						default: throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be either 1 or 2");
					}
				}

				public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out STuple<T1, T2> key)
				{
					if (count != 1 & count != 2) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be either 1 or 2");

					var t = TuPack.Unpack(reader.ReadToEnd()).OfSize(count);
					Contract.Assert(t != null);
					key = new STuple<T1, T2>(
						t.Get<T1>(0),
						count == 2 ? t.Get<T2>(1) : default
					);
				}
			}

			internal class TupleCompositeEncoder<T1, T2, T3> : CompositeKeyEncoder<T1, T2, T3>
			{

				public static readonly TupleCompositeEncoder<T1, T2, T3> Default = new TupleCompositeEncoder<T1, T2, T3>();

				private TupleCompositeEncoder() { }

				public override IKeyEncoding Encoding => TypeSystem.Tuples;

				public override void WriteKeyPartsTo(ref SliceWriter writer, int count, ref STuple<T1, T2, T3> key)
				{
					switch (count)
					{
						case 3: TupleEncoder.WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3); break;
						case 2: TupleEncoder.WriteKeysTo(ref writer, key.Item1, key.Item2); break;
						case 1: TupleEncoder.WriteKeysTo(ref writer, key.Item1); break;
						default: throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 3");
					}
				}

				public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out STuple<T1, T2, T3> key)
				{
					if (count < 1 | count > 3) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 3");

					var t = TuPack.Unpack(reader.ReadToEnd()).OfSize(count);
					Contract.Assert(t != null);
					key = new STuple<T1, T2, T3>(
						t.Get<T1>(0),
						count >= 2 ? t.Get<T2>(1) : default,
						count >= 3 ? t.Get<T3>(2) : default
					);
				}
			}

			internal class TupleCompositeEncoder<T1, T2, T3, T4> : CompositeKeyEncoder<T1, T2, T3, T4>
			{

				public static readonly TupleCompositeEncoder<T1, T2, T3, T4> Default = new TupleCompositeEncoder<T1, T2, T3, T4>();

				private TupleCompositeEncoder() { }

				public override IKeyEncoding Encoding => TypeSystem.Tuples;

				public override void WriteKeyPartsTo(ref SliceWriter writer, int count, ref STuple<T1, T2, T3, T4> key)
				{
					switch (count)
					{
						case 4: TupleEncoder.WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3, key.Item4); break;
						case 3: TupleEncoder.WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3); break;
						case 2: TupleEncoder.WriteKeysTo(ref writer, key.Item1, key.Item2); break;
						case 1: TupleEncoder.WriteKeysTo(ref writer, key.Item1); break;
						default: throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 4");
					}
				}

				public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out STuple<T1, T2, T3, T4> key)
				{
					if (count < 1 || count > 4) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 4");

					var t = TuPack.Unpack(reader.ReadToEnd()).OfSize(count);
					Contract.Assert(t != null);
					key = new STuple<T1, T2, T3, T4>(
						t.Get<T1>(0),
						count >= 2 ? t.Get<T2>(1) : default,
						count >= 3 ? t.Get<T3>(2) : default,
						count >= 4 ? t.Get<T4>(3) : default
					);
				}
			}

			internal class TupleCompositeEncoder<T1, T2, T3, T4, T5> : CompositeKeyEncoder<T1, T2, T3, T4, T5>
			{

				public static readonly TupleCompositeEncoder<T1, T2, T3, T4, T5> Default = new TupleCompositeEncoder<T1, T2, T3, T4, T5>();

				private TupleCompositeEncoder() { }

				public override IKeyEncoding Encoding => TypeSystem.Tuples;

				public override void WriteKeyPartsTo(ref SliceWriter writer, int count, ref STuple<T1, T2, T3, T4, T5> key)
				{
					switch (count)
					{
						case 5: TupleEncoder.WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3, key.Item4, key.Item5); break;
						case 4: TupleEncoder.WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3, key.Item4); break;
						case 3: TupleEncoder.WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3); break;
						case 2: TupleEncoder.WriteKeysTo(ref writer, key.Item1, key.Item2); break;
						case 1: TupleEncoder.WriteKeysTo(ref writer, key.Item1); break;
						default: throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 5");
					}
				}

				public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out STuple<T1, T2, T3, T4, T5> key)
				{
					if (count < 1 || count > 5) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 5");

					var t = TuPack.Unpack(reader.ReadToEnd()).OfSize(count);
					Contract.Assert(t != null);
					key = new STuple<T1, T2, T3, T4, T5>(
						t.Get<T1>(0),
						count >= 2 ? t.Get<T2>(1) : default,
						count >= 3 ? t.Get<T3>(2) : default,
						count >= 4 ? t.Get<T4>(3) : default,
						count >= 5 ? t.Get<T5>(4) : default
					);
				}
			}

			#region Keys

			[NotNull]
			public static IKeyEncoder<T1> Key<T1>()
			{
				return TupleEncoder<T1>.Default;
			}

			[NotNull]
			public static ICompositeKeyEncoder<T1, T2> CompositeKey<T1, T2>()
			{
				return TupleCompositeEncoder<T1, T2>.Default;
			}

			[NotNull]
			public static ICompositeKeyEncoder<T1, T2, T3> CompositeKey<T1, T2, T3>()
			{
				return TupleCompositeEncoder<T1, T2, T3>.Default;
			}

			[NotNull]
			public static ICompositeKeyEncoder<T1, T2, T3, T4> CompositeKey<T1, T2, T3, T4>()
			{
				return TupleCompositeEncoder<T1, T2, T3, T4>.Default;
			}

			#endregion

			#region Values...

			[NotNull]
			public static IValueEncoder<T> Value<T>()
			{
				return TupleEncoder<T>.Default;
			}

			#endregion

		}

	}

}
