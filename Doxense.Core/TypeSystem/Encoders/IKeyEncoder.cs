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

namespace Doxense.Serialization.Encoders
{
	using Doxense.Memory;

	/// <summary>Base interface for all key encoders</summary>
	[PublicAPI]
	public interface IKeyEncoder
	{

		/// <summary>Parent encoding</summary>
		IKeyEncoding Encoding { get; }

	}

	/// <summary>Encoder that can serialize and deserialize keys using a binary encoding</summary>
	/// <typeparam name="TKey">Type of the key</typeparam>
	[PublicAPI]
	public interface IKeyEncoder<TKey> : IKeyEncoder
	{

		/// <summary>Encodes a single value to an output buffer</summary>
		void WriteKeyTo(ref SliceWriter writer, TKey? value);

		/// <summary>Decodes a single value from an input buffer</summary>
		void ReadKeyFrom(ref SliceReader reader, out TKey? value);

		/// <summary>Tries to decode a single value from an input buffer</summary>
		bool TryReadKeyFrom(ref SliceReader reader, out TKey? value);

	}

	/// <summary>Encoder for a key composed of a single part</summary>
	/// <typeparam name="TKey">Type of the key</typeparam>
	[PublicAPI]
	public sealed class KeyEncoder<TKey> : IKeyEncoder<TKey>, IKeyEncoding
	{

		/// <summary>Creates an encoded that will invoke the specified <paramref name="pack"/> and <paramref name="unpack"/> lamba functions</summary>
		public KeyEncoder(Func<TKey?, Slice> pack, Func<Slice, TKey?> unpack)
		{
			this.Pack = pack;
			this.Unpack = unpack;
		}

		private Func<TKey?, Slice> Pack { get; }

		private Func<Slice, TKey?> Unpack { get; }

		#region KeyEncoding...

		/// <inheritdoc />
		IKeyEncoder<TOther> IKeyEncoding.GetKeyEncoder<TOther>()
		{
			var type = typeof(TOther);
			if (type != typeof(TKey))
			{
				throw ErrorKeyTypeNotSupported(type);
			}
			return (IKeyEncoder<TOther>) (object) this;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static NotSupportedException ErrorKeyTypeNotSupported(Type type)
		{
			return new NotSupportedException($"This custom encoding is intended for type {typeof(TKey).GetFriendlyName()} and cannot process keys of type {type.GetFriendlyName()}.");
		}

		/// <inheritdoc />
		ICompositeKeyEncoder<T1, T2> IKeyEncoding.GetKeyEncoder<T1, T2>() => throw new NotSupportedException();

		/// <inheritdoc />
		ICompositeKeyEncoder<T1, T2, T3> IKeyEncoding.GetKeyEncoder<T1, T2, T3>() => throw new NotSupportedException();

		/// <inheritdoc />
		ICompositeKeyEncoder<T1, T2, T3, T4> IKeyEncoding.GetKeyEncoder<T1, T2, T3, T4>() => throw new NotSupportedException();

		/// <inheritdoc />
		IDynamicKeyEncoder IKeyEncoding.GetDynamicKeyEncoder() => throw new NotSupportedException();

		#endregion

		#region KeyEncoder...

		/// <inheritdoc />
		public IKeyEncoding Encoding => this;

		/// <inheritdoc />
		public void WriteKeyTo(ref SliceWriter writer, TKey? value)
		{
			writer.WriteBytes(this.Pack(value));
		}

		/// <inheritdoc />
		public void ReadKeyFrom(ref SliceReader reader, out TKey? value)
		{
			value = this.Unpack(reader.ReadToEnd());
		}

		/// <inheritdoc />
		public bool TryReadKeyFrom(ref SliceReader reader, out TKey? value)
		{
			try
			{
				value = this.Unpack(reader.ReadToEnd());
				return true;
			}
			catch (FormatException)
			{
				value = default;
				return false;
			}
		}

		#endregion

	}

}
