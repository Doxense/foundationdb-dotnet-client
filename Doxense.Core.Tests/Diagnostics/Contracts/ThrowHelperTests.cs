#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

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
