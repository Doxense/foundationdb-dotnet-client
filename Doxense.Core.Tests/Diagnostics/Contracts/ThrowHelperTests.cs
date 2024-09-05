﻿#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

// ReSharper disable NotResolvedInText
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace Doxense.Diagnostics.Contracts.Tests
{
	using System.IO;
	using System.Runtime.CompilerServices;

	/// <summary>Tests sur la classe statique ThrowHelper</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[SetInvariantCulture]
	[Parallelizable(ParallelScope.All)]
	public class ThrowHelperTests : SimpleTest
	{

		private static class TrustButVerify
		{
			public static string CallMe(string s)
			{
				Log($"CallMe(string):{s}");
				return "CallMe(string):" + s;
			}

			public static string CallMe(ref DefaultInterpolatedStringHandler s)
			{
				Log($"CallMe(interpolated):{s.ToString()}");
				return "CallMe(interpolated):" + s.ToStringAndClear();
			}
		}

		[Test, Order(0)]
		public void Test_VerifyAssumptions()
		{
			// we need to check that given two overloads, one with a string, and one with an interpolated string handler,
			// the compiler will invoke the former with literal strings, and the later with explict interportaled strings!

			var hello = "hello";
			var world = "world";

			// should invoke the string overload
			Assume.That(TrustButVerify.CallMe("hello"), Is.EqualTo("CallMe(string):hello"));
			Assume.That(TrustButVerify.CallMe("hello" + " world!"), Is.EqualTo("CallMe(string):hello world!"));
			Assume.That(TrustButVerify.CallMe(hello), Is.EqualTo("CallMe(string):hello"));

			// should invoke the interpolated overload
			Assume.That(TrustButVerify.CallMe($"{hello}, world!"), Is.EqualTo("CallMe(interpolated):hello, world!"));
			Assume.That(TrustButVerify.CallMe($"{hello}, {world}!"), Is.EqualTo("CallMe(interpolated):hello, world!"));
			Assume.That(TrustButVerify.CallMe($"{hello + ", " + world}!"), Is.EqualTo("CallMe(interpolated):hello, world!"));

			// cornercases:
			Assume.That(TrustButVerify.CallMe($"hello"), Is.EqualTo("CallMe(string):hello"), "Interpolated string without any argument is erased by the compiler");
		}

		[Test]
		public void Test_ThrowInvalidOperationException()
		{
			// literal string

			Assert.That(() => ThrowHelper.ThrowInvalidOperationException("Hello world !!!"), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("Hello world !!!"));

			Assert.That(() => throw ThrowHelper.InvalidOperationException("Hello world !!!"), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("Hello world !!!"));

			// Interpolated Strings

			var hello = "Hello";
			var world = "World";
			var exclamation = "!!!";

			Assert.That(() => ThrowHelper.ThrowInvalidOperationException($"{hello}, World !!!"), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("Hello, World !!!"));

			Assert.That(() => ThrowHelper.ThrowInvalidOperationException($"{hello}, {world} !!!"), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("Hello, World !!!"));

			Assert.That(() => ThrowHelper.ThrowInvalidOperationException($"{hello}, {world} {exclamation}"), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("Hello, World !!!"));

			Assert.That(() => ThrowHelper.ThrowInvalidOperationException($"1+2={3}"), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("1+2=3"));

			Assert.That(() => ThrowHelper.ThrowInvalidOperationException($"{1}+{2}=3"), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("1+2=3"));

			Assert.That(() => ThrowHelper.ThrowInvalidOperationException($"{1}+{2}={3}"), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("1+2=3"));

		}

		[Test]
		public void Test_ThrowArgumentNullException()
		{
			// note: pas certain que le message par défaut ne change pas d'une version a l'autre de .NET, ou dépende de la langue système ...

			var x = Assert.Throws<ArgumentNullException>(() => ThrowHelper.ThrowArgumentNullException("foo"));
			Assert.That(x.ParamName, Is.EqualTo("foo"));
			Assert.That(x.Message, Is.EqualTo("Value cannot be null. (Parameter 'foo')")); // <-- peut dépendre de la langue et de la version de .NET !

			x = Assert.Throws<ArgumentNullException>(() => ThrowHelper.ThrowArgumentNullException("foo", "Hello world !!!"));
			Assert.That(x.ParamName, Is.EqualTo("foo"));
			Assert.That(x.Message, Is.EqualTo("Hello world !!! (Parameter 'foo')")); // <-- peut dépendre de la langue et de la version de .NET !
		}

		[Test]
		public void Test_ThrowArgumentException()
		{
			// note: pas certain que le message par défaut ne change pas d'une version a l'autre de .NET, ou dépende de la langue système ...

			var x = Assert.Throws<ArgumentException>(() => ThrowHelper.ThrowArgumentException("foo"));
			Assert.That(x.ParamName, Is.EqualTo("foo"));
			Assert.That(x.Message, Is.EqualTo("Value does not fall within the expected range. (Parameter 'foo')")); // <-- peut dépendre de la langue et de la version de .NET !

			x = Assert.Throws<ArgumentException>(() => ThrowHelper.ThrowArgumentException("foo", "Hello world !!!"));
			Assert.That(x.ParamName, Is.EqualTo("foo"));
			Assert.That(x.Message, Is.EqualTo("Hello world !!! (Parameter 'foo')")); // <-- peut dépendre de la langue et de la version de .NET !
		}

		[Test]
		public void Test_ThrowObjectDisposedException()
		{
			var x0 = Assert.Throws<ObjectDisposedException>(() => ThrowHelper.ThrowObjectDisposedException(new MemoryStream(), "He's dead, Jim!"));
			Assert.That(x0.Message, Is.EqualTo("He's dead, Jim!\r\nObject name: 'MemoryStream'."));
			Assert.That(x0.InnerException, Is.Null);

			var x1 = Assert.Throws<ObjectDisposedException>(() => ThrowHelper.ThrowObjectDisposedException("He's dead, Jim!", new InvalidOperationException("I'm dead, Jim!")));
			Assert.That(x1.Message, Is.EqualTo("He's dead, Jim!"));
			Assert.That(x1.InnerException, Is.InstanceOf<InvalidOperationException>());
			Assert.That(x1.InnerException?.Message, Is.EqualTo("I'm dead, Jim!"));
		}

	}

}
