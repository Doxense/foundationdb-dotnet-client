#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Encoders
{
	using System;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization.Json;
	using Doxense.Serialization.Json.Binary;

	public sealed class JsonPackValueEncoding : IValueEncoding
	{

		public static JsonPackValueEncoding Instance { get; } = new JsonPackValueEncoding(null, null);

		public JsonPackValueEncoding(CrystalJsonSettings? settings, CrystalJsonTypeResolver? resolver)
		{
			this.Settings = settings ?? CrystalJsonSettings.JsonCompact;
			this.Resolver = resolver ?? CrystalJson.DefaultResolver;
		}

		public CrystalJsonSettings Settings { get; }

		public CrystalJsonTypeResolver Resolver { get; }

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

		public JsonPackValueEncoder<T> GetValueEncoder<T>()
		{
			if (this.Settings.Equals(CrystalJsonSettings.Json) && this.Resolver == CrystalJson.DefaultResolver)
			{
				return JsonPackValueEncoder<T>.Instance;
			}
			return new JsonPackValueEncoder<T>(this.Settings, this.Resolver);
		}
	}

	public sealed class JsonPackValueEncoder<T> : IValueEncoder<T>
	{

		public static JsonPackValueEncoder<T> Instance { get; } = new JsonPackValueEncoder<T>(null, null);

		public CrystalJsonSettings Settings { get; }

		public CrystalJsonTypeResolver Resolver { get; }

		public JsonPackValueEncoder(CrystalJsonSettings? settings, CrystalJsonTypeResolver? resolver)
		{
			this.Settings = settings ?? CrystalJsonSettings.JsonCompact;
			this.Resolver = resolver ?? CrystalJson.DefaultResolver;
		}

		#region JobTicket...

		Slice IValueEncoder<T, Slice>.EncodeValue(T value)
		{
			Contract.NotNullAllowStructs(value);
			return JsonPack.Encode(JsonValue.FromValue<T>(value, this.Settings, this.Resolver), this.Settings);
		}

		T IValueEncoder<T, Slice>.DecodeValue(Slice packed)
		{
			return JsonPack.Decode(packed, this.Settings).As<T>(required: true, resolver: this.Resolver)!;
		}

		#endregion

	}

}
