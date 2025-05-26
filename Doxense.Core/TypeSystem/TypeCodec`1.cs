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
	using SnowBank.Buffers;

	/// <summary>Base implementation for type codecs that support both <see cref="IOrderedTypeCodec{T}">ordered</see> and <see cref="IUnorderedTypeCodec{T}">unordered</see> encoding</summary>
	/// <typeparam name="T">Type of encoded values</typeparam>
	public abstract class TypeCodec<T> : IOrderedTypeCodec<T>, IUnorderedTypeCodec<T>
	{

		/// <inheritdoc />
		public abstract void EncodeOrderedTo(ref SliceWriter output, T? value);

		/// <inheritdoc />
		public abstract T? DecodeOrderedFrom(ref SliceReader input);

		/// <inheritdoc />
		public virtual Slice EncodeOrdered(T? value)
		{
			var writer = default(SliceWriter);
			EncodeOrderedTo(ref writer, value);
			return writer.ToSlice();
		}

		/// <inheritdoc />
		public virtual T? DecodeOrdered(Slice input)
		{
			var slicer = new SliceReader(input);
			return DecodeOrderedFrom(ref slicer);
		}

		/// <inheritdoc />
		public virtual void EncodeUnorderedSelfTerm(ref SliceWriter output, T? value)
		{
			EncodeOrderedTo(ref output, value);
		}

		/// <inheritdoc />
		public virtual T? DecodeUnorderedSelfTerm(ref SliceReader input)
		{
			return DecodeOrderedFrom(ref input);
		}

		/// <inheritdoc />
		public virtual Slice EncodeUnordered(T? value)
		{
			var writer = default(SliceWriter);
			EncodeUnorderedSelfTerm(ref writer, value);
			return writer.ToSlice();
		}

		/// <inheritdoc />
		public virtual T? DecodeUnordered(Slice input)
		{
			var reader = new SliceReader(input);
			return DecodeUnorderedSelfTerm(ref reader);
		}

	}

}
