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

namespace Doxense.Serialization.Encoders
{
	using Doxense.Collections.Tuples;
	using Doxense.Collections.Tuples.Encoding;
	using Doxense.Memory;
	using Doxense.Serialization.Json;

	public sealed class JsonValueEncoding : IValueEncoding, IDynamicKeyEncoding
	{

		public static JsonValueEncoding Instance { get; } = new(null, null);

		public JsonValueEncoding(CrystalJsonSettings? settings, CrystalJsonTypeResolver? resolver)
		{
			this.Settings = settings ?? CrystalJsonSettings.JsonCompact.WithEnumAsStrings().WithIso8601Dates();
			this.Resolver = resolver ?? CrystalJson.DefaultResolver;
		}

		public CrystalJsonSettings Settings { get; }

		public CrystalJsonTypeResolver Resolver { get; }

		#region IKeyEncoding...

		IKeyEncoder<TKey> IKeyEncoding.GetKeyEncoder<TKey>() => GetKeyEncoder<TKey>();

		[Pure]
		public JsonKeyEncoder<T> GetKeyEncoder<T>()
		{
			if (this.Settings.Equals(CrystalJsonSettings.Json) && this.Resolver == CrystalJson.DefaultResolver)
			{
				return JsonKeyEncoder<T>.Instance;
			}
			return new JsonKeyEncoder<T>(this);
		}

		ICompositeKeyEncoder<T1, T2> IKeyEncoding.GetKeyEncoder<T1, T2>()
			=> throw new NotSupportedException();

		ICompositeKeyEncoder<T1, T2, T3> IKeyEncoding.GetKeyEncoder<T1, T2, T3>()
			=> throw new NotSupportedException();

		ICompositeKeyEncoder<T1, T2, T3, T4> IKeyEncoding.GetKeyEncoder<T1, T2, T3, T4>()
			=> throw new NotSupportedException();

		IDynamicKeyEncoder IKeyEncoding.GetDynamicKeyEncoder() => GetDynamicKeyEncoder();

		[Pure]
		public JsonDynamicKeyEncoder GetDynamicKeyEncoder()
		{
			if (this.Settings.Equals(CrystalJsonSettings.Json) && this.Resolver == CrystalJson.DefaultResolver)
			{
				return JsonDynamicKeyEncoder.Instance;
			}
			return new JsonDynamicKeyEncoder(this);
		}

		#endregion

		#region IValueEncoding...

		IValueEncoder<TValue, TStorage> IValueEncoding.GetValueEncoder<TValue, TStorage>()
		{
			if (typeof(TStorage) == typeof(Slice)
			 || typeof(TStorage) == typeof(string)
			 || typeof(TStorage) == typeof(JsonObject))
			{
				return (IValueEncoder<TValue, TStorage>) (object) GetValueEncoder<TValue>();
			}
			throw new NotSupportedException();
		}

		IValueEncoder<TValue> IValueEncoding.GetValueEncoder<TValue>() => GetValueEncoder<TValue>();

		public JsonValueEncoder<T> GetValueEncoder<T>() where T : notnull
		{
			if (this.Settings.Equals(CrystalJsonSettings.Json) && this.Resolver == CrystalJson.DefaultResolver)
			{
				return JsonValueEncoder<T>.Instance;
			}
			return new JsonValueEncoder<T>(this.Settings, this.Resolver);
		}

		#endregion

