#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

#pragma warning disable CS8602 // Dereference of a possibly null reference.
namespace Doxense.Diagnostics.Contracts.Tests
{
	using System;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Threading;
	using Doxense.Testing;
	using NUnit.Framework;

	/// <summary>Tests sur la classe statique ThrowHelper</summary>
	[TestFixture]
	[Category("Core-SDK")]
	public class ThrowHelperTests : DoxenseTest
	{

		protected override void OnBeforeEverything()
		{
			Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
			Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
		}

		[Test]
		public void Test_ThrowInvalidOperationException()
		{

			var x = Assert.Throws<InvalidOperationException>(() => ThrowHelper.ThrowInvalidOperationException("Hello world !!!"));
			Assert.That(x.Message, Is.EqualTo("Hello world !!!"));

			// String.Format(..., strings)

			x = Assert.Throws<InvalidOperationException>(() => ThrowHelper.ThrowInvalidOperationException("{0} !!!", "Hello world"));
			Assert.That(x.Message, Is.EqualTo("Hello world !!!"));

			x = Assert.Throws<InvalidOperationException>(() => ThrowHelper.ThrowInvalidOperationException("{0} {1} !!!", "Hello", "world"));
			Assert.That(x.Message, Is.EqualTo("Hello world !!!"));

			x = Assert.Throws<InvalidOperationException>(() => ThrowHelper.ThrowInvalidOperationException("{0} {1} {2}", "Hello", "world", "!!!"));
			Assert.That(x.Message, Is.EqualTo("Hello world !!!"));

			// String.Format(... objects)

			x = Assert.Throws<InvalidOperationException>(() => ThrowHelper.ThrowInvalidOperationException("1+2={0}", 3));
			Assert.That(x.Message, Is.EqualTo("1+2=3"));

			x = Assert.Throws<InvalidOperationException>(() => ThrowHelper.ThrowInvalidOperationException("{0}+{1}=3", 1, 2));
			Assert.That(x.Message, Is.EqualTo("1+2=3"));

			x = Assert.Throws<InvalidOperationException>(() => ThrowHelper.ThrowInvalidOperationException("{0}+{1}={2}", 1, 2, 3));
			Assert.That(x.Message, Is.EqualTo("1+2=3"));

		}

		[Test]
		[SuppressMessage("ReSharper", "NotResolvedInText")]
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
		[SuppressMessage("ReSharper", "NotResolvedInText")]
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
