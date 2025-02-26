#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Serialization.Json
{

	/// <summary>Bundle interface that is implemented by source-generated encoders</summary>
	/// <remarks>
	/// <para>This interfaces bundles <see cref="IJsonSerializer{T}"/>, <see cref="IJsonDeserializer{T}"/> and <see cref="IJsonPacker{T}"/>,
	/// so that it is easier to provide APIs that take a single instance, that is able to both encode and decode JSON without needing two or three different parameters.</para>
	/// </remarks>
	public interface IJsonConverter<T> : IJsonSerializer<T>, IJsonDeserializer<T>, IJsonPacker<T>
	{

	}

	public interface IJsonReadOnlyConverter<TValue, out TReadOnlyProxy> : IJsonConverter<TValue>
		where TReadOnlyProxy : IJsonReadOnlyProxy<TValue, TReadOnlyProxy>
	{
		TReadOnlyProxy AsReadOnly(JsonValue value);

		TReadOnlyProxy AsReadOnly(TValue instance);
	}

	public interface IJsonMutableConverter<TValue, out TMutableProxy> : IJsonConverter<TValue>
		where TMutableProxy : IJsonMutableProxy<TValue, TMutableProxy>
	{
		TMutableProxy ToMutable(JsonValue value);

		TMutableProxy ToMutable(TValue instance);

	}

	public interface IJsonObservableConverter<TValue, out TReadOnlyProxy, out TObservableProxy> : IJsonReadOnlyConverter<TValue, TReadOnlyProxy>
		where TReadOnlyProxy : IJsonReadOnlyProxy<TValue, TReadOnlyProxy>
		where TObservableProxy : IJsonObservableProxy<TValue, TObservableProxy>
	{
		//TObservableProxy ToObservable(ObservableJsonValue value);

		//TObservableProxy ToObservable(TValue value);

	}

	/// <summary>Bundle interface that is implemented by source-generated encoders</summary>
	/// <typeparam name="TValue"></typeparam>
	/// <typeparam name="TReadOnlyProxy"></typeparam>
	/// <typeparam name="TMutableProxy"></typeparam>
	/// <typeparam name="TObservableProxy"></typeparam>
	public interface IJsonConverter<TValue, out TReadOnlyProxy, out TMutableProxy, out TObservableProxy> :
		IJsonConverter<TValue>,
		IJsonReadOnlyConverter<TValue, TReadOnlyProxy>,
		IJsonMutableConverter<TValue, TMutableProxy>,
		IJsonObservableConverter<TValue, TReadOnlyProxy, TObservableProxy>
		where TReadOnlyProxy : IJsonReadOnlyProxy<TValue, TReadOnlyProxy>
		where TMutableProxy : IJsonMutableProxy<TValue, TMutableProxy>
		where TObservableProxy : IJsonObservableProxy<TValue, TObservableProxy>
	{
	}

	internal sealed class DefaultJsonConverter<T> : IJsonConverter<T>
	{

		public Action<CrystalJsonWriter, T?> SerializeHandler { get; }

		public Func<JsonValue, ICrystalJsonTypeResolver, T> DeserializeHandler { get; }

		public Func<T, CrystalJsonSettings, ICrystalJsonTypeResolver, JsonValue> PackHandler { get; }

		public DefaultJsonConverter(
			Action<CrystalJsonWriter, T?>? serializeHandler,
			Func<JsonValue, ICrystalJsonTypeResolver, T>? deserializeHandler,
			Func<T, CrystalJsonSettings, ICrystalJsonTypeResolver, JsonValue>? packHandler
		)
		{
			this.SerializeHandler = serializeHandler ?? (static (_, _) => throw new NotSupportedException("Operation not supported"));
			this.DeserializeHandler = deserializeHandler ?? (static (_, _) => throw new NotSupportedException("Operation not supported")); ;
			this.PackHandler = packHandler ?? (static (_, _, _) => throw new NotSupportedException("Operation not supported"));
		}

		/// <inheritdoc />
		public void Serialize(CrystalJsonWriter writer, T? instance) => this.SerializeHandler(writer, instance);

		/// <inheritdoc />
		public T Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver = null)
		{
			return this.DeserializeHandler(value, resolver ?? CrystalJson.DefaultResolver);
		}

		/// <inheritdoc />
		public JsonValue Pack(T instance, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return this.PackHandler(instance, settings ?? CrystalJsonSettings.Json, resolver ?? CrystalJson.DefaultResolver);
		}
	}

	internal sealed class RuntimeJsonConverter<T> : IJsonConverter<T>
	{

		/// <summary>Default converter for instances of type <typeparamref name="T"/></summary>
		public static readonly IJsonConverter<T> Default = new RuntimeJsonConverter<T>();

		/// <inheritdoc />
		public void Serialize(CrystalJsonWriter writer, T? instance)
		{
			if (instance is null)
			{
				writer.WriteNull();
			}
			else
			{
				CrystalJsonVisitor.VisitValue<T>(instance, writer);
			}
		}

		/// <inheritdoc />
		public T Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver = null)
		{
			return value.As<T>(default, resolver)!;
		}

		/// <inheritdoc />
		public JsonValue Pack(T instance, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return JsonValue.FromValue<T>(instance, settings, resolver);
		}

	}

}
