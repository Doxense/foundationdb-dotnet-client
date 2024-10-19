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

#if NET8_0_OR_GREATER

namespace FoundationDB.Client
{
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Numerics;
	using System.Runtime.InteropServices;
	using System.Text;
	using Doxense.Collections.Tuples;

	public enum FqlItemType
	{
		Variable = -2,
		MaybeMore = -1,

		Invalid = 0,

		Nil,
		Bool,
		Int,
		UInt,
		Float,
		String,
		Bytes,
		Uuid,
		Tuple,

	}

	[Flags]
	public enum FqlVariableTypes
	{
		Nil = 1 << 0,
		Bool = 1 << 1,
		Int = 1 << 2,
		UInt = 1 << 3,
		Float = 1 << 4,
		String = 1 << 5,
		Bytes = 1 << 6,
		Uuid = 1 << 7,
		Tuple = 1 << 8,
	}

	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FqlTupleItem : IEquatable<FqlTupleItem>, IFqlExpression
	{
		private static readonly object s_false = true;

		private static readonly object s_true = true;

		private static readonly object[] s_smallIntCache = Enumerable.Range(0, 100).Select(i => (object) (long) i).ToArray();

		private static readonly object[] s_smallUIntCache = Enumerable.Range(0, 100).Select(i => (object) (ulong) i).ToArray();

		public readonly FqlItemType Type;

		public readonly object? Value;

		public FqlTupleItem(FqlItemType type, object? value)
		{
			this.Type = type;
			this.Value = value;
		}

		/// <inheritdoc />
		public bool IsPattern => this.Type is FqlItemType.Variable or FqlItemType.MaybeMore;

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj is FqlTupleItem other && Equals(other);

		/// <inheritdoc />
		public override int GetHashCode() => this.Type switch
		{
			FqlItemType.Variable => HashCode.Combine(FqlItemType.Tuple, (FqlVariableTypes) this.Value!),
			FqlItemType.MaybeMore => HashCode.Combine(FqlItemType.MaybeMore),
			FqlItemType.Nil => HashCode.Combine(FqlItemType.Nil),
			FqlItemType.Bool => HashCode.Combine(FqlItemType.Bytes, (bool) this.Value!),
			FqlItemType.Int => HashCode.Combine(FqlItemType.String, (long) this.Value!),
			FqlItemType.UInt => HashCode.Combine(FqlItemType.String, (ulong) this.Value!),
			FqlItemType.Float => HashCode.Combine(FqlItemType.String, (double) this.Value!),
			FqlItemType.String => HashCode.Combine(FqlItemType.String, (string) this.Value!),
			FqlItemType.Bytes => HashCode.Combine(FqlItemType.Bytes, (Slice) this.Value!),
			FqlItemType.Tuple => HashCode.Combine(FqlItemType.Tuple, (FqlTupleExpression) this.Value!),
			_ => HashCode.Combine(this.Type),
		};

		public bool Matches(object? value) => this.Type switch
		{
			FqlItemType.Variable => MatchType((FqlVariableTypes) this.Value!, value),
			FqlItemType.MaybeMore => ReferenceEquals(value, null),
			FqlItemType.Nil => ReferenceEquals(value, null),
			FqlItemType.Bool => value is bool b && Equals(b),
			FqlItemType.Int or FqlItemType.UInt => value switch
			{
				int i => Equals(i),
				long l => Equals(l),
				uint ui => Equals(ui),
				ulong ul => Equals(ul),
				_ => false,
			},
			FqlItemType.Float => value switch
			{
				float f => Equals(f),
				double d => Equals(d),
				//TODO: decimal, Half
				_ => false,
			},
			FqlItemType.String => value is string str && Equals(str),
			FqlItemType.Bytes => value is Slice s && Equals(s),
			FqlItemType.Uuid => value switch
			{
				Uuid128 u => Equals(u),
				Guid g => Equals(g),
				_ => false,
			},
			_ => throw new NotImplementedException(this.Type.ToString())
		};

		public bool Equals(FqlTupleItem other) => other.Type == this.Type && object.Equals(this.Value, other.Value);

		public bool Equals(int value)
		{
			if (this.Type == FqlItemType.Int) return ((long) this.Value!) == value;
			if (this.Type == FqlItemType.UInt) return value >= 0 && ((ulong) this.Value!) == (ulong) value;
			return false;
		}

		public bool Equals(long value)
		{
			if (this.Type == FqlItemType.Int) return ((long) this.Value!) == value;
			if (this.Type == FqlItemType.UInt) return value >= 0 && ((ulong) this.Value!) == (ulong) value;
			return false;
		}

		public bool Equals(uint value)
		{
			if (this.Type == FqlItemType.Int) return ((long) this.Value!) >= 0 && ((ulong) (long) this.Value!) == value;
			if (this.Type == FqlItemType.UInt) return ((ulong) this.Value!) == value;
			return false;
		}

		public bool Equals(ulong value)
		{
			if (this.Type == FqlItemType.Int) return ((long) this.Value!) >= 0 && ((ulong) (long) this.Value!) == value;
			if (this.Type == FqlItemType.UInt) return ((ulong) this.Value!) == value;
			return false;
		}

		public bool Equals(float value)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return this.Type == FqlItemType.Float && (float.IsNaN(value) ? double.IsNaN((double) this.Value!) : ((double) this.Value!) == value);
		}

