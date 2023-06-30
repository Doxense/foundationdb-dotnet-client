#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense
{
	using Doxense.Threading.Tasks;
	using NUnit.Framework;
	using NUnit.Framework.Constraints;
	using System;
	using System.Threading.Tasks;

	/// <summary>Classe helper pour les tests sur les Task&lt;T&gt;</summary>
	public static class TaskAssert
	{

		/// <summary>Vérifie qu'une Task déclenche bien une exception d'un certain type</summary>
		public static T Throws<T>(Task task, string message, params object[] args)
			where T : Exception
		{
			Assert.That(task, Is.Not.Null, "task");

			Exception error = null;
			try
			{
				task.GetAwaiter().GetResult();
			}
			catch (Exception e)
			{
				error = e;
			}
			Assert.That(error, new ExactTypeConstraint(typeof(T)), message, args);
			return error as T;
		}

		/// <summary>Vérifie qu'une Task retourne bien un résultat attendu</summary>
		public static void AreEqual<T>(T expected, Task<T> task, string message, params object[] args)
		{
			Assert.That(task, Is.Not.Null, "task");
			T result = task.GetAwaiter().GetResult();
			Assert.That(result, Is.EqualTo(expected), message, args);
		}

		/// <summary>Vérifie qu'une Task retourne bien un résultat différent</summary>
		public static void AreNotEqual<T>(T expected, Task<T> task, string message, params object[] args)
		{
			Assert.That(task, Is.Not.Null, "task");
			T result = task.GetAwaiter().GetResult();
			Assert.That(result, Is.Not.EqualTo(expected), message, args);
		}

		/// <summary>Vérifie qu'une Task retourne bien un résultat null</summary>
		public static void IsNull<T>(Task<T> task, string message, params object[] args)
		{
			Assert.That(task, Is.Not.Null, "task");
			T result = task.GetAwaiter().GetResult();
			Assert.That(result, Is.Null, message, args);
		}

		/// <summary>Vérifie qu'une Task retourne bien un résultat non-null</summary>
		public static void IsNotNull<T>(Task<T> task, string message, params object[] args)
		{
			Assert.That(task, Is.Not.Null, "task");
			T result = task.GetAwaiter().GetResult();
			Assert.That(result, Is.Not.Null, message, args);
		}

		/// <summary>Vérifie qu'une Task retourne bien 'true'</summary>
		public static void IsTrue(Task<bool> task, string message, params object[] args)
		{
			Assert.That(task, Is.Not.Null, "task");
			bool condition = task.GetAwaiter().GetResult();
			Assert.That(condition, Is.True, message, args);
		}

		/// <summary>Vérifie qu'une Task retourne bien 'false'</summary>
		public static void IsFalse(Task<bool> task, string message, params object[] args)
		{
			Assert.That(task, Is.Not.Null, "task");
			bool condition = task.GetAwaiter().GetResult();
			Assert.That(condition, Is.False, message, args);
		}
	}

}
