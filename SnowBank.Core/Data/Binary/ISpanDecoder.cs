// #region Copyright (c) 2023-2025 SnowBank SAS
// //
// // All rights are reserved. Reproduction or transmission in whole or in part, in
// // any form or by any means, electronic, mechanical or otherwise, is prohibited
// // without the prior written consent of the copyright owner.
// //
// #endregion

namespace SnowBank.Data.Binary
{

	/// <summary>Defines methods to decode the binary representation of instances of type <typeparamref name="TValue"/> from a span</summary>
	/// <typeparam name="TValue">Type of the decoded values</typeparam>
	/// <remarks>
	/// <para>This type is expected to be implemented on structs <b>ONLY</b>, in order to achieve the best performance by using JIT inlining and other optimizations as much as possible.</para>
	/// </remarks>
	public interface ISpanDecoder<TValue>
	{

		/// <summary>Decodes the value from the input buffer, if it contains enough data</summary>
		/// <param name="source">Source buffer with bytes to decode</param>
		/// <param name="value">Receives the decoded value, if the operation was successful</param>
		/// <returns><c>true</c> if the value was successfully decoded, <c>false</c> if the buffer does not contain a full representation of the value, or an exception if the decoding failed for other reasons.</returns>
		/// <remarks>
		/// <para>The caller should first call this method with the first chunk of data available. As long as the method returns <c>false</c>, the caller should wait for more data to arrive before calling it again, until it returns <c>true</c>.</para>
		/// <para>Please note that the method MUST NOT return <c>false</c> for a reason other than the buffer not containing enough data, otherwise the caller may end up in an infinite retry loop, passing a larger and larger buffer.</para>
		/// <para>If the data is incomplete, but the available data is already malformed, the method <b>SHOULD</b> throw an exception, in order to avoid waiting for more data before throwing.</para>
		/// </remarks>
		static abstract bool TryDecode(ReadOnlySpan<byte> source, out TValue? value);

	}

}