		/// <summary>Encode une valeur JSON en un tuple</summary>
		/// <param name="prefix"></param>
		/// <param name="value">Valeur JSON de type quelconque</param>
		/// <returns>Tuple contenant la (ou les) valeurs tu tuple</returns>
		/// <remarks>Certaines valeurs (comme des JsonArray) peuvent retourner un embedded tuple!</remarks>
		public static IVarTuple Append(IVarTuple prefix, JsonValue value)
		{
			Contract.NotNull(value);

			switch (value.Type)
			{
				case JsonType.Null:
				{
					return prefix.Append<string?>(null);
				}
				case JsonType.String:
				{
					//TODO: détecter le DateTime et les Guids ?
					return prefix.Append(value.ToString());
				}
				case JsonType.Number:
				{
					// 123 => (int) 123
					// 12.3 => (double) 12.3
					var num = (JsonNumber) value;
					if (!num.IsDecimal)
					{
						if (num.IsUnsigned)
						{
							return prefix.Append(num.ToUInt64());
						}
						else
						{
							return prefix.Append(num.ToInt64());
						}
					}
					else
					{
						//TODO: auto-detect float vs double ?
						return prefix.Append(num.ToDouble());
					}
				}
				case JsonType.Boolean:
				{
					// False => (int) 0
					// True => (int) 1
					return prefix.Append(value.ToBoolean() ? 1 : 0);
				}
				case JsonType.Array:
				{
					// [1, 2, 3] => (1, 2, 3)
					var tuple = STuple.Empty;
					foreach (var item in (JsonArray) value)
					{
						tuple = Append(tuple, item);
					}
					return prefix.Append(tuple);
				}
				case JsonType.DateTime:
				{
					// note: pour des raison de round-tripping, on va encoder les dates en string
					return prefix.Append(value.ToString());
				}
				default:
				{
					throw new NotSupportedException($"Cannot convert JSON value of type {value.Type} into a tuple");
				}
			}
		}

		[Pure]
		public static IVarTuple ToTuple(JsonValue value)
		{
			return Append(STuple.Empty, value);
		}

		[Pure]
		public IVarTuple ToTuple<T>(T? value)
		{
			return ToTuple(JsonValue.FromValue(value, this.Settings, this.Resolver));
		}

		[Pure]
		public JsonValue DecodeToJson(object? o) => o switch
		{
			null => JsonNull.Null,
			IVarTuple tuple => tuple.ToJsonArray(DecodeToJson),
			_ => JsonValue.FromValue(o, this.Settings, this.Resolver)
		};

		/// <summary>Décode le premier élément d'un tuple contenant une valeur JSON</summary>
		/// <param name="tuple">Tuple dont le début contient un valeur JSON</param>
		/// <param name="remaining">Retourne le reste du tuple, moins les données consommées pour décoder la valeur, ou null</param>
		/// <returns></returns>
		public JsonValue DecodeNext(IVarTuple tuple, out IVarTuple? remaining)
		{
			Contract.NotNull(tuple);

			//TODO: si on voulait optimiser le décodage des tuples (sans les boxer), il faudrait une API sur FdbSlicedTuple pour scanner chaque token!
			if (tuple.Count == 0)
			{
				remaining = null;
				return JsonNull.Missing;
			}

			remaining = tuple.Count > 1 ? tuple.Substring(1) : null;
			return DecodeToJson(tuple[0]);
		}

	}

	public sealed class JsonValueEncoder<T> : IValueEncoder<T>, IValueEncoder<T, string>, IValueEncoder<T, JsonObject> where T : notnull
	{

		public static JsonValueEncoder<T> Instance { get; } = new(null, null);

		public CrystalJsonSettings Settings { get; }

		public CrystalJsonTypeResolver Resolver { get; }

		public JsonValueEncoder(CrystalJsonSettings? settings, CrystalJsonTypeResolver? resolver)
		{
			this.Settings = settings ?? CrystalJsonSettings.JsonCompact;
			this.Resolver = resolver ?? CrystalJson.DefaultResolver;
		}

		string IValueEncoder<T, string>.EncodeValue(T? value)
		{
			Contract.NotNull(value);
			return CrystalJson.Serialize(value, this.Settings, this.Resolver);
		}

		T IValueEncoder<T, string>.DecodeValue(string packed)
		{
			Contract.NotNull(packed);
			return CrystalJson.Deserialize<T>(packed, this.Settings, this.Resolver);
		}

		Slice IValueEncoder<T, Slice>.EncodeValue(T? value)
		{
			Contract.NotNull(value);
			return CrystalJson.ToSlice(value, this.Settings, this.Resolver);
		}

