#region Copyright (c) 2005-2023 Doxense SAS
// See License.MD for license information
#endregion

namespace Doxense.Runtime
{
	using System;
	using System.Diagnostics;
	using System.Reflection;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using JetBrains.Annotations;

	[DebuggerNonUserCode]
	public static class PlatformHelpers
	{

		/// <summary>JIT a method, if possible</summary>
		private static void PreJit(MethodInfo? m)
		{
			if (m != null && !m.IsAbstract && !m.ContainsGenericParameters)
			{
				try
				{
					RuntimeHelpers.PrepareMethod(m.MethodHandle);
				}
				catch (PlatformNotSupportedException)
				{
					// sous .NET Core (observé en 2.2), certains méthodes déclenchent cette exception, en particulier certains pattern "IAsyncResult BeginXYZ(...)" et "... EndXYZ(IAsyncResult, ....)".
					// => je n'ai pas vraiment trouvé de point distinctif sur les méthodes pour les filtrer en amont (comme on le fait pour les méthode abstract ou génériques)
#if FULL_DEBUG
					System.Diagnostics.Debug.WriteLine($"// Skipping JIT for unsupported method {m.DeclaringType?.Name}.{m}");
#endif
				}
			}
		}

		public static void PreJit(params Type[] types)
		{
			foreach (var type in types) PreJit(type);
		}

		/// <summary>JIT all the methods of the specified type</summary>
		public static void PreJit(Type type)
		{
			// JIT all the methods and properties
			int nm = 0;
			foreach (var m in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
			{
				PreJit(m);
				++nm;
			}

#if FULL_DEBUG
			System.Diagnostics.Trace.WriteLine($"// PreJit({type.FullName}): {nm} method(s)");
#endif

			// make sure we JIT the nested classes as well
			foreach (var nested in type.GetNestedTypes())
			{
				PreJit(nested);
			}
		}

		[Pure]
		public static string GetThreadName(Thread thread)
		{
			return thread.Name ?? ("#" + thread.ManagedThreadId);
		}

		public static string CurrentThreadName
		{
			[Pure]
			get => GetThreadName(Thread.CurrentThread);
		}

	}

}
