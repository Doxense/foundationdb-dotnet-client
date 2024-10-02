#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json.Encoders
{
	using System.Diagnostics.CodeAnalysis;
	using Doxense.Collections.Tuples;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;

	/// <summary>Codec that encodes <see cref="JsonValue"/> instances into either database keys (ordered) or values (unordered)</summary>
	public sealed class JsonValueCodec : IKeyEncoder<JsonValue>, IValueEncoder<JsonValue>, IKeyEncoding
	{
		private static readonly CrystalJsonSettings s_defaultSettings = CrystalJsonSettings.JsonCompact.WithEnumAsStrings().WithIso8601Dates();

		public static readonly JsonValueCodec Default = new JsonValueCodec();

		public JsonValueCodec()
			: this(null, null)
		{ }

		public JsonValueCodec(CrystalJsonSettings settings)
			: this(settings, null)
		{ }

		public JsonValueCodec(CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			this.Settings = settings ?? s_defaultSettings;
			this.Resolver = resolver ?? CrystalJson.DefaultResolver;
		}

		public CrystalJsonSettings Settings { get; }

		public ICrystalJsonTypeResolver Resolver { get; }

		/// <summary>Encode une valeur JSON en un tuple</summary>
		/// <param name="prefix"></param>
		/// <param name="value">Valeur JSON de type quelconque</param>
		/// <returns>Tuple contenant la (ou les) valeurs tu tuple</returns>
		/// <remarks>Certaines valeurs (comme des JsonArray) peuvent retourner un embedded tuple!</remarks>
		public static IVarTuple Append(IVarTuple prefix, JsonValue? value)
		{
			value ??= JsonNull.Null;
			switch (value.Type)
			{
				case JsonType.Null:
				{
					return prefix.Append(default(string));
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
					var num = (JsonNumber)value;
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
					IVarTuple tuple = STuple.Empty;
					foreach (var item in (JsonArray) value)
					{
						tuple = Append(tuple, item);
					}
					return prefix.Append<IVarTuple>(tuple);
				}
				case JsonType.DateTime:
				{
					// note: pour des raison de round-tripping, on va encoder les dates en string
					return prefix.Append<string>(value.ToString());
				}
				default:
				{
					throw new NotSupportedException($"Cannot convert JSON value of type {value.Type} into a tuple");
				}
			}
		}

		public static IVarTuple ToTuple(JsonValue? value)
		{
			return Append(STuple.Empty, value);
		}

		public static JsonValue DecodeToJson(object? o)
		{
			if (o is IVarTuple tuple)
			{
				return tuple.ToJsonArray((item) => DecodeToJson(item));
			}
			else
			{
				return JsonValue.FromValue(o);
			}
		}

		/// <summary>Décode le premier élément d'un tuple contenant une valeur JSON</summary>
		/// <param name="tuple">Tuple dont le début contient un valeur JSON</param>
		/// <param name="remaining">Retourne le reste du tuple, moins les données consommées pour décoder la valeur, ou null</param>
		/// <returns></returns>
		public static JsonValue DecodeNext(IVarTuple tuple, out IVarTuple? remaining)
		{
			Contract.NotNull(tuple);

			//TODO: si on voulait optimiser le décodage des tuples (sans les boxer), il faudrait une API sur FdbSlicedTuple pour scanner chaque token!
			if (tuple.Count == 0)
			{
				remaining = null;
				return JsonNull.Missing;
			}
			else
			{
				remaining = tuple.Count > 1 ? tuple.Substring(1) : null;
				return DecodeToJson(tuple[0]);
			}
		}

		/// <summary>Décode le premier élément d'un tuple contenant une valeur JSON</summary>
		/// <param name="tuple">Tuple dont le début contient un valeur JSON</param>
		/// <returns></returns>
		public static JsonValue DecodeNext(IVarTuple tuple, int offset)
		{
			Contract.NotNull(tuple);

			//TODO: si on voulait optimiser le décodage des tuples (sans les boxer), il faudrait une API sur FdbSlicedTuple pour scanner chaque token!
			if (tuple.Count <= offset)
			{
				return JsonNull.Missing;
			}
			else
			{
				return DecodeToJson(tuple[offset]);
			}
		}

		public void WriteKeyTo(ref SliceWriter writer, JsonValue? value)
		{
			TuPack.PackTo(ref writer, ToTuple(value));
		}

		public void ReadKeyFrom(ref SliceReader reader, out JsonValue value)
		{
			IVarTuple? tuple = TuPack.Unpack(reader.ReadToEnd());
			Contract.Debug.Assert(tuple != null);
			value = DecodeNext(tuple, out tuple);
			if (tuple != null) throw new FormatException("Found extra items at the encoded of the encoded JSON value");
		}

		public bool TryReadKeyFrom(ref SliceReader reader, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (!TuPack.TryUnpack(reader.ReadToEnd(), out var tuple))
			{
				value = null;
				return false;
			}
			Contract.Debug.Assert(tuple != null);
			value = DecodeNext(tuple, out tuple);
			return tuple is null;
		}

		public Slice EncodeValue(JsonValue? value)
		{
			return value is null ? Slice.Nil
				: value.IsNull ? Slice.Empty
				: value.ToJsonSlice(CrystalJsonSettings.JsonCompact);
		}

		public JsonValue DecodeValue(Slice value)
		{
			return value.Count == 0
				? (value.IsNull ? JsonNull.Missing : JsonNull.Null)
				: CrystalJson.Parse(value);
		}

		public IKeyEncoding Encoding => this;

		#region IKeyEncoding...

		IDynamicKeyEncoder IKeyEncoding.GetDynamicKeyEncoder() => throw new NotSupportedException();

		IKeyEncoder<T1> IKeyEncoding.GetKeyEncoder<T1>()
		{
			if (typeof(T1) == typeof(JsonValue)) return (IKeyEncoder<T1>) (object) this;
			throw new NotSupportedException();
		}

		ICompositeKeyEncoder<T1, T2> IKeyEncoding.GetKeyEncoder<T1, T2>() => throw new NotSupportedException();

		ICompositeKeyEncoder<T1, T2, T3> IKeyEncoding.GetKeyEncoder<T1, T2, T3>() => throw new NotSupportedException();

		ICompositeKeyEncoder<T1, T2, T3, T4> IKeyEncoding.GetKeyEncoder<T1, T2, T3, T4>() => throw new NotSupportedException();

		#endregion
	}

}
