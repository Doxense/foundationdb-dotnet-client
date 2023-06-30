#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense
{
	using Doxense.Tools;
	using JetBrains.Annotations;
	using NUnit.Framework;
	using NUnit.Framework.Constraints;
	using System;
	using System.Threading.Tasks;

	/// <summary>Classe helper pour les tests unitaires asynchrones</summary>
	public static class AsyncAssert
	{

		/// <summary>Vérifie qu'une méthode asynchrone génère bien une exception d'un type particulier</summary>
		/// <remarks>Cette méthode est nécessaire en .NET 4.0 car NUnit ne reconnait que les async delegate de .NET 4.5</remarks>
		public static async Task<T> Throws<T>([InstantHandle] Func<Task> asyncDelegate, string message = null, params object[] args)
			where T : Exception
		{
			Assert.That(asyncDelegate, Is.Not.Null, "asyncDelegate");

			Exception error = null;
			try
			{
				await asyncDelegate();
			}
			catch (Exception e)
			{
				error = e.UnwrapIfAggregate();
			}
			Assert.That(error, new ExactTypeConstraint(typeof(T)), message, args);
			return error as T;
		}

		/// <summary>Vérifie qu'une méthode asynchrone retourne bien le résultat attendu</summary>
		public static async Task AreEqual<T>(T expected, Task<T> task, string message = null, params object[] args)
		{
			Assert.That(task, Is.Not.Null, "task");
			T result = await task;
			Assert.That(result, Is.EqualTo(expected), message, args);
		}

		/// <summary>Vérifie qu'une méthode asynchrone retourne bien le résultat attendu</summary>
		/// <remarks>Cette méthode est nécessaire en .NET 4.0 car NUnit ne reconnait que les async delegate de .NET 4.5</remarks>
		public static async Task AreEqual<T>(T expected, [InstantHandle] Func<Task<T>> asyncDelegate, string message = null, params object[] args)
		{
			Assert.That(asyncDelegate, Is.Not.Null, "asyncDelegate");
			T result = await asyncDelegate();
			Assert.That(result, Is.EqualTo(expected), message, args);
		}

		/// <summary>Vérifie qu'une méthode asynchrone retourne bien un résultat différent</summary>
		/// <remarks>Cette méthode est nécessaire en .NET 4.0 car NUnit ne reconnait que les async delegate de .NET 4.5</remarks>
		public static async Task AreNotEqual<T>(T expected, [InstantHandle] Func<Task<T>> asyncDelegate, string message = null, params object[] args)
		{
			Assert.That(asyncDelegate, Is.Not.Null, "asyncDelegate");
			T result = await asyncDelegate();
			Assert.That(result, Is.Not.EqualTo(expected), message, args);
		}

		/// <summary>Vérifie qu'une méthode asynchrone retourne bien un résultat différent</summary>
		public static async Task AreNotEqual<T>(T expected, Task<T> task, string message = null, params object[] args)
		{
			Assert.That(task, Is.Not.Null, "task");
			T result = await task;
			Assert.That(result, Is.Not.EqualTo(expected), message, args);
		}


		/// <summary>Vérifie qu'une méthode asynchrone retourne bien un résultat null</summary>
		/// <remarks>Cette méthode est nécessaire en .NET 4.0 car NUnit ne reconnait que les async delegate de .NET 4.5</remarks>
		public static async Task IsNull<T>([InstantHandle] Func<Task<T>> asyncDelegate, string message = null, params object[] args)
			where T : class
		{
			Assert.That(asyncDelegate, Is.Not.Null, "asyncDelegate");
			T result = await asyncDelegate();
			Assert.That(result, Is.Null, message, args);
		}

		/// <summary>Vérifie qu'une méthode asynchrone retourne bien un résultat null</summary>
		public static async Task IsNull<T>(Task<T> task, string message = null, params object[] args)
			where T : class
		{
			Assert.That(task, Is.Not.Null, "task");
			T result = await task;
			Assert.That(result, Is.Null, message, args);
		}

		/// <summary>Vérifie qu'une méthode asynchrone retourne bien un résultat non-null</summary>
		/// <remarks>Cette méthode est nécessaire en .NET 4.0 car NUnit ne reconnait que les async delegate de .NET 4.5</remarks>
		public static async Task IsNotNull<T>([InstantHandle] Func<Task<T>> asyncDelegate, string message = null, params object[] args)
			where T : class
		{
			Assert.That(asyncDelegate, Is.Not.Null, "asyncDelegate");
			T result = await asyncDelegate();
			Assert.That(result, Is.Not.Null, message, args);
		}

		/// <summary>Vérifie qu'une méthode asynchrone retourne bien un résultat non-null</summary>
		public static async Task IsNotNull<T>(Task<T> task, string message = null, params object[] args)
			where T : class
		{
			Assert.That(task, Is.Not.Null, "asyncDelegate");
			T result = await task;
			Assert.That(result, Is.Not.Null, message, args);
		}

		/// <summary>Vérifie qu'une méthode asynchrone retourne bien 'true'</summary>
		public static async Task True([InstantHandle] Func<Task<bool>> asyncDelegate, string message = null, params object[] args)
		{
			Assert.That(asyncDelegate, Is.Not.Null, "asyncDelegate");
			bool condition = await asyncDelegate();
			Assert.That(condition, Is.True, message, args);
		}

		/// <summary>Vérifie qu'une méthode asynchrone retourne bien 'false'</summary>
		public static async Task False([InstantHandle] Func<Task<bool>> asyncDelegate, string message = null, params object[] args)
		{
			Assert.That(asyncDelegate, Is.Not.Null, "asyncDelegate");
			bool condition = await asyncDelegate();
			Assert.That(condition, Is.False, message, args);
		}

		/// <summary>Vérifie qu'une méthode asynchrone retourne bien 'true'</summary>
		public static async Task True(Task<bool> task, string message = null, params object[] args)
		{
			Assert.That(task, Is.Not.Null, "task");
			bool condition = await task;
			Assert.That(condition, Is.True, message, args);
		}

		/// <summary>Vérifie qu'une méthode asynchrone retourne bien 'false'</summary>
		public static async Task False(Task<bool> task, string message = null, params object[] args)
		{
			Assert.That(task, Is.Not.Null, "task");
			bool condition = await task;
			Assert.That(condition, Is.False, message, args);
		}

	}

}
