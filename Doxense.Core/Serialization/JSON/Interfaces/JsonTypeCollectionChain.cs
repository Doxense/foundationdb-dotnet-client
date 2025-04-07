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

	/// <summary>Collection that aggregates multiple smaller collections into a single chain</summary>
	public sealed class JsonTypeCollectionChain : IJsonTypeCollection
	{

		/// <summary>Creates a new empty chain of type collections</summary>
		[Pure]
		public static JsonTypeCollectionChain Create() => new();

		/// <summary>Creates a new chain with the specified type collections</summary>
		/// <param name="collections">List of collections, in the same order as they would be appended to the chain using <see cref="Append"/></param>
		[Pure]
		public static JsonTypeCollectionChain Create(ReadOnlySpan<IJsonTypeCollection> collections)
		{
			var chain = new JsonTypeCollectionChain();
			foreach (var collection in collections)
			{
				chain = chain.Append(collection);
			}
			return chain;
		}

		/// <summary>Link in the chain</summary>
		[DebuggerDisplay("Collection={Collection.GetType().FullName}")]
		private sealed class ChainLink
		{

			/// <summary>Collection of this link</summary>
			public required IJsonTypeCollection Collection { get; init; }

			/// <summary>Next collection in the list, or <c>null</c> if this is the end of the list</summary>
			public required ChainLink? Next { get; init; }
		
		}

		/// <summary>Head of the chain</summary>
		private ChainLink? Head { get; set; }

#if NET9_0_OR_GREATER
		private System.Threading.Lock Lock { get; } = new();
#else
		private object Lock { get; } = new();
#endif

		/// <summary>Adds a new type collection to this chain</summary>
		/// <param name="collection">Collection of types</param>
		/// <returns>The same chain instance</returns>
		/// <remarks>The order of appends is important for conflict resolution in case the same type is present in multiple collections. Collections are evaluated to last to first, <paramref name="collection"/> will have priority against any previously added collection.</remarks>
		public JsonTypeCollectionChain Append(IJsonTypeCollection collection)
		{
			//TODO: check that this is not already added?
			lock (this.Lock)
			{
				this.Head = new() { Collection = collection, Next = this.Head };
			}
			return this;
		}

		/// <inheritdoc />
		public bool TryGetConverterFor(Type type, [MaybeNullWhen(false)] out IJsonConverter converter)
		{
			var current = this.Head;
			while (current != null)
			{
				if (current.Collection.TryGetConverterFor(type, out converter))
				{
					return true;
				}

				current = current.Next;
			}

			converter = null;
			return false;
		}

		/// <inheritdoc />
		public bool TryGetConverterFor<T>([MaybeNullWhen(false)] out IJsonConverter<T> converter)
		{
			var current = this.Head;
			while(current != null)
			{
				if (current.Collection.TryGetConverterFor<T>(out converter))
				{
					return true;
				}
				current = current.Next;
			}
			converter = null;
			return false;
		}

	}

	internal sealed class RuntimeJsonConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] T> : IJsonConverter<T>
	{

		/// <summary>Default converter for instances of type <typeparamref name="T"/></summary>
		public static readonly IJsonConverter<T> Default = new RuntimeJsonConverter<T>();

		/// <inheritdoc />
		public IJsonTypeCollection? GetTypeCollection() => null; //TODO: return a common singleton?

		/// <inheritdoc />
		public Type GetTargetType() => typeof(T);

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

		/// <inheritdoc />
		public bool TryMapMemberToPropertyName(string memberName, [MaybeNullWhen(false)] out string propertyName)
		{
			var def = CrystalJson.DefaultResolver.ResolveMemberOfType(typeof(T), memberName);
			if (def == null)
			{
				propertyName = null;
				return false;
			}
			propertyName = def.Name;
			return true;
		}

		/// <inheritdoc />
		public bool TryMapPropertyToMemberName(string propertyName, [MaybeNullWhen(false)] out string memberName)
		{
			var def = CrystalJson.DefaultResolver.ResolveJsonType(typeof(T));
			if (def != null)
			{
				foreach (var member in def.Members)
				{
					if (member.Name == propertyName)
					{
						memberName = member.OriginalName;
						return true;
					}
				}
			}

			memberName = null;
			return false;
		}

	}

}
