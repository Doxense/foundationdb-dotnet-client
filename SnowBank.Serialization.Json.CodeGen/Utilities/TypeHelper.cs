﻿#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Serialization.Json.CodeGen
{

	public static class TypeHelper
	{

		/// <summary>Tests if a type is nullable (ie: 'int?' or 'DateTime?')</summary>
		[Pure]
		public static bool IsNullableType(this Type type)
		{
			//note: we should check if it is generic, and if its generic definition is 'Nullable<>' but this is too slow at runtime
			// CORRECT but SLOW: return type.IsGenericType && type.Name == "Nullable`1" && typeof(Nullable<>) == type.GetGenericTypeDefinition();
			// => we exploit the fact that the 'Name' of typeof(T?) or typeof(Nullable<T>) is always "Nullable`1", and that it is not possible to derive from a struct...
			return type.Name == "Nullable`1";
		}

		/// <summary>Tests if a type is an anonymous type generated by the compiler (ie: <c>new { foo = ..., bar = .... }</c>)</summary>
		[Pure]
		public static bool IsAnonymousType(this Type type)
		{
			// There is no direct test to detect anonymous types (they are just like any other type)
			// The only distinctive signs are: (cf http://stackoverflow.com/questions/315146/anonymous-types-are-there-any-distingushing-characteristics )
			// * Must be a class
			// * must have the attribute [CompilerGeneratedAttribute]
			// * Must be sealed Elle est sealed
			// * Must derive from System.Object
			// * Must be generic, with has many type arguments than properties
			// * Must have a single ctor, with the same number of parameters
			// * Must override Equals, GetHashcode et ToString(), and nothing else
			// * Name will be "<>f_AnonymousType...." in C#, and "VB$AnonymousType..." in VB.NET

			return type.IsClass
			       && type.IsSealed
			       && type.IsGenericType
			       && typeof(object) == type.BaseType
			       && (type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal) || type.Name.StartsWith("VB$AnonymousType", StringComparison.Ordinal));
			//&& type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false);
		}

		/// <summary>Returns a "human-readable" version of a type's name, including the generic arguments and nested types.</summary>
		/// <returns>"string", "int", "FooBar", "FooBar.NestedBaz", "List&lt;int>", "Dictionary&lt;string, Something&lt;Foo, Bar>>"</returns>
		/// <remarks>This name will NOT be in a format that easily allows retrieve it later, and should mostly be used in error message, logs or debug messages.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string GetFriendlyName(this Type type) => GetCompilableTypeName(type, omitNamespace: true, global: false);

		/// <summary>Returns a "human-readable" version of a type's name, including the generic arguments and nested types.</summary>
		/// <returns>"string", "int", "FooBar", "FooBar.NestedBaz", "List&lt;int>", "Dictionary&lt;string, Something&lt;Foo, Bar>>"</returns>
		/// <remarks>This name will NOT be in a format that easily allows retrieve it later, and should mostly be used in error message, logs or debug messages.</remarks>
		[Pure]
		public static string GetCompilableTypeName(Type type, bool omitNamespace, bool global)
		{
			if (type.IsByRef)
			{
				//TODO: how can we detect "readonly" to use "in" instead?
				return "ref " + GetCompilableTypeName(type.GetElementType()!, omitNamespace, global);
			}

			if (type.IsArray)
			{ // => "Acme[]"

				int rank = type.GetArrayRank();
				var suffix = rank switch
				{
					1 => "[]",
					2 => "[,]",
					_ => type.Name.Substring(type.Name.IndexOf('[')),
				};
				return GetCompilableTypeName(type.GetElementType()!, omitNamespace, global) + suffix;
			}

			if (type == typeof(string))
			{
				return "string";
			}

			if (type.IsPrimitive)
			{
				switch (Type.GetTypeCode(type))
				{
					case TypeCode.Boolean:  return "bool";
					case TypeCode.Char:     return "char";
					case TypeCode.SByte:    return "sbyte";
					case TypeCode.Byte:     return "byte";
					case TypeCode.Int16:    return "short";
					case TypeCode.UInt16:   return "ushort";
					case TypeCode.Int32:    return "int";
					case TypeCode.UInt32:   return "uint";
					case TypeCode.Int64:    return "long";
					case TypeCode.UInt64:   return "ulong";
					case TypeCode.Single:   return "float";
					case TypeCode.Double:   return "double";
					case TypeCode.Decimal:  return "decimal";
					case TypeCode.DateTime: return "DateTime";
				}
			}

			if (type.IsGenericParameter && type.DeclaringMethod == null)
			{
				return type.Name;
			}

			string? globalPrefix = global ? "global::" : null;

			if (type.IsNullableType())
			{
				return GetCompilableTypeName(type.GetGenericArguments()[0], omitNamespace, global) + "?";
			}

			string? parentPrefix = null;
			int outerOffset = 0;
			if (type.IsNested)
			{
				var outer = type.DeclaringType!;
				if (outer.IsGenericType)
				{
					// If this is of the form Outer<T1, T2>.Inner<T3>, the DeclaringType will not have the actual concrete types,
					// we have to get them from the generic type arguments of the inner type. It *looks* like the convention is
					// that Inner.GetGenericArguments() will return the types in order, so { T1, T2, T3 }, and if we know that the
					// outer types takes only N types (2 in the above example), then it will be the first N types that can be used
					// to construct the correct Outer class.
					// Example: if we have "Type inner = typeof(Outer<string, bool>.Inner<Guid>);" then:
					// > inner.DeclaringType returns "Outer<T1, T2>" instead of expected "Outer<string, bool>."
					// > inner.GetGenericArguments() will return { string, int, Guid }
					// > inner.DeclaringType.GetGenericArguments() will return { <T1>, <T2> } of length 2
					// > inner.DeclaringType.MakeGenericType(inner.GetGenericArguments().Take(2).ToArray()) will return the correct "Outer<string, bool>" !

					var innerArgs = type.GetGenericArguments(); // these will be the concrete types
					var outerArgs = outer.GetGenericArguments(); // these will be the generic types
					var concreteArgs = new Type[outerArgs.Length];
					Array.Copy(innerArgs, 0, concreteArgs, 0, concreteArgs.Length);

					//note: we only make a generic version of the type to get its name, and will not invoke any member at runtime
#pragma warning disable IL3050
#pragma warning disable IL2055
					outer = outer.MakeGenericType(concreteArgs);
#pragma warning restore IL2055
#pragma warning restore IL3050
					
					outerOffset = outerArgs.Length;
				}

				if (type.Name.StartsWith("<>"))
				{
					return GetCompilableTypeName(outer, omitNamespace, global);
				}

				parentPrefix = GetCompilableTypeName(outer, omitNamespace, false) + ".";
			}

			if (!omitNamespace)
			{
				parentPrefix = type.Namespace + "." + parentPrefix;
			}

			if (type.IsGenericType)
			{ // => "Acme<string, bool>"


				var args = type.GetGenericArguments();
				if (outerOffset != 0)
				{
					var tmp = args.Length != outerOffset ? new Type[args.Length - outerOffset] : [ ];
					Array.Copy(args, outerOffset, tmp, 0, tmp.Length);
					args = tmp;
				}
				string baseName;
				if (type.IsAnonymousType())
				{
					// the compiler generates "<>f__AnonymousType#<....,....,....>" where # is a unique counter (in hex).
					// we will replace this with "AnonymousType<...,...,...>" which is easier to read, and is similar to what is done by ASP.NET MVC
					baseName = "AnonymousType";
				}
				else
				{
					baseName = type.GetGenericTypeDefinition().Name;
					// generic arguments are after the backtick (`)
					int p = baseName.IndexOf('`');
					if (p > 0) baseName = baseName.Substring(0, p);
				}

				// we will recursively call GetFriendlyName on each generic argument types
				if (args.Length == 0)
				{
					return globalPrefix + parentPrefix + baseName;
				}
				return $"{globalPrefix}{parentPrefix}{baseName}<{string.Join(", ", args.Select(arg => GetCompilableTypeName(arg, omitNamespace, global)))}>";
			}

			if (type.IsPointer)
			{ // => "Acme*"
				string baseName = globalPrefix + parentPrefix + GetCompilableTypeName(type.GetElementType()!, omitNamespace, global);
				return baseName + type.Name.Substring(type.Name.IndexOf('*'));
			}

			// standard case
			return globalPrefix + parentPrefix + type.Name;
		}

		/// <summary>Returns a "human-readable" version of a method's name, including the parameters.</summary>
		/// <returns>"</returns>
		public static string GetFriendlyName(this MethodBase method, bool omitNamespace = true)
		{
			var declaringType = method.DeclaringType;
			var typeName = declaringType != null ? GetCompilableTypeName(declaringType, omitNamespace, global: false) : null;
			var parameters = method.GetParameters();

			if (method.IsConstructor)
			{
				return $"{(method.IsStatic ? "static " : null)}{typeName}({string.Join(", ", parameters.Select(p => $"{p.ParameterType.GetFriendlyName()} {p.Name}"))})";
			}

			//if (parameters.Length == 1 && method.Name.StartsWith("get_"))
			//{
			//	return $"{typeName}.{method.Name}[{parameters[0].ParameterType.GetFriendlyName()} {parameters[0].Name}] {{ get; }}";
			//}
			//if (parameters.Length == 2 && method.Name.StartsWith("set_"))
			//{
			//	return $"{typeName}.{method.Name}[{parameters[0].ParameterType.GetFriendlyName()} {parameters[0].Name}] {{ set; }}";
			//}

			return $"{typeName}{(typeName != null ? "." : null)}{method.Name}({string.Join(", ", parameters.Select(p => $"{p.ParameterType.GetFriendlyName()} {p.Name}"))})";
		}
	
	}
	
}