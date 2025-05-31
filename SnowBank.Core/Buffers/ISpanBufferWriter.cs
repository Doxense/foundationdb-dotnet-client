#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Buffers
{
	using System.Buffers;

	/// <summary>Represents an output sink into which <typeparam name="T"/> data can be written.</summary>
	/// <remarks>This is a simpler version of <see cref="IBufferWriter{T}"/> for containers that don't use managed memory, and thus cannot implement <see cref="IBufferWriter{T}.GetMemory"/>.</remarks>
	public interface ISpanBufferWriter<T>
	{
		/// <summary>
		/// Notifies <see cref="ISpanBufferWriter{T}"/> that <paramref name="count"/> amount of data was written to the output <see cref="Span{T}"/>/<see cref="Memory{T}"/>
		/// </summary>
		/// <remarks>
		/// You must request a new buffer after calling Advance to continue writing more data and cannot write to a previously acquired buffer.
		/// </remarks>
		void Advance(int count);

		/// <summary>
		/// Returns a <see cref="Span{T}"/> to write to that is at least the requested length (specified by <paramref name="sizeHint"/>).
		/// If no <paramref name="sizeHint"/> is provided (or it's equal to <code>0</code>), some non-empty buffer is returned.
		/// </summary>
		/// <remarks>
		/// This must never return an empty <see cref="Span{T}"/> but it can throw
		/// if the requested buffer size is not available.
		/// </remarks>
		/// <remarks>
		/// There is no guarantee that successive calls will return the same buffer or the same-sized buffer.
		/// </remarks>
		/// <remarks>
		/// You must request a new buffer after calling Advance to continue writing more data and cannot write to a previously acquired buffer.
		/// </remarks>
		Span<T> GetSpan(int sizeHint = 0);
	}

}
