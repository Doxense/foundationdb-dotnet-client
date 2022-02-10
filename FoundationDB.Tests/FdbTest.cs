#region BSD License
/* Copyright (c) 2013-2019, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

#nullable enable

namespace FoundationDB.Client.Tests
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FoundationDB.Layers.Directories;
	using NUnit.Framework;

	/// <summary>Base class for all FoundationDB tests</summary>
	public abstract class FdbTest
	{

		private CancellationTokenSource m_cts;
		private CancellationToken m_ct;
		private Stopwatch m_timer;

		protected int OverrideApiVersion = 0;

		[OneTimeSetUp]
		protected void BeforeAllTests()
		{
			// We must ensure that FDB is running before executing the tests
			// => By default, we always use 
			if (Fdb.ApiVersion == 0)
			{
				int version = OverrideApiVersion;
				if (version == 0) version = Fdb.GetDefaultApiVersion();
				if (version > Fdb.GetMaxApiVersion())
				{
					Assume.That(version, Is.LessThanOrEqualTo(Fdb.GetMaxApiVersion()), "Unit tests require that the native fdb client version be at least equal to the current binding version!");
				}
				Fdb.Start(version);
			}
			else if (OverrideApiVersion != 0 && OverrideApiVersion != Fdb.ApiVersion)
			{
				//note: cannot change API version on the fly! :(
				Assume.That(Fdb.ApiVersion, Is.EqualTo(OverrideApiVersion), "The API version selected is not what this test is expecting!");
			}
		}

		[SetUp]
		protected void BeforeEachTest()
		{
			lock (this)
			{
				m_cts = null;
				m_ct = CancellationToken.None;
			}

			//note: some test runners fail with a null-ref in the Test.FullName property ...
			string fullName;
			try
			{
				fullName = TestContext.CurrentContext.Test.FullName;
			}
			catch
			{
				fullName = this.GetType().Name + ".???";
			}
			Trace.WriteLine("=== " + fullName + "() === " + DateTime.Now.TimeOfDay);

			m_timer = Stopwatch.StartNew();
		}

		[TearDown]
		protected void AfterEachTest()
		{
			m_timer.Stop();
			if (m_cts != null)
			{
				try { m_cts.Cancel(); } catch { }
				m_cts.Dispose();
			}
		}

		/// <summary>Time elapsed since the start of the current test</summary>
		protected TimeSpan TestElapsed
		{
			[DebuggerStepThrough]
			get { return m_timer.Elapsed; }
		}

		/// <summary>Cancellation token usable by any test</summary>
		protected CancellationToken Cancellation
		{
			[DebuggerStepThrough]
			get
			{
				if (m_cts == null) SetupCancellation();
				return m_ct;
			}
		}

		private void SetupCancellation()
		{
			lock (this)
			{
				if (m_cts == null)
				{
					m_cts = new CancellationTokenSource();
					m_ct = m_cts.Token;
				}
			}
		}

		/// <summary>Connect to the local test database</summary>
		[DebuggerStepThrough]
		protected Task<IFdbDatabase> OpenTestDatabaseAsync()
		{
			return TestHelpers.OpenTestDatabaseAsync(this.Cancellation);
		}

		/// <summary>Connect to the local test database</summary>
		[DebuggerStepThrough]
		protected Task<IFdbDatabase> OpenTestPartitionAsync()
		{
			return TestHelpers.OpenTestPartitionAsync(this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task<FdbDirectorySubspace> GetCleanDirectory(IFdbDatabase db, params string[] path)
		{
			return TestHelpers.GetCleanDirectory(db, path, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task DumpSubspace(IFdbDatabase db, IKeySubspace subspace)
		{
			return TestHelpers.DumpSubspace(db, subspace, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected async Task DeleteSubspace(IFdbDatabase db, IKeySubspace subspace)
		{
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				tr.ClearRange(subspace);
				await tr.CommitAsync();
			}
		}

		#region Logging...

		// These methods are just there to help with the problem of culture-aware string formatting

		[DebuggerStepThrough]
		public static void Log(string text)
		{
			TestContext.Progress.WriteLine(text);
		}

		/// <summary>Retourne une représentation textuelle basique d'un object, tenant sur une seule ligne</summary>
		protected static string Stringify(object? item)
		{
			if (item == null)
			{ // Null
				return "<null>";
			}

			if (item is string str)
			{ // hack pour empecher les CRLF de casser l'affichage
				if (str.Length == 0) return @"""""";
				return "\"" + str.Replace(@"\", @"\\").Replace("\r", @"\r").Replace("\n", @"\n").Replace("\0", @"\0").Replace(@"""", @"\""") + "\"";
			}

			var type = item.GetType();
			if (type.Name.StartsWith("ValueTuple`", StringComparison.Ordinal))
			{
				return item.ToString()!;
			}

			// Formattable
			if (item is IFormattable formattable)
			{
				// utilise le format le plus adapté en fonction du type
				string? fmt = null;
				if (item is int || item is uint || item is long || item is ulong)
				{
					fmt = "N0";
				}
				else if (item is double || item is float)
				{
					fmt = "R";
				}
				else if (item is DateTime || item is DateTimeOffset)
				{
					fmt = "O";
				}
				return $"({item.GetType().Name}) {formattable.ToString(fmt, CultureInfo.InvariantCulture)}";
			}

			if (type.IsArray)
			{ // Array
				Array arr = (Array) item;
				var elType = type.GetElementType()!;
				if (typeof(IFormattable).IsAssignableFrom(elType))
				{
					return $"({elType.Name}[{arr.Length}]) [ {string.Join(", ", arr.Cast<IFormattable>().Select(x => x.ToString(null, CultureInfo.InvariantCulture)))} ]";
				}
				return $"({elType.Name}[{arr.Length}]) [{string.Join(", ", arr.Cast<object>().Select(x => Stringify(x)))}]";
			}

			// Alea Jacta Est
			return $"({type.Name}) {item.ToString()}";
		}

		[DebuggerNonUserCode]
		public static void Log(object? item)
		{
			Log(item as string ?? Stringify(item));
		}

		[DebuggerStepThrough]
		public static void Log()
		{
			Log(string.Empty);
		}

		[DebuggerStepThrough]
		public static void Dump(object item)
		{
			if (item == null)
			{
				Log("null");
			}
			else
			{
				Log(string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", item.GetType().Name, item));
			}
		}

		#endregion

	}
}