		T? IValueEncoder<T, Slice>.DecodeValue(Slice packed)
		{
			return packed.IsNullOrEmpty ? default : CrystalJson.Deserialize<T>(packed, this.Settings, this.Resolver);
		}

		JsonObject IValueEncoder<T, JsonObject>.EncodeValue(T? value)
		{
			Contract.NotNull(value);
			return JsonObject.FromObject(value, this.Settings, this.Resolver);
		}

		T? IValueEncoder<T, JsonObject>.DecodeValue(JsonObject packed)
		{
			return packed.As<T?>(resolver: this.Resolver);
		}

	}

	public sealed class JsonKeyEncoder<T> : IKeyEncoder<T>
	{

		public static JsonKeyEncoder<T> Instance { get; } = new(JsonValueEncoding.Instance);

		public JsonValueEncoding Encoding { get; }

		IKeyEncoding IKeyEncoder.Encoding => this.Encoding;

		public CrystalJsonSettings Settings => this.Encoding.Settings;

		public CrystalJsonTypeResolver Resolver => this.Encoding.Resolver;

		public JsonKeyEncoder(JsonValueEncoding encoding)
		{
			this.Encoding = encoding;
		}

		#region IKeyEncoder<T>

		public void WriteKeyTo(ref SliceWriter writer, T? value)
		{
			TuPack.PackTo(ref writer, this.Encoding.ToTuple(value));
		}

		public void ReadKeyFrom(ref SliceReader reader, out T? value)
		{
			var tuple = TuPack.Unpack(reader.ReadToEnd());
			Contract.Debug.Assert(tuple != null);
			value = this.Encoding.DecodeNext(tuple, out tuple).As<T>();
			if (tuple != null)
			{
				throw new FormatException("Found extra items at the encoded of the encoded JSON value");
			}
		}

		public bool TryReadKeyFrom(ref SliceReader reader, out T? value)
		{
			if (!TuPack.TryUnpack(reader.ReadToEnd(), out var tuple))
			{
				value = default;
				return false;
			}
			Contract.Debug.Assert(tuple != null);
			value = this.Encoding.DecodeNext(tuple, out var remainder).As<T>();
			return remainder is null;
		}

		#endregion

	}

	public sealed class JsonDynamicKeyEncoder : IDynamicKeyEncoder
	{

		public static JsonDynamicKeyEncoder Instance { get; } = new(JsonValueEncoding.Instance);

		public JsonDynamicKeyEncoder(JsonValueEncoding encoding)
		{
			this.Encoding = encoding;
		}

		IKeyEncoding IKeyEncoder.Encoding => this.Encoding;

		IDynamicKeyEncoding IDynamicKeyEncoder.Encoding => this.Encoding;

		public JsonValueEncoding Encoding { get; }

		public CrystalJsonSettings Settings => this.Encoding.Settings;

		public ICrystalJsonTypeResolver Resolver => this.Encoding.Resolver;

		#region IDynamicKeyEncoder...

#pragma warning disable IL2095

		void IDynamicKeyEncoder.PackKey<TTuple>(ref SliceWriter writer, TTuple items)
			=> throw new NotSupportedException();

		void IDynamicKeyEncoder.EncodeKey<T1>(ref SliceWriter writer, T1? item1)
			where T1 : default
			=> throw new NotSupportedException();

		void IDynamicKeyEncoder.EncodeKey<T1, T2>(ref SliceWriter writer, T1? item1, T2? item2)
			where T1 : default
			where T2 : default
			=> throw new NotSupportedException();

		void IDynamicKeyEncoder.EncodeKey<T1, T2, T3>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3)
			where T1 : default
			where T2 : default
			where T3 : default
			=> throw new NotSupportedException();

		void IDynamicKeyEncoder.EncodeKey<T1, T2, T3, T4>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			=> throw new NotSupportedException();

		void IDynamicKeyEncoder.EncodeKey<T1, T2, T3, T4, T5>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			=> throw new NotSupportedException();

		void IDynamicKeyEncoder.EncodeKey<T1, T2, T3, T4, T5, T6>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			where T6 : default
			=> throw new NotSupportedException();

