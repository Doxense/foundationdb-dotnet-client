#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Text.Tests
{
	using SnowBank.Text;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class Base1024EncodingFacts : SimpleTest
	{

		[Test]
		public void Test_Base1024_Encoder_RoundTrips()
		{
			// ensure that we can encode any arbitrary size random buffer into a string literal, and back to the original bytes

			var sourceBuffer = new byte[100 + 16];
			var charBuffer = new char[100 + 16];
			var decodedBuffer = new byte[100 + 16];

			for (int i = 0; i < 10_000; i++)
			{
				// choose random length
				int len = this.Rnd.Next(1, 65);
				sourceBuffer.AsSpan().Fill(0xAA);
				decodedBuffer.AsSpan().Fill(0x55);
				charBuffer.AsSpan().Fill('_');

				var source = sourceBuffer.AsSpan(0, len);

				// fill with random bytes
				this.Rnd.NextBytes(source);

				// encode to string
				Assert.That(Base1024Encoding.TryEncodeTo(source, charBuffer, out var charsWritten), Is.True);
				var encoded = charBuffer.AsSpan(0, charsWritten);

				// ensure that the rest of the buffer is untouched
				if (charBuffer.AsSpan(charsWritten).ContainsAnyExcept('_'))
				{
					DumpHexa(charBuffer.AsSpan(charsWritten));
					Assert.Fail("Tail of chars buffer was modified!");
				}

				// decode back to bytes
				Assert.That(Base1024Encoding.TryDecodeTo(encoded, decodedBuffer, out var bytesWritten), Is.True);
				var decoded = decodedBuffer.AsSpan(0, bytesWritten);

				// ensure that the decoded bytes are valid
				if (decoded.Length != source.Length)
				{
					DumpVersus(source, decoded);
					Assert.That(decoded.Length, Is.EqualTo(source.Length));
				}
				if (!decoded.SequenceEqual(source))
				{
					DumpVersus(source, decoded);
					Assert.That(decoded.ToArray(), Is.EqualTo(source.ToArray()));
				}

				// ensure that the rest of the buffer is untouched
				if (decodedBuffer.AsSpan(bytesWritten).ContainsAnyExcept((byte) 0x55))
				{
					DumpHexa(decodedBuffer.AsSpan(bytesWritten));
					Assert.Fail("Tail of decoded buffer was modified!");
				}

			}
		}

	}

}
