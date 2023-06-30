#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Runtime.Tests
{
	using System;
	using System.Collections.Generic;
	using Doxense.Serialization;
	using NUnit.Framework;

	[TestFixture]
	[Category("Core-SDK")]
	public class TypeHelperFacts
	{

		[Test]
		public void Test_GetFriendlyName()
		{
			// native types
			Assert.That(typeof(string).GetFriendlyName(), Is.EqualTo("string"));
			Assert.That(typeof(int).GetFriendlyName(), Is.EqualTo("int"));
			Assert.That(typeof(bool).GetFriendlyName(), Is.EqualTo("bool"));

			// Simple types
			Assert.That(typeof(DateTime).GetFriendlyName(), Is.EqualTo("DateTime"));
			Assert.That(typeof(DateTimeKind).GetFriendlyName(), Is.EqualTo("DateTimeKind"));
			Assert.That(typeof(System.IO.FileStream).GetFriendlyName(), Is.EqualTo("FileStream"));

			// Generics...
			Assert.That(typeof(List<string>).GetFriendlyName(), Is.EqualTo("List<string>"));
			Assert.That(typeof(Dictionary<Guid, List<string>>).GetFriendlyName(), Is.EqualTo("Dictionary<Guid, List<string>>"));

			// Nested types
			Assert.That(typeof(Outer.Inner).GetFriendlyName(), Is.EqualTo("Outer.Inner"));
			Assert.That(typeof(Outer<string, bool>.Inner).GetFriendlyName(), Is.EqualTo("Outer<string, bool>.Inner"));
			Assert.That(typeof(Outer<string, bool>.Inner<Guid>).GetFriendlyName(), Is.EqualTo("Outer<string, bool>.Inner<Guid>"));
			Assert.That(typeof(Outer<string, bool>.SomeEnum).GetFriendlyName(), Is.EqualTo("Outer<string, bool>.SomeEnum"));
			Assert.That(typeof(Outer<string, bool>.Middle<Guid>.Inner<DateTime>).GetFriendlyName(), Is.EqualTo("Outer<string, bool>.Middle<Guid>.Inner<DateTime>"));

			// True Last Boss
			Assert.That(typeof(Dictionary<Outer<string, bool>.Inner, List<Outer<string, bool>.Middle<Guid>.Inner<DateTime>>>).GetFriendlyName(), Is.EqualTo("Dictionary<Outer<string, bool>.Inner, List<Outer<string, bool>.Middle<Guid>.Inner<DateTime>>>"));
		}

	}

	public class Outer { public class Inner { }}

	public class Outer<T1, T2>
	{
		public class Inner { } 
		public class Inner<T3> { }

		public class Middle<T3>
		{
			public class Inner<T4> { }
		}

		public enum SomeEnum { }

	}


}