		void IDynamicKeyEncoder.EncodeKey<T1, T2, T3, T4, T5, T6, T7>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			where T6 : default
			where T7 : default
			=> throw new NotSupportedException();

		void IDynamicKeyEncoder.EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7, T8? item8)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			where T6 : default
			where T7 : default
			where T8 : default
			=> throw new NotSupportedException();

		IVarTuple IDynamicKeyEncoder.UnpackKey(Slice packed) => throw new NotSupportedException();

		SpanTuple IDynamicKeyEncoder.UnpackKey(ReadOnlySpan<byte> packed) => throw new NotSupportedException();

		bool IDynamicKeyEncoder.TryUnpackKey(Slice packed, out IVarTuple tuple) => throw new NotSupportedException();

		bool IDynamicKeyEncoder.TryUnpackKey(ReadOnlySpan<byte> packed, out SpanTuple tuple) => throw new NotSupportedException();

		T1 IDynamicKeyEncoder.DecodeKey<T1>(Slice packed)
			where T1 : default
			=> throw new NotSupportedException();

		T1 IDynamicKeyEncoder.DecodeKey<T1>(ReadOnlySpan<byte> packed)
			where T1 : default
			=> throw new NotSupportedException();

		T1 IDynamicKeyEncoder.DecodeKeyFirst<T1>(Slice packed, int? expectedSize)
			where T1 : default
			=> throw new NotSupportedException();

		T1 IDynamicKeyEncoder.DecodeKeyFirst<T1>(ReadOnlySpan<byte> packed, int? expectedSize)
			where T1 : default
			=> throw new NotSupportedException();

		(T1?, T2?) IDynamicKeyEncoder.DecodeKeyFirst<T1, T2>(Slice packed, int? expectedSize)
			where T1 : default
			where T2 : default
			=> throw new NotSupportedException();

		(T1?, T2?) IDynamicKeyEncoder.DecodeKeyFirst<T1, T2>(ReadOnlySpan<byte> packed, int? expectedSize)
			where T1 : default
			where T2 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?) IDynamicKeyEncoder.DecodeKeyFirst<T1, T2, T3>(Slice packed, int? expectedSize)
			where T1 : default
			where T2 : default
			where T3 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?) IDynamicKeyEncoder.DecodeKeyFirst<T1, T2, T3>(ReadOnlySpan<byte> packed, int? expectedSize)
			where T1 : default
			where T2 : default
			where T3 : default => throw new NotSupportedException();

		T1 IDynamicKeyEncoder.DecodeKeyLast<T1>(Slice packed, int? expectedSize)
			where T1 : default
			=> throw new NotSupportedException();

		T1 IDynamicKeyEncoder.DecodeKeyLast<T1>(ReadOnlySpan<byte> packed, int? expectedSize)
			where T1 : default
			=> throw new NotSupportedException();

		(T1?, T2?) IDynamicKeyEncoder.DecodeKeyLast<T1, T2>(Slice packed, int? expectedSize)
			where T1 : default
			where T2 : default => throw new NotSupportedException();

		(T1?, T2?) IDynamicKeyEncoder.DecodeKeyLast<T1, T2>(ReadOnlySpan<byte> packed, int? expectedSize)
			where T1 : default
			where T2 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?) IDynamicKeyEncoder.DecodeKeyLast<T1, T2, T3>(Slice packed, int? expectedSize)
			where T1 : default
			where T2 : default
			where T3 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?) IDynamicKeyEncoder.DecodeKeyLast<T1, T2, T3>(ReadOnlySpan<byte> packed, int? expectedSize)
			where T1 : default
			where T2 : default
			where T3 : default
			=> throw new NotSupportedException();

		(T1?, T2?) IDynamicKeyEncoder.DecodeKey<T1, T2>(Slice packed)
			where T1 : default
			where T2 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?) IDynamicKeyEncoder.DecodeKey<T1, T2, T3>(Slice packed)
			where T1 : default
			where T2 : default
			where T3 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?, T4?) IDynamicKeyEncoder.DecodeKey<T1, T2, T3, T4>(Slice packed)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?, T4?, T5?) IDynamicKeyEncoder.DecodeKey<T1, T2, T3, T4, T5>(Slice packed)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?, T4?, T5?, T6?) IDynamicKeyEncoder.DecodeKey<T1, T2, T3, T4, T5, T6>(Slice packed)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			where T6 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?, T4?, T5?, T6?, T7?) IDynamicKeyEncoder.DecodeKey<T1, T2, T3, T4, T5, T6, T7>(Slice packed)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			where T6 : default
			where T7 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) IDynamicKeyEncoder.DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(Slice packed)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			where T6 : default
			where T7 : default
			where T8 : default
			=> throw new NotSupportedException();

		(T1?, T2?) IDynamicKeyEncoder.DecodeKey<T1, T2>(ReadOnlySpan<byte> packed)
			where T1 : default
			where T2 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?) IDynamicKeyEncoder.DecodeKey<T1, T2, T3>(ReadOnlySpan<byte> packed)
			where T1 : default
			where T2 : default
			where T3 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?, T4?) IDynamicKeyEncoder.DecodeKey<T1, T2, T3, T4>(ReadOnlySpan<byte> packed)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?, T4?, T5?) IDynamicKeyEncoder.DecodeKey<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> packed)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?, T4?, T5?, T6?) IDynamicKeyEncoder.DecodeKey<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> packed)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			where T6 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?, T4?, T5?, T6?, T7?) IDynamicKeyEncoder.DecodeKey<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> packed)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			where T6 : default
			where T7 : default
			=> throw new NotSupportedException();

		(T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) IDynamicKeyEncoder.DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> packed)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			where T6 : default
			where T7 : default
			where T8 : default
			=> throw new NotSupportedException();

		(Slice Begin, Slice End) IDynamicKeyEncoder.ToRange(Slice prefix)
			=> throw new NotSupportedException();

		(Slice Begin, Slice End) IDynamicKeyEncoder.ToRange<TTuple>(Slice prefix, TTuple items)
			=> throw new NotSupportedException();

		(Slice Begin, Slice End) IDynamicKeyEncoder.ToKeyRange<T1>(Slice prefix, T1? item1)
			where T1 : default
			=> throw new NotSupportedException();

		(Slice Begin, Slice End) IDynamicKeyEncoder.ToKeyRange<T1, T2>(Slice prefix, T1? item1, T2? item2)
			where T1 : default
			where T2 : default
			=> throw new NotSupportedException();

		(Slice Begin, Slice End) IDynamicKeyEncoder.ToKeyRange<T1, T2, T3>(Slice prefix, T1? item1, T2? item2, T3? item3)
			where T1 : default
			where T2 : default
			where T3 : default
			=> throw new NotSupportedException();

		(Slice Begin, Slice End) IDynamicKeyEncoder.ToKeyRange<T1, T2, T3, T4>(Slice prefix, T1? item1, T2? item2, T3? item3, T4? item4)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			=> throw new NotSupportedException();

		(Slice Begin, Slice End) IDynamicKeyEncoder.ToKeyRange<T1, T2, T3, T4, T5>(Slice prefix, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			=> throw new NotSupportedException();

		(Slice Begin, Slice End) IDynamicKeyEncoder.ToKeyRange<T1, T2, T3, T4, T5, T6>(Slice prefix, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			where T6 : default
			=> throw new NotSupportedException();

		(Slice Begin, Slice End) IDynamicKeyEncoder.ToKeyRange<T1, T2, T3, T4, T5, T6, T7>(Slice prefix, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			where T6 : default
			where T7 : default
			=> throw new NotSupportedException();

		(Slice Begin, Slice End) IDynamicKeyEncoder.ToKeyRange<T1, T2, T3, T4, T5, T6, T7, T8>(Slice prefix, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7, T8? item8)
			where T1 : default
			where T2 : default
			where T3 : default
			where T4 : default
			where T5 : default
			where T6 : default
			where T7 : default
			where T8 : default
			=> throw new NotSupportedException();

#pragma warning restore IL2095

		#endregion

	}

}