		public bool Equals(double value)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return this.Type == FqlItemType.Float && (double.IsNaN(value) ? double.IsNaN((double) this.Value!) : ((double) this.Value!) == value);
		}

		public bool Equals(string value)
		{
			return this.Type == FqlItemType.String && ((string) this.Value!) == value;
		}

		public bool Equals(ReadOnlySpan<char> value)
		{
			return this.Type == FqlItemType.String && value.SequenceEqual((string) this.Value!);
		}

		public bool Equals(Guid value)
		{
			return this.Type == FqlItemType.Uuid && ((Uuid128) this.Value!) == (Uuid128) value;
		}

		public bool Equals(Uuid128 value)
		{
			return this.Type == FqlItemType.Uuid && ((Uuid128) this.Value!) == value;
		}

		public bool Equals(Slice value)
		{
			return this.Type == FqlItemType.Bytes && ((Slice) this.Value!).Equals(value);
		}

		public bool Equals(IVarTuple value)
		{
			return this.Type == FqlItemType.Tuple && ((FqlTupleExpression) this.Value!).Match(value);
		}

		public static bool MatchType(FqlVariableTypes types, object? value)
		{
			return value switch
			{
				null => types.HasFlag(FqlVariableTypes.Nil),
				bool => types.HasFlag(FqlVariableTypes.Bool),
				int i => i < 0 ? types.HasFlag(FqlVariableTypes.Int) : (types.HasFlag(FqlVariableTypes.Int) || types.HasFlag(FqlVariableTypes.UInt)),
				uint => (types.HasFlag(FqlVariableTypes.Int) || types.HasFlag(FqlVariableTypes.UInt)),
				long l => l < 0 ? types.HasFlag(FqlVariableTypes.Int) : (types.HasFlag(FqlVariableTypes.Int) || types.HasFlag(FqlVariableTypes.UInt)),
				ulong => (types.HasFlag(FqlVariableTypes.Int) || types.HasFlag(FqlVariableTypes.UInt)),
				float => types.HasFlag(FqlVariableTypes.Float),
				double => types.HasFlag(FqlVariableTypes.Float),
				string => types.HasFlag(FqlVariableTypes.String),
				Slice => types.HasFlag(FqlVariableTypes.Bytes),
				Guid => types.HasFlag(FqlVariableTypes.Uuid),
				Uuid128 => types.HasFlag(FqlVariableTypes.Uuid),
				IVarTuple => types.HasFlag(FqlVariableTypes.Tuple),
				_ => false,
			};
		}

		/// <inheritdoc />
		public override string ToString() => this.Type switch
		{
			FqlItemType.Variable => "<" + ToTypeLiteral((FqlVariableTypes) this.Value!) + ">",
			FqlItemType.MaybeMore => "...",
			FqlItemType.Nil => "nil",
			FqlItemType.Bool => ((bool) this.Value!) ? "true" : "false",
			FqlItemType.Int => ((long) this.Value!).ToString(null, CultureInfo.InvariantCulture),
			FqlItemType.UInt => ((ulong) this.Value!).ToString(null, CultureInfo.InvariantCulture),
			FqlItemType.Float => ((double) this.Value!).ToString("R", CultureInfo.InvariantCulture),
			FqlItemType.String => $"\"{((string) this.Value!).Replace("\"", "\\\"")}\"",
			FqlItemType.Uuid => ((Uuid128) this.Value!).ToString("B"),
			FqlItemType.Bytes => "0x" + ((Slice) this.Value!).ToString("x"),
			FqlItemType.Tuple => ((FqlTupleExpression) this.Value!).ToString(),
			_ => $"<?{this.Type}?>",
		};

		public void Explain(ExplanationBuilder builder)
		{
			if (this.Type == FqlItemType.Tuple)
			{
				((FqlTupleExpression) this.Value!).Explain(builder);
				return;
			}

			builder.WriteLine($"{this.Type}: {this.ToString()}");
		}

		private static readonly Dictionary<FqlVariableTypes, string> s_typesLiteralCache = new();

		public string ToTypeLiteral(FqlVariableTypes types)
		{
			lock (s_typesLiteralCache)
			{
				if (s_typesLiteralCache.TryGetValue(types, out var s))
				{
					return s;
				}

				s = CreateTypeLiteral(types);
				s_typesLiteralCache[types] = s;
				return s;
			}

			static string CreateTypeLiteral(FqlVariableTypes types)
			{
				if (BitOperations.PopCount((uint) types) == 1)
				{
					return types.ToString().ToLowerInvariant();
				}

				var sb = new StringBuilder();
				foreach (var x in Enum.GetValues<FqlVariableTypes>())
				{
					if ((types & x) != 0)
					{
						if (sb.Length != 0) sb.Append('|');
						sb.Append(x.ToString().ToLowerInvariant());
					}
				}

				return sb.ToString();
			}

		}

		public static FqlTupleItem Variable(FqlVariableTypes types) => new(FqlItemType.Variable, types);

		public static FqlTupleItem MaybeMore() => new(FqlItemType.MaybeMore, null);

		public static FqlTupleItem Nil() => new(FqlItemType.Nil, null);

		public static FqlTupleItem Boolean(bool value) => new(FqlItemType.Bool, value ? s_true : s_false);

		public static FqlTupleItem Int(int value) => new(FqlItemType.Int, (uint) value < s_smallIntCache.Length ? s_smallIntCache[value] : (long) value);

		public static FqlTupleItem Int(long value) => new(FqlItemType.Int, (value >= 0 && value < s_smallIntCache.Length) ? s_smallIntCache[value] : value);

		public static FqlTupleItem UInt(uint value) => new(FqlItemType.UInt, value < s_smallUIntCache.Length ? s_smallUIntCache[value] : (ulong) value);

		public static FqlTupleItem UInt(ulong value) => new(FqlItemType.UInt, value < (ulong) s_smallUIntCache.Length ? s_smallUIntCache[value] : value);

		public static FqlTupleItem Float(float value) => new(FqlItemType.Float, (double) value);

		public static FqlTupleItem Float(double value) => new(FqlItemType.Float, value);

		public static FqlTupleItem String(string value) => new(FqlItemType.String, value);

		public static FqlTupleItem Bytes(Slice value) => new(FqlItemType.Bytes, value);

		public static FqlTupleItem Uuid(Guid value) => new(FqlItemType.Uuid, (Uuid128) value);

		public static FqlTupleItem Uuid(Uuid128 value) => new(FqlItemType.Uuid, value);

		public static FqlTupleItem Tuple(FqlTupleExpression value) => new(FqlItemType.Tuple, value);

	}

	[DebuggerDisplay("{ToString(),nq}")]
	public sealed class FqlTupleExpression : IEquatable<FqlTupleExpression>, IFqlExpression
	{
		public List<FqlTupleItem> Items { get; } = [];

		/// <inheritdoc />
		public bool IsPattern => this.Items.Any(x => x.IsPattern);

		public bool Match(IVarTuple? tuple)
		{
			if (tuple == null) return false;

			var items = CollectionsMarshal.AsSpan(this.Items);

			// if the last is MaybeMore, we don't need to check
			bool exactSize = true;
			while(items.Length > 0 && items[^1].Type == FqlItemType.MaybeMore)
			{
				exactSize = false;
				items = items[..^1];
			}

			// if the tuple is smaller, it will not match
			if (exactSize)
			{
				if (tuple.Count != items.Length) return false;
			}
			else
			{
				if (tuple.Count < items.Length) return false;
			}

			for (int i = 0; i < items.Length; i++)
			{
				if (!items[i].Matches(tuple[i]))
				{
					return false;
				}
			}
			return true;
		}

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj is FqlTupleExpression other && Equals(other);

		/// <inheritdoc />
		public override int GetHashCode()
		{
			var hc = new HashCode();
			foreach (var item in this.Items)
			{
				hc.Add(item);
			}
			return hc.ToHashCode();
		}

		public bool Equals(FqlTupleExpression? other)
		{
			if (other is null) return false;
			if (ReferenceEquals(other, this)) return true;

			var items = this.Items;
			var otherItems = other.Items;
			if (items.Count != otherItems.Count) return false;

			for (int i = 0; i < items.Count; i++)
			{
				if (!items[i].Equals(otherItems[i])) return false;
			}

			return true;
		}

		#region Builder Pattern...

		public static FqlTupleExpression Create() => new();

		public FqlTupleExpression Add(FqlTupleItem item)
		{
			this.Items.Add(item);
			return this;
		}

		public FqlTupleExpression AddMaybeMore() => Add(FqlTupleItem.MaybeMore());

		public FqlTupleExpression AddVariable(FqlVariableTypes types) => Add(FqlTupleItem.Variable(types));

		public FqlTupleExpression AddNil() => Add(FqlTupleItem.Nil());

		public FqlTupleExpression AddBoolean(bool value) => Add(FqlTupleItem.Boolean(value));

		public FqlTupleExpression AddInt(int value) => Add(FqlTupleItem.Int(value));

		public FqlTupleExpression AddInt(long value) => Add(FqlTupleItem.Int(value));

		public FqlTupleExpression AddUInt(uint value) => Add(FqlTupleItem.UInt(value));

		public FqlTupleExpression AddUInt(ulong value) => Add(FqlTupleItem.UInt(value));

		public FqlTupleExpression AddFloat(float value) => Add(FqlTupleItem.Float(value));

		public FqlTupleExpression AddFloat(double value) => Add(FqlTupleItem.Float(value));

		public FqlTupleExpression AddString(string value) => Add(FqlTupleItem.String(value));

		public FqlTupleExpression AddBytes(Slice value) => Add(FqlTupleItem.Bytes(value));

		public FqlTupleExpression AddUuid(Guid value) => Add(FqlTupleItem.Uuid(value));

		public FqlTupleExpression AddUuid(Uuid128 value) => Add(FqlTupleItem.Uuid(value));

		public FqlTupleExpression AddTuple(FqlTupleExpression value) => Add(FqlTupleItem.Tuple(value));

		#endregion

		/// <inheritdoc />
		public override string ToString()
		{
			switch (this.Items.Count)
			{
				case 0: return "()";
				case 1: return $"({this.Items[0]})";
				default:
				{
					var sb = new StringBuilder();
					sb.Append('(');
					foreach (var item in this.Items)
					{
						if (sb.Length > 1) sb.Append(", ");
						sb.Append(item.ToString());
					}
					sb.Append(')');
					return sb.ToString();
				}
			}
		}

		/// <inheritdoc />
		public void Explain(ExplanationBuilder builder)
		{
			if (!builder.Recursive)
			{
				builder.WriteLine($"Tuple: [{this.Items.Count}] {ToString()}");
				return;
			}

			builder.WriteLine($"Tuple: [{this.Items.Count}]");
			builder.ExplainChildren(this.Items);
		}

	}

}

#endif
