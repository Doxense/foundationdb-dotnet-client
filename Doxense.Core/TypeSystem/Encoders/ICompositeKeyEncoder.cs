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
	
	/// <summary>Encoder that can serialize and deserialize composite keys using a binary encoding</summary>
	[PublicAPI]
	public interface ICompositeKeyEncoder<T1, T2> : IKeyEncoder<(T1?, T2?)>
	{
		/// <summary>Write some or all parts of a composite key</summary>
		void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?) key);

		/// <summary>Read some or all parts of a composite key</summary>
		void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?) items);

		/// <summary>Read some or all parts of a composite key</summary>
		bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?) items);
	}

	/// <summary>Encoder that can serialize and deserialize composite keys using a binary encoding</summary>
	[PublicAPI]
	public interface ICompositeKeyEncoder<T1, T2, T3> : IKeyEncoder<(T1?, T2?, T3?)>
	{
		/// <summary>Write some or all parts of a composite key</summary>
		void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?, T3?) key);

		/// <summary>Read some or all parts of a composite key</summary>
		void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?) items);

		/// <summary>Read some or all parts of a composite key</summary>
		bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?) items);
	}

	/// <summary>Encoder that can serialize and deserialize composite keys using a binary encoding</summary>
	[PublicAPI]
	public interface ICompositeKeyEncoder<T1, T2, T3, T4> : IKeyEncoder<(T1?, T2?, T3?, T4?)>
	{
		/// <summary>Write some or all parts of a composite key</summary>
		void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?, T3?, T4?) key);

		/// <summary>Read some or all parts of a composite key</summary>
		void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?) items);

		/// <summary>Read some or all parts of a composite key</summary>
		bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?) items);

	}

	/// <summary>Encoder that can serialize and deserialize composite keys using a binary encoding</summary>
	[PublicAPI]
	public interface ICompositeKeyEncoder<T1, T2, T3, T4, T5> : IKeyEncoder<(T1?, T2?, T3?, T4?, T5?)>
	{
		/// <summary>Write some or all parts of a composite key</summary>
		void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?, T3?, T4?, T5?) key);

		/// <summary>Read some or all parts of a composite key</summary>
		void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?) items);

		/// <summary>Read some or all parts of a composite key</summary>
		bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?) items);
	}

	/// <summary>Encoder that can serialize and deserialize composite keys using a binary encoding</summary>
	[PublicAPI]
	public interface ICompositeKeyEncoder<T1, T2, T3, T4, T5, T6> : IKeyEncoder<(T1?, T2?, T3?, T4?, T5?, T6?)>
	{
		/// <summary>Write some or all parts of a composite key</summary>
		void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?, T3?, T4?, T5?, T6?) key);

		/// <summary>Read some or all parts of a composite key</summary>
		void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?, T6?) items);

		/// <summary>Read some or all parts of a composite key</summary>
		bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?, T6?) items);
	}

	/// <summary>Wrapper for encoding and decoding a pair with lambda functions</summary>
	[PublicAPI]
	public abstract class CompositeKeyEncoder<T1, T2> : ICompositeKeyEncoder<T1, T2>
	{

		/// <inheritdoc />
		public abstract IKeyEncoding Encoding { get; }

		/// <inheritdoc />
		public abstract void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?) items);

		/// <inheritdoc />
		public abstract void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?) items);

		/// <inheritdoc />
		public abstract bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?) items);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteKeyTo(ref SliceWriter writer, (T1?, T2?) items)
		{
			WriteKeyPartsTo(ref writer, 2, in items);
		}

		/// <inheritdoc />
		public void ReadKeyFrom(ref SliceReader reader, out (T1?, T2?) items)
		{
			ReadKeyPartsFrom(ref reader, 2, out items);
		}

		/// <inheritdoc />
		public bool TryReadKeyFrom(ref SliceReader reader, out (T1?, T2?) items)
		{
			return TryReadKeyPartsFrom(ref reader, 2, out items);
		}

	}

	/// <summary>Wrapper for encoding and decoding a triplet with lambda functions</summary>
	[PublicAPI]
	public abstract class CompositeKeyEncoder<T1, T2, T3> : ICompositeKeyEncoder<T1, T2, T3>
	{

		/// <inheritdoc />
		public abstract IKeyEncoding Encoding { get; }

		/// <inheritdoc />
		public abstract void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?, T3?) items);

		/// <inheritdoc />
		public abstract void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?) items);

		/// <inheritdoc />
		public abstract bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?) items);

		/// <inheritdoc />
		public void WriteKeyTo(ref SliceWriter writer, (T1?, T2?, T3?) items)
		{
			WriteKeyPartsTo(ref writer, 3, in items);
		}

		/// <inheritdoc />
		public void ReadKeyFrom(ref SliceReader reader, out (T1?, T2?, T3?) items)
		{
			ReadKeyPartsFrom(ref reader, 3, out items);
		}

		/// <inheritdoc />
		public bool TryReadKeyFrom(ref SliceReader reader, out (T1?, T2?, T3?) items)
		{
			return TryReadKeyPartsFrom(ref reader, 3, out items);
		}

	}

	/// <summary>Wrapper for encoding and decoding a quad with lambda functions</summary>
	[PublicAPI]
	public abstract class CompositeKeyEncoder<T1, T2, T3, T4> : ICompositeKeyEncoder<T1, T2, T3, T4>
	{

		/// <inheritdoc />
		public abstract IKeyEncoding Encoding { get; }

		/// <inheritdoc />
		public abstract void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?, T3?, T4?) items);

		/// <inheritdoc />
		public abstract void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?) items);

		/// <inheritdoc />
		public abstract bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?) items);

		/// <inheritdoc />
		public void WriteKeyTo(ref SliceWriter writer, (T1?, T2?, T3?, T4?) items)
		{
			WriteKeyPartsTo(ref writer, 4, in items);
		}

		/// <inheritdoc />
		public void ReadKeyFrom(ref SliceReader reader, out (T1?, T2?, T3?, T4?) items)
		{
			ReadKeyPartsFrom(ref reader, 4, out items);
		}

		/// <inheritdoc />
		public bool TryReadKeyFrom(ref SliceReader reader, out (T1?, T2?, T3?, T4?) items)
		{
			return TryReadKeyPartsFrom(ref reader, 4, out items);
		}

	}

	/// <summary>Wrapper for encoding and decoding five items with lambda functions</summary>
	[PublicAPI]
	public abstract class CompositeKeyEncoder<T1, T2, T3, T4, T5> : ICompositeKeyEncoder<T1, T2, T3, T4, T5>
	{

		/// <inheritdoc />
		public abstract IKeyEncoding Encoding { get; }

		/// <inheritdoc />
		public abstract void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?, T3?, T4?, T5?) items);

		/// <inheritdoc />
		public abstract void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?) items);

		/// <inheritdoc />
		public abstract bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?) items);

		/// <inheritdoc />
		public void WriteKeyTo(ref SliceWriter writer, (T1?, T2?, T3?, T4?, T5?) items)
		{
			WriteKeyPartsTo(ref writer, 5, in items);
		}

		/// <inheritdoc />
		public void ReadKeyFrom(ref SliceReader reader, out (T1?, T2?, T3?, T4?, T5?) items)
		{
			ReadKeyPartsFrom(ref reader, 5, out items);
		}

		/// <inheritdoc />
		public bool TryReadKeyFrom(ref SliceReader reader, out (T1?, T2?, T3?, T4?, T5?) items)
		{
			return TryReadKeyPartsFrom(ref reader, 5, out items);
		}

	}

	/// <summary>Wrapper for encoding and decoding six items with lambda functions</summary>
	[PublicAPI]
	public abstract class CompositeKeyEncoder<T1, T2, T3, T4, T5, T6> : ICompositeKeyEncoder<T1, T2, T3, T4, T5, T6>
	{

		/// <inheritdoc />
		public abstract IKeyEncoding Encoding { get; }

		/// <inheritdoc />
		public abstract void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?, T3?, T4?, T5?, T6?) items);

		/// <inheritdoc />
		public abstract void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?, T6?) items);

		/// <inheritdoc />
		public abstract bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?, T6?) items);

		/// <inheritdoc />
		public void WriteKeyTo(ref SliceWriter writer, (T1?, T2?, T3?, T4?, T5?, T6?) items)
		{
			WriteKeyPartsTo(ref writer, 6, in items);
		}

		/// <inheritdoc />
		public void ReadKeyFrom(ref SliceReader reader, out (T1?, T2?, T3?, T4?, T5?, T6?) items)
		{
			ReadKeyPartsFrom(ref reader, 6, out items);
		}

		/// <inheritdoc />
		public bool TryReadKeyFrom(ref SliceReader reader, out (T1?, T2?, T3?, T4?, T5?, T6?) items)
		{
			return TryReadKeyPartsFrom(ref reader, 6, out items);
		}

	}

}
