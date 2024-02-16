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
	using System;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization.Json;
	using Doxense.Serialization.Json.Binary;

	public sealed class JsonPackValueEncoding : IValueEncoding
	{

		public static JsonPackValueEncoding Instance { get; } = new(null, null);

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

		public static JsonPackValueEncoder<T> Instance { get; } = new(null, null);

		public CrystalJsonSettings Settings { get; }

		public CrystalJsonTypeResolver Resolver { get; }

		public JsonPackValueEncoder(CrystalJsonSettings? settings, CrystalJsonTypeResolver? resolver)
		{
			this.Settings = settings ?? CrystalJsonSettings.JsonCompact;
			this.Resolver = resolver ?? CrystalJson.DefaultResolver;
		}

		#region JobTicket...

		Slice IValueEncoder<T, Slice>.EncodeValue(T? value)
		{
			Contract.NotNullAllowStructs(value);
			return JsonPack.Encode(JsonValue.FromValue<T>(value, this.Settings, this.Resolver), this.Settings);
		}

		T? IValueEncoder<T, Slice>.DecodeValue(Slice packed)
		{
			return JsonPack.Decode(packed, this.Settings).As<T>(required: true, resolver: this.Resolver);
		}

		#endregion

	}

}
