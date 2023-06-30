#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
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
