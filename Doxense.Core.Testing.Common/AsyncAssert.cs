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

#if DEPRECATED // scream-test to check if people still using it can easily rewrite to a more modern version

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

#endif
