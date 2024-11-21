#define FULL_DEBUG

namespace Doxense.Serialization.Json.CodeGen
{
	using System.Collections.Generic;
	using System.Text;
	using System.Threading;
	using Microsoft.CodeAnalysis;

	/// <summary>
	/// An equatable value representing type identity.
	/// </summary>
	[DebuggerDisplay("{ToString(),nq}")]
	public sealed class TypeRef : IEquatable<TypeRef>
	{

		private static readonly Dictionary<SpecialType, TypeRef> s_cachedTypes = new();
		private static readonly ReaderWriterLockSlim s_lock = new();

		public static TypeRef Create(ITypeSymbol type)
		{
			switch (type.SpecialType)
			{
				case >= SpecialType.System_Boolean and<= SpecialType.System_String:
				case SpecialType.System_DateTime:
				//TODO: more!
				{
					s_lock.EnterUpgradeableReadLock();
					if (!s_cachedTypes.TryGetValue(type.SpecialType, out var cached))
					{
						cached = new(type);
						s_lock.EnterWriteLock();
						s_cachedTypes[type.SpecialType] = cached;
						s_lock.ExitWriteLock();
					}
					s_lock.ExitUpgradeableReadLock();
					return cached;
				}

				default:
				{
					return new TypeRef(type);
				}
			}
		}

		public TypeRef(ITypeSymbol type)
		{
			if (type is null) throw new ArgumentNullException(nameof(type));

			this.Name = type.Name;
			this.FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		}

		public string Name { get; }

		/// <summary>
		/// Fully qualified assembly name, prefixed with "global::", e.g. global::System.Numerics.BigInteger.
		/// </summary>
		public string FullyQualifiedName { get; }

		public override bool Equals(object? obj) => this.Equals(obj as TypeRef);
	
		public bool Equals(TypeRef? other) => other != null && this.FullyQualifiedName == other.FullyQualifiedName;

		public override int GetHashCode() => this.FullyQualifiedName.GetHashCode();

		public override string ToString() => $"typeof({this.FullyQualifiedName})";

	}

	/// <summary>
	/// An equatable value representing type identity.
	/// </summary>
	[DebuggerDisplay("Name = {Name}")]
	public sealed record TypeMetadata
	{

		private static readonly Dictionary<SpecialType, TypeMetadata> s_cachedTypes = new();
		private static readonly ReaderWriterLockSlim s_lock = new();

		public static TypeMetadata Create(ITypeSymbol type)
		{
			switch (type.SpecialType)
			{
				case >= SpecialType.System_Boolean and<= SpecialType.System_String:
				case SpecialType.System_DateTime:
				//TODO: more!
				{
					s_lock.EnterUpgradeableReadLock();
					if (!s_cachedTypes.TryGetValue(type.SpecialType, out var cached))
					{
						cached = new(type);
						s_lock.EnterWriteLock();
						s_cachedTypes[type.SpecialType] = cached;
						s_lock.ExitWriteLock();
					}
					s_lock.ExitUpgradeableReadLock();
					return cached;
				}

				default:
				{
					return new TypeMetadata(type);
				}
			}
		}

		public TypeMetadata(ITypeSymbol type)
		{
			if (type is null) throw new ArgumentNullException(nameof(type));

			this.Ref = TypeRef.Create(type);
			this.Name = type.Name;
			this.NameSpace = type.ContainingNamespace?.ToDisplayString() ?? "";
			this.IsSealed = type.IsSealed;
			this.TypeKind = type.TypeKind;
			this.SpecialType = type.OriginalDefinition?.SpecialType ?? default;
			this.Nullability = type.NullableAnnotation;
			this.IsRecord = type.IsRecord;
			if (this.SpecialType == SpecialType.System_Nullable_T)
			{
				var underlyingType = (type as INamedTypeSymbol)?.TypeArguments.FirstOrDefault();
				if (underlyingType is not null)
				{
					this.Name = "Nullable<" + underlyingType.Name + ">";
					this.UnderlyingType = Create(underlyingType);
				}
			}

			// we want to generate the hierarchy of derived classes
			var parent = type.BaseType;
			if (parent is not null)
			{
				var parents = new List<TypeRef>();
				do
				{
					parents.Add(TypeRef.Create(parent));
					parent = parent.BaseType;
				}
				while (parent is not null);
				this.Parents = parents.ToImmutableEquatableArray();
			}
			else
			{
				this.Parents = ImmutableEquatableArray<TypeRef>.Empty;
			}

			var ifaces = type.Interfaces;
			if (ifaces.Length > 0)
			{
				this.Interfaces = ifaces.Select(TypeRef.Create).ToImmutableEquatableArray();
			}
			else
			{
				this.Interfaces = ImmutableEquatableArray<TypeRef>.Empty;
			}
		}

		public ImmutableEquatableArray<TypeRef> Parents { get; set; }

		public TypeRef Ref { get; }

		public string Name { get; }

		/// <summary>Fully qualified assembly name, prefixed with "global::", e.g. global::System.Numerics.BigInteger.</summary>
		public string FullyQualifiedName => this.Ref.FullyQualifiedName;

		public string NameSpace { get; }

		public bool IsSealed { get; }

		public bool IsRecord { get; }

		public TypeKind TypeKind { get; }

		/// <summary>Enumeration that identifies 'special' types</summary>
		public SpecialType SpecialType { get; }

		/// <summary>If this is a <see cref="Nullable{T}"/> (ex: <c>int?</c>) references the underlying concrete type (ex: <c>int</c>); otherwise, <c>null</c></summary>
		public TypeMetadata? UnderlyingType { get; }

