#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System.Text;
	using Doxense.Text;

	public static class ObservableJson
	{

		public static string ComputePath(JsonPath path, ReadOnlySpan<char> key, Index? index)
		{
			if (index != null)
			{
				return ComputePathWithIndex(path, key, index.Value);
			}
			return path.IsEmpty() ? key.ToString() : key.Length == 0 ? path.ToString() : path[key].ToString();

			static string ComputePathWithIndex(JsonPath path, ReadOnlySpan<char> key, Index index)
			{
				return (path.IsEmpty() ? key.ToString() : key.Length == 0 ? path.ToString() : (path[key].ToString())) + "[" + index.ToString() + "]";
			}
		}

		public static string? ComputePath(ObservableJsonValue? instance, string key) => ComputePath(instance, key.AsSpan(), null);

		public static string? ComputePath(ObservableJsonValue? instance, ReadOnlySpan<char> key) => ComputePath(instance, key, null);

		public static string? ComputePath(ObservableJsonValue? instance, int index) => ComputePath(instance, default, index);

		public static string? ComputePath(ObservableJsonValue? instance, Index index) => ComputePath(instance, default, index);

		public static string? ComputePath(ObservableJsonValue? instance, ReadOnlySpan<char> key, Index? index)
		{
			if (instance == null)
			{
				return ComputePath(JsonPath.Empty, key, index);
			}

			var sb = StringBuilderCache.Acquire();
			ComputePath(sb, instance);
			if (key.Length != 0)
			{
				if (sb.Length != 0) sb.Append('.');
				JsonPath.WriteTo(sb, key);
			}
			else if (index.HasValue)
			{
				JsonPath.WriteTo(sb, index.Value);
			}

			if (sb.Length == 0)
			{
				StringBuilderCache.Release(sb);
				return null;
			}
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		public static void ComputePath(StringBuilder sb, ObservableJsonValue current)
		{
			Contract.Debug.Requires(sb != null && current != null);

			var path = current.Path;

			if (path.Parent != null)
			{
				ComputePath(sb, path.Parent);
			}

			if (path.Key != null)
			{
				if (sb.Length != 0) sb.Append('.');
				JsonPath.WriteTo(sb, path.Key);
			}

			if (path.Index != null)
			{
				JsonPath.WriteTo(sb, path.Index.Value);
			}
		}

		public static ObservableJsonValue FromJson(IObservableJsonTransaction tr, ObservableJsonPath path, JsonValue value)
		{
			switch (value)
			{
				case JsonObject obj:
				{
					return new(tr, path, obj.ToReadOnly());
				}
				case JsonArray arr:
				{
					return new(tr, path, arr.ToReadOnly());
				}
				default:
				{
					Contract.Debug.Assert(value.IsReadOnly);
					return new(tr, path, value);
				}
			}
		}

	}

}
