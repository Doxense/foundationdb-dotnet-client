#region Copyright (c) 2005-2023 Doxense SAS
// See License.MD for license information
#endregion

namespace Doxense.Text
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Text;
	using JetBrains.Annotations;

	public static class StringBuilderCache
	{
		//README: this is a copy of the BCL's StringBuilderCache which is internal
		//TODO: update the magic numbers that may or may not be appropriate for our use cases!

		private const int MAX_BUILDER_SIZE = 360;

		[ThreadStatic] private static StringBuilder? CachedInstance;

		public static StringBuilder Acquire(int capacity = 16)
		{
			if (capacity <= MAX_BUILDER_SIZE)
			{
				var sb = CachedInstance;
				if (sb != null)
				{
					// Avoid stringbuilder block fragmentation by getting a new StringBuilder
					// when the requested size is larger than the current capacity
					if (capacity <= sb.Capacity)
					{
						CachedInstance = null;
						sb.Clear();
						return sb;
					}
				}
			}
			return new StringBuilder(capacity);
		}

		public static void Release(StringBuilder sb)
		{
			if (sb.Capacity <= MAX_BUILDER_SIZE)
			{
				CachedInstance = sb;
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string GetStringAndRelease(StringBuilder sb)
		{
			string result = sb.ToString();
			Release(sb);
			return result;
		}
	}

}