		public NullableAnnotation Nullability { get; }

		public ImmutableEquatableArray<TypeRef> Interfaces { get; }

		public override string ToString() => $"{{ Name = {this.Name}, Kind = {TypeKind}, Special = {SpecialType}{(IsSealed?", Sealed" : "")}{(IsRecord?", Record" : "")}, FullName = {this.FullyQualifiedName}}}";

		public void Explain(StringBuilder sb, string? indent = null)
		{
			sb.Append(indent).Append("Name = ").AppendLine(this.Name);
			sb.Append(indent).Append("NameSpace = ").AppendLine(this.NameSpace);
			sb.Append(indent).Append("FullyQualifiedName = ").AppendLine(this.FullyQualifiedName);
			if (this.UnderlyingType != null)
			{
				sb.Append(indent).AppendLine("Underlying Type:");
				this.UnderlyingType.Explain(sb, indent is null ? "- " : ("  " + indent));
				return;
			}
			sb.Append(indent).Append("TypeKind = ").AppendLine(this.TypeKind.ToString());
			if (this.SpecialType != SpecialType.None) sb.Append(indent).Append("SpecialType = ").AppendLine(this.SpecialType.ToString());
			if (this.Nullability != NullableAnnotation.None) sb.Append(indent).Append("Nullability = ").AppendLine(this.Nullability.ToString());
			if (this.IsSealed) sb.Append(indent).AppendLine("IsSealed = true");
			if (this.IsRecord) sb.Append(indent).AppendLine("IsRecord = true");
			if (this.Parents.Count > 0)
			{
				sb.Append(indent).AppendLine("Parents:");
				indent = indent is null ? "- " : ("  " + indent);
				foreach (var @class in this.Parents)
				{
					sb.Append(indent).AppendLine(@class.FullyQualifiedName);
				}
			}
			if (this.Interfaces.Count > 0)
			{
				sb.Append(indent).AppendLine("Interfaces:");
				indent = indent is null ? "- " : ("  " + indent);
				foreach (var iface in this.Interfaces)
				{
					sb.Append(indent).AppendLine(iface.FullyQualifiedName);
				}
			}
		}

		public bool CanBeNull() => this.TypeKind is not TypeKind.Struct || this.SpecialType is SpecialType.System_Nullable_T;

		public bool IsValueType() => this.TypeKind is TypeKind.Struct;

		/// <summary>Returns the type name annotated as nullable, if this is allowed by the type</summary>
		/// <remarks>This should be used when referencing this type as a method argument that allows null</remarks>
		public string GetAnnotatedTypeName() => IsValueType() ? this.FullyQualifiedName : (this.FullyQualifiedName + "?");

		public bool IsNullableOfT()
		{
			return this.SpecialType == SpecialType.System_Nullable_T;
		}

		public bool IsNullableOfT(out TypeMetadata underlyingType)
		{
			if (this.SpecialType is SpecialType.System_Nullable_T)
			{
				underlyingType = this.UnderlyingType!;
				return true;
			}

			underlyingType = null!;
			return false;
		}

		public bool IsArray() => this.TypeKind is TypeKind.Array;

		public bool IsStringLike(bool allowNullables = true)
		{
			if (allowNullables && this.UnderlyingType is not null)
			{
				return this.UnderlyingType.IsStringLike();
			}

			switch (this.SpecialType)
			{
				case SpecialType.System_String:
				{
					return true;
				}
			}

			if (this.NameSpace is "System")
			{
				return this.Name is "Guid" or "Uuid128" or "Uuid96" or "Uuid80" or "Uuid64";
			}

			return false;
		}

		public bool IsBooleanLike(bool allowNullables = true)
		{
			if (allowNullables && this.UnderlyingType is not null)
			{
				return this.UnderlyingType.IsBooleanLike();
			}

			return this.SpecialType == SpecialType.System_Boolean;
		}

		public bool IsNumberLike(bool allowNullables = true)
		{
			if (allowNullables && this.UnderlyingType is not null)
			{
				return this.UnderlyingType.IsNumberLike();
			}

			switch (this.SpecialType)
			{
				case SpecialType.System_UInt16:
				case SpecialType.System_Int16:
				case SpecialType.System_UInt32:
				case SpecialType.System_Int32:
				case SpecialType.System_Int64:
				case SpecialType.System_UInt64:
				case SpecialType.System_Single:
				case SpecialType.System_Double:
				case SpecialType.System_Decimal:
				{
					return true;
				}
			}

			if (this.NameSpace is "System")
			{
				return this.Name is "Half";
			}

			return false;
		}

		public bool IsDateLike(bool allowNullables = true)
		{
			if (allowNullables && this.UnderlyingType is not null)
			{
				return this.UnderlyingType.IsDateLike();
			}

			if (this.SpecialType == SpecialType.System_DateTime)
			{
				return true;
			}

			if (this.Name == "System")
			{
				return this.Name is "DateTime" or "DateOnly";
			}

			if (this.NameSpace == "NodaTime")
			{
				return this.Name is "Instant";
			}

			return false;
		}

		public bool DerivesFrom(string baseClassFullName)
		{
			return false;
		}

		public bool Implements(string interfaceFullName)
		{
			return false;
		}

		public bool IsEnumerable(out TypeMetadata elemType)
		{
			elemType = null!;
			return false;
		}

		public bool IsDictionary(out TypeMetadata keyType, out TypeMetadata valueType)
		{
			keyType = null!;
			valueType = null!;
			return false;
		}

	}

}
