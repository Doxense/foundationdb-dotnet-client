#region BSD License
/* Copyright (c) 2013-2023 Doxense SAS
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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Serialization.Encoders
{
	using System;
	using Doxense.Memory;

	/// <summary>Base interface for all key encoders</summary>
	public interface IKeyEncoder
	{
		/// <summary>Parent encoding</summary>
		IKeyEncoding Encoding { get; }
	}

	public interface IKeyEncoder<T1> : IKeyEncoder
	{
		/// <summary>Encode a single value</summary>
		void WriteKeyTo(ref SliceWriter writer, T1? value);

		/// <summary>Decode a single value</summary>
		void ReadKeyFrom(ref SliceReader reader, out T1? value);

		bool TryReadKeyFrom(ref SliceReader reader, out T1? value);
	}

	public class KeyEncoder<TKey> : IKeyEncoder<TKey>, IKeyEncoding
	{
		public KeyEncoder(Func<TKey, Slice> pack, Func<Slice, TKey> unpack)
		{
			this.Pack = pack;
			this.Unpack = unpack;
		}

		private Delegate Pack { get; }

		private Delegate Unpack { get; }

		#region KeyEncoding...

		IKeyEncoder<T> IKeyEncoding.GetKeyEncoder<T>()
		{
			if (typeof(T) != typeof(TKey)) throw new NotSupportedException($"This custom encoding can only process keys of type {typeof(TKey).Name}.");
			return (IKeyEncoder<T>) this;
		}

		ICompositeKeyEncoder<T1, T2> IKeyEncoding.GetKeyEncoder<T1, T2>() => throw new NotSupportedException();

		ICompositeKeyEncoder<T1, T2, T3> IKeyEncoding.GetKeyEncoder<T1, T2, T3>() => throw new NotSupportedException();

		ICompositeKeyEncoder<T1, T2, T3, T4> IKeyEncoding.GetKeyEncoder<T1, T2, T3, T4>() => throw new NotSupportedException();

		IDynamicKeyEncoder IKeyEncoding.GetDynamicKeyEncoder() => throw new NotSupportedException();

		#endregion

		#region KeyEncoder...

		public IKeyEncoding Encoding => this;

		public void WriteKeyTo(ref SliceWriter writer, TKey value)
		{
			if (this.Pack is Func<TKey, Slice> f)
			{
				writer.WriteBytes(f(value));
				return;
			}
			throw new InvalidOperationException();
		}

		public void ReadKeyFrom(ref SliceReader reader, out TKey value)
		{
			if (this.Unpack is Func<Slice, TKey> f)
			{
				value = f(reader.ReadToEnd());
				return;
			}
			throw new InvalidOperationException();
		}

		public bool TryReadKeyFrom(ref SliceReader reader, out TKey value)
		{
			if (this.Unpack is Func<Slice, TKey> f)
			{
				try
				{
					value = f(reader.ReadToEnd());
					return true;
				}
				catch (FormatException)
				{
					value = default!;
					return false;
				}
			}

			value = default!;
			return false;

		}

		#endregion
	}
}

#endif
