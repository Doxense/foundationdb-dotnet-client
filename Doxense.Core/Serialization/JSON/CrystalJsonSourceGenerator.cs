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

namespace Doxense.Serialization.Json
{
	using System.CodeDom.Compiler;
	using System.Globalization;
	using System.Reflection;
	using System.Runtime.InteropServices;
	using System.Text;

	public class CrystalJsonSourceGenerator
	{

		public CrystalJsonTypeResolver JsonResolver { get; set; } = CrystalJson.DefaultResolver;

		public string Namespace { get; set; } = "Snowbank.Serialization.Json.Generated";

		public string SerializerContainerName { get; set; } = "GeneratedSerializers";

		public Dictionary<Type, CrystalJsonTypeDefinition> TypeMap { get; } = [ ];

		public HashSet<Assembly> AllowedAssemblies { get; } = [ ];

		private HashSet<Type> ObservedTypes { get; } = [ ];

		private HashSet<Type> FastFieldSerializable { get; } = ComputeFastFieldSerializable();

		private HashSet<Type> FastFieldArraySerializable { get; } = ComputeFastFieldArraySerializable();

		private static HashSet<Type> ComputeFastFieldSerializable()
		{
			var res = new HashSet<Type>();
			foreach (var m in typeof(CrystalJsonWriter).GetMethods(BindingFlags.Public | BindingFlags.Instance))
			{
				if (m.Name != nameof(CrystalJsonWriter.WriteField) || m.IsGenericMethod)
				{
					continue;
				}

				var parameters = m.GetParameters();
				if (parameters.Length != 2) continue;
				if (parameters[0].ParameterType != typeof(JsonEncodedPropertyName)) continue; //HACKHACK: how test a byref type ??

				res.Add(parameters[1].ParameterType);
			}

			return res;
		}

		private static HashSet<Type> ComputeFastFieldArraySerializable()
		{
			var res = new HashSet<Type>();
			foreach (var m in typeof(CrystalJsonWriter).GetMethods(BindingFlags.Public | BindingFlags.Instance))
			{
				if (m.Name != nameof(CrystalJsonWriter.WriteFieldArray) || m.IsGenericMethod)
				{
					continue;
				}

				var parameters = m.GetParameters();
				if (parameters.Length != 2) continue;
				if (parameters[0].ParameterType != typeof(JsonEncodedPropertyName)) continue;

				if (parameters[1].ParameterType.IsEnumerableType(out var elemType))
				{
					res.Add(elemType);
				}
			}

			return res;
		}

		public void AddType(Type type) => AddTypes([ type ]);

		public void AddTypes(Type[] types) => AddTypes(new ReadOnlySpan<Type>(types));

		public void AddTypes(ReadOnlySpan<Type> types)
		{
			var extra = new Queue<CrystalJsonTypeDefinition>();

			foreach (var type in types)
			{
				if (this.TypeMap.ContainsKey(type)) continue;

				var typeDef = this.JsonResolver.ResolveJsonType(type);
				if (typeDef != null)
				{
					extra.Enqueue(typeDef);
					this.AllowedAssemblies.Add(typeDef.Type.Assembly);
				}
			}

			while(extra.Count > 0)
			{
				var typeDef = extra.Dequeue();
				Console.WriteLine("Inspecting " + typeDef.Type.FullName);
				this.TypeMap.Add(typeDef.Type, typeDef);

				foreach(var nested in typeDef.Type.GetNestedTypes())
				{
					if (!this.AllowedAssemblies.Contains(nested.Assembly)) continue;
					var subDef = this.JsonResolver.ResolveJsonType(nested);
					if (subDef != null && subDef.Members.Length > 0 && this.ObservedTypes.Add(subDef.Type))
					{
						extra.Enqueue(subDef);
					}
				}

				foreach(var member in typeDef.Members)
				{
					// is it an array, list, dictionary?

					var actualType = member.Type;

					if (IsDictionary(actualType, out var keyType, out var valueType))
					{
						actualType = valueType;
					}
					else if (IsEnumerable(actualType, out var elemType))
					{
						if (IsArray(actualType, out _) || IsList(actualType, out _))
						{
							actualType = elemType;
						}
					}

					if (!this.AllowedAssemblies.Contains(actualType.Assembly)) continue;

					var subDef = this.JsonResolver.ResolveJsonType(actualType);

					if (subDef != null && subDef.Members.Length > 0 && this.ObservedTypes.Add(subDef.Type))
					{
						extra.Enqueue(subDef);
					}
				}

			}
		}

		private static bool IsStringLike(Type type, bool allowNullables = true)
		{
			if (type == typeof(string) || type == typeof(Guid) || type == typeof(Uuid128) || type == typeof(Uuid96) || type == typeof(Uuid80) || type == typeof(Uuid64))
			{
				return true;
			}
			if (allowNullables)
			{
				if (type == typeof(Guid?) || type == typeof(Uuid128?) || type == typeof(Uuid96?) || type == typeof(Uuid80?) || type == typeof(Uuid64?))
				{
					return true;
				}
			}
			return false;
		}

		private static bool IsBooleanLike(Type type, bool allowNullables = true)
		{
			return type == typeof(bool) || (allowNullables && type == typeof(bool?));
		}

		private static bool IsNumberLike(Type type, bool allowNullables = true)
		{
			if (type == typeof(int)   || type == typeof(long)
			 || type == typeof(uint)  || type == typeof(ulong)
			 || type == typeof(short) || type == typeof(ushort)
			 || type == typeof(float) || type == typeof(double)
			 || type == typeof(Half)  || type == typeof(decimal))
			{
				return true;
			}

			if (allowNullables)
			{
				if (type == typeof(int?)   || type == typeof(long?)
				 || type == typeof(uint?)  || type == typeof(ulong?)
				 || type == typeof(short?) || type == typeof(ushort?)
				 || type == typeof(float?) || type == typeof(double?)
				 || type == typeof(Half?)  || type == typeof(decimal?))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsDateLike(Type type, bool allowNullables = true)
		{
			if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(NodaTime.Instant))
			{
				return true;
			}

			if (allowNullables)
			{
				if (type == typeof(DateTime?) || type == typeof(DateTimeOffset?) || type == typeof(TimeOnly?) || type == typeof(NodaTime.Instant?))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsArray(Type type, [MaybeNullWhen(false)] out Type elementType)
		{
			if (type.IsArray)
			{
				elementType = type.GetElementType()!;
				return true;
			}

			elementType = null;
			return false;
		}

		private static bool IsList(Type type, [MaybeNullWhen(false)] out Type elementType)
		{
			if (type.IsGenericType && type.IsGenericInstanceOf(typeof(List<>), out var listType)
			)
			{
				return type.IsEnumerableType(out elementType);
			}

			elementType = null;
			return false;
		}

		private static bool IsDictionary(Type type, [MaybeNullWhen(false)] out Type keyType, [MaybeNullWhen(false)] out Type valueType)
		{
			return type.IsDictionaryType(out keyType, out valueType);
		}

		private static bool IsEnumerable(Type type, [MaybeNullWhen(false)] out Type elementType)
		{
			return type.IsEnumerableType(out elementType);
		}

		public string GenerateCode()
		{
			var sb = new CodeBuilder();
			sb.Comment("<auto-generated/>");
			sb.NewLine();

			sb.AppendLine("#if NET8_0_OR_GREATER");
			sb.NewLine();
			sb.AppendLine("#nullable enable annotations");
			sb.AppendLine("#nullable enable warnings"); //TODO: REVIEW: to disable or not to disable warnings?
			sb.NewLine();

			sb.Namespace(
				this.Namespace,
				() =>
				{
					// we don't want to have to specify the namespace everytime
					sb.AppendLine($"using {typeof(JsonValue).Namespace};");
					// we also use a lot of helper static methods from this type
					sb.AppendLine($"using static {typeof(JsonSerializerExtensions).Namespace}.{nameof(JsonSerializerExtensions)};");
					sb.NewLine();

					sb.AppendLine("/// <summary>Generated source code for JSON operations on application types</summary>");
					sb.Attribute<DynamicallyAccessedMembersAttribute>([sb.Constant(DynamicallyAccessedMemberTypes.All)]);
					sb.Attribute<GeneratedCodeAttribute>([sb.Constant(nameof(CrystalJsonSourceGenerator)), sb.Constant("0.1")]);
					sb.Attribute<DebuggerNonUserCodeAttribute>();
					sb.Class(
						"public static partial",
						this.SerializerContainerName,
						[ ],
						[ ],
						() =>
						{
							foreach (var typeDef in this.TypeMap.Values)
							{
								GenerateCodeForType(sb, typeDef);
							}
						}
					);
				}
			);

			sb.NewLine();
			sb.AppendLine("#endif");

			return sb.ToStringAndClear();
		}

		private string GetSerializerName(Type type) => type.Name;
		private string GetLocalSerializerRef(Type type) => $"{this.SerializerContainerName}.{GetSerializerName(type)}";

		private string GetReadOnlyProxyName(Type type) => $"{type.Name}ReadOnly";
		private string GetLocalReadOnlyProxyRef(Type type) => $"{this.SerializerContainerName}.{GetReadOnlyProxyName(type)}";

		private string GetMutableProxyName(Type type) => $"{type.Name}Mutable";
		private string GetLocalMutableProxyRef(Type type) => $"{this.SerializerContainerName}.{GetMutableProxyName(type)}";

		/// <summary>Returns the name of the generated const string with the serialized name of this member</summary>
		private string GetPropertyNameRef(CrystalJsonMemberDefinition member) => "PropertyNames." + member.OriginalName;

		/// <summary>Returns the name of the generated static singleton with the definition of this member</summary>
		private string GetPropertyEncodedNameRef(CrystalJsonMemberDefinition member) => "PropertyEncodedNames." + member.OriginalName;

		private string GetPropertyAccessorName(CrystalJsonMemberDefinition member) => $"{member.OriginalName}Accessor";

		private void GenerateCodeForType(CodeBuilder sb, CrystalJsonTypeDefinition typeDef)
		{
			var type = typeDef.Type;
			var jsonConverterType = typeof(IJsonConverter<>).MakeGenericType(type);

			var typeName = sb.TypeName(type);
			var serializerName = GetSerializerName(type);
			var serializerTypeName = serializerName + "JsonConverter";

			sb.AppendLine($"#region {type.GetFriendlyName()}...");
			sb.NewLine();

			sb.AppendLine($"/// <summary>JSON converter for type <see cref=\"{type.FullName}\">{type.GetFriendlyName()}</see></summary>");
			sb.AppendLine($"public static {serializerTypeName} {serializerName} => m_cached{serializerName} ??= new();");
			sb.NewLine();
			sb.AppendLine($"private static {serializerTypeName}? m_cached{serializerName};");
			sb.NewLine();

			sb.AppendLine($"/// <summary>Converts instances of type <see cref=\"T:{type.FullName}\">{type.GetFriendlyName()}</see> to and from JSON.</summary>");
			sb.Class(
				"public sealed",
				serializerTypeName,
				[ sb.TypeName(jsonConverterType) ],
				[ ],
				() =>
				{

					#region JsonSerialize...

					sb.AppendLine("#region Serialization...");
					sb.NewLine();

					sb.AppendLine("/// <summary>Names of all serialized members for this type</summary>");
					sb.AppendLine("public static class PropertyNames");
					sb.EnterBlock("properties");
					sb.NewLine();
					foreach (var member in typeDef.Members)
					{
						sb.AppendLine($"/// <summary>Serialized name of the <see cref=\"{typeName}.{member.OriginalName}\"/> {(member.Member is PropertyInfo ? "property" : "field")} of the <see cref=\"{typeName}\"/> {(type.IsValueType ? "struct" : "class")}</summary>");
						sb.AppendLine($"public const string {member.OriginalName} = {sb.Constant(member.Name)};");
						sb.NewLine();
					}

					sb.AppendLine($"public static string[] GetAllNames() => new [] {{ {string.Join(", ", typeDef.Members.Select(m => GetPropertyNameRef(m)))} }};"); //TODO: PERF!
					sb.NewLine();

					sb.LeaveBlock("properties");
					sb.NewLine();

					sb.AppendLine("/// <summary>Cached encoded names for all serialized members for this type</summary>");
					sb.AppendLine("public static class PropertyEncodedNames");
					sb.EnterBlock("properties");
					sb.NewLine();
					foreach (var member in typeDef.Members)
					{
						sb.AppendLine($"/// <summary>Encoded name of the <see cref=\"{typeName}.{member.OriginalName}\"/> {(member.Member is PropertyInfo ? "property" : "field")} of the <see cref=\"{typeName}\"/> {(type.IsValueType ? "struct" : "class")}</summary>");
						sb.AppendLine($"public static readonly {nameof(JsonEncodedPropertyName)} {member.OriginalName} = new({GetPropertyNameRef(member)});");
						sb.NewLine();
					}
					sb.LeaveBlock("properties");
					sb.NewLine();

#if DISABLED
					sb.AppendLine("internal static class PropertyDefinitions");
					sb.EnterBlock("properties");

					// first we need to generate the cached property names (pre-encoded)
					foreach (var member in typeDef.Members)
					{
						sb.NewLine();
						sb.AppendLine($"/// <summary>Unsafe accessor for the <see cref=\"{typeName}.{member.OriginalName}\"/> {(member.Member is PropertyInfo ? "property" : "field")}</summary>");
						sb.AppendLine($"public static readonly {nameof(CrystalJsonMemberDefinition)} {member.OriginalName} = new()");
						sb.EnterBlock();
						sb.AppendLine($"Type = typeof({sb.TypeName(member.Type)}),");
						sb.AppendLine($"Name = {GetPropertyNameRef(member)},");
						sb.AppendLine($"OriginalName = {sb.Constant(member.OriginalName)},");
						sb.AppendLine($"EncodedName = new ({sb.Constant(member.OriginalName)}),");
						sb.AppendLine($"IsNotNull = {sb.Constant(member.IsNotNull)},");
						sb.AppendLine($"IsRequired = {sb.Constant(member.IsRequired)},");
						sb.AppendLine($"IsKey = {sb.Constant(member.IsKey)},");
						sb.AppendLine($"IsInitOnly = {sb.Constant(member.IsInitOnly)},");
						sb.AppendLine($"Getter = (instance) => {GetPropertyAccessorName(member)}(({typeName}) instance),");
						sb.AppendLine($"Setter = (instance, value) => {GetPropertyAccessorName(member)}(({typeName}) instance) = ({sb.TypeName(member.Type)}) value,"); //BUGBUG: does not work on structs!
						//TODO: Visitor, Binder, ...
						sb.LeaveBlock(semicolon: true);
					}
					sb.NewLine();
					sb.LeaveBlock("properties");
					sb.NewLine();
#endif

					sb.AppendLine($"/// <summary>Writes a JSON representation of an instance of type <see cref=\"{typeName}\"/></summary>");
					sb.Method(
						"public",
						"void",
						nameof(IJsonSerializer<object>.Serialize),
						[
							sb.Parameter<CrystalJsonWriter>("writer"),
							sb.Parameter(typeName + (typeDef.Type.IsValueType ? "" : "?"), "instance")
						],
						() =>
						{

							if (typeDef.DefaultIsNull)
							{
								sb.If(
									"instance is null", () =>
									{
										sb.Call("writer.WriteNull");
										sb.Return();
									}
								);
								sb.NewLine();
							}

							if (type.IsAssignableTo(typeof(IJsonSerializer<>).MakeGenericType(type)))
							{
								sb.Return($"{typeName}.{nameof(IJsonSerializer<object>.Serialize)}(writer, instance);");
							}
							else if (type.IsAssignableTo(typeof(IJsonSerializable)))
							{
								sb.Return($"instance.{nameof(IJsonSerializable.JsonSerialize)}(writer);");
							}
							else
							{
								if (!typeDef.IsSealed)
								{
									sb.If(
										$"instance.GetType() != typeof({typeName})", () =>
										{
											sb.Call(nameof(CrystalJsonVisitor) + "." + nameof(CrystalJsonVisitor.VisitValue), [ "instance", $"typeof({typeName})", "writer" ]);
											sb.Return();
										}
									);
									sb.NewLine();
								}

								sb.AppendLine("var state = writer.BeginObject();");

								foreach (var member in typeDef.Members)
								{
									var propertyName = GetPropertyEncodedNameRef(member);

									sb.NewLine();
									sb.Comment($"{member.Type.GetFriendlyName()} {member.OriginalName} => \"{member.Name}\"");

									if (this.FastFieldSerializable.Contains(member.Type))
									{ // there is a direct, dedicated method for this!
										sb.Call($"writer.{nameof(CrystalJsonWriter.WriteField)}", [ propertyName, $"instance.{member.OriginalName}" ]);
										continue;
									}

									if (this.TypeMap.TryGetValue(member.Type, out var subDef))
									{
										sb.Call($"writer.{nameof(CrystalJsonWriter.WriteField)}", [ propertyName, $"instance.{member.OriginalName}", GetLocalSerializerRef(subDef.Type) ]);
										continue;
									}

									if (member.Type.IsAssignableTo(typeof(IJsonSerializable)))
									{
										sb.Call($"writer.{nameof(CrystalJsonWriter.WriteFieldJsonSerializable)}", [ propertyName, $"instance.{member.OriginalName}" ]);
										continue;
									}

									if (IsDictionary(member.Type, out var keyType, out var valueType))
									{
										if (this.TypeMap.ContainsKey(valueType))
										{
											if (keyType == typeof(string))
											{
												sb.Call($"writer.{nameof(CrystalJsonWriter.WriteFieldDictionary)}", [ propertyName, $"instance.{member.OriginalName}", GetLocalSerializerRef(valueType) ]);
												continue;
											}
										}
										sb.Comment($"TODO: unsupported dictionary type: key={keyType.FullName}, value={valueType.FullName}");
									}
									else if (IsEnumerable(member.Type, out var elemType))
									{
										if (this.FastFieldArraySerializable.Contains(elemType))
										{
											sb.Call($"writer.{nameof(CrystalJsonWriter.WriteFieldArray)}", [ propertyName, $"instance.{member.OriginalName}" ]);
											continue;
										}

										if (this.TypeMap.ContainsKey(elemType))
										{
											sb.Call($"writer.{nameof(CrystalJsonWriter.WriteFieldArray)}", [ propertyName, $"instance.{member.OriginalName}", GetLocalSerializerRef(elemType) ]);
											continue;
										}

										if (elemType.IsAssignableTo(typeof(IJsonSerializable)))
										{
											sb.Call($"writer.{nameof(CrystalJsonWriter.WriteFieldJsonSerializableArray)}", [ propertyName, $"instance.{member.OriginalName}" ]);
											continue;
										}

										if (member.Type == typeof(IEnumerable<>).MakeGenericType(elemType))
										{
											sb.Call($"writer.{nameof(CrystalJsonWriter.WriteFieldArray)}", [ propertyName, $"instance.{member.OriginalName}" ]);
											continue;
										}

										sb.Comment("TODO: unsupported enumerable type: " + member.Type.FullName);
									}

									sb.Call($"writer.{nameof(CrystalJsonWriter.WriteField)}", [ propertyName, $"instance.{member.OriginalName}" ]);
								}

								sb.NewLine();
								sb.Call("writer.EndObject", [ "state" ]);
							}
						}
					);

					sb.AppendLine("#endregion");
					sb.NewLine();

					#endregion

					#region JsonPack...

					sb.AppendLine("#region Packing...");
					sb.NewLine();

					sb.AppendLine($"/// <summary>Converts an instance of <see cref=\"{typeName}\"/> into the equivalent <see cref=\"{nameof(JsonValue)}\"/></summary>");
					sb.Method(
						"public",
						nameof(JsonValue),
						nameof(IJsonPacker<object>.Pack),
						[
							sb.Parameter(typeName + (typeDef.Type.IsValueType ? "" : "?"), "instance"),
							sb.Parameter<CrystalJsonSettings>("settings", nullable: true),
							sb.Parameter<ICrystalJsonTypeResolver>("resolver", nullable: true)
						],
						() =>
						{
							if (typeDef.DefaultIsNull)
							{
								sb.If(
									"instance is null", () =>
									{
										sb.Return(nameof(JsonNull) + "." + nameof(JsonNull.Null));
									}
								);
								sb.NewLine();
							}

							if (type.IsAssignableTo(typeof(IJsonPackable)))
							{ // the type knows how to encode itself
								sb.Return($"instance.{nameof(IJsonPackable.JsonPack)}(instance, settings, resolver);");
							}
							else
							{

								if (!typeDef.IsSealed)
								{
									sb.If(
										$"instance.GetType() != typeof({typeName})", () =>
										{
											sb.Return($"{nameof(JsonValue)}.{nameof(JsonValue.FromValue)}(instance)");
										}
									);
									sb.NewLine();
								}

								// Create the object that will be filled by the rest of the method
								// note: the object always starts as mutable, and is frozen at the end if the settings request for a read-only instance
								sb.AppendLine($"var obj = new {nameof(JsonObject)}({typeDef.Members.Length});");
								sb.NewLine();

								//TODO: sort by "alphabetical with key first" order? or "has defined in the source" order?

								foreach (var member in typeDef.Members)
								{
									var getter = $"instance.{member.OriginalName}";

									sb.Comment($"\"{member.Name}\" => {member.Type.GetFriendlyName()}{(member.IsNullableRefType ? "?" :"")} {member.OriginalName}{(member.IsKey ? ", KEY" : "")}{(member.Member is PropertyInfo ? ", prop" : ", field")}{(member.IsRequired ? ", required" : "")}{(member.HasDefaultValue ? ", hasDefault" : "")}{(member.IsInitOnly ? ", initOnly" : member.ReadOnly ? ", readOnly" : "")}");

									string? converter = null;
									bool handlesNullable = false; // if the converter will accept the Nullable<T> variant of a value type

									var memberType = member.NullableOfType ?? member.Type;
									var propertyNameRef = GetPropertyNameRef(member);

									if (this.TypeMap.TryGetValue(memberType, out var subDef))
									{
										converter = $"{GetLocalSerializerRef(subDef.Type)}.{nameof(IJsonPacker<object>.Pack)}($VALUE$, settings, resolver)";
									}
									else if (memberType.IsAssignableTo(typeof(JsonValue)))
									{ // already JSON!
										converter = $"settings.{nameof(CrystalJsonSettingsExtensions.IsReadOnly)}() ? {getter}?.{nameof(JsonValue.ToReadOnly)}() : {getter}";
									}
									else if (memberType.IsValueType)
									{ // there is a direct, dedicated method for this!

										if (memberType == typeof(bool))
										{
											converter = $"{nameof(JsonBoolean)}.{nameof(JsonBoolean.Return)}($VALUE$)";
											handlesNullable = true;
										}
										else if (memberType == typeof(int) || memberType == typeof(long) || memberType == typeof(float) || memberType == typeof(double) || memberType == typeof(TimeSpan) || memberType == typeof(TimeOnly))
										{
											converter = $"{sb.MethodName<JsonNumber>(nameof(JsonNumber.Return))}($VALUE$)";
											handlesNullable = true;
										}
										else if (memberType == typeof(Guid) || memberType == typeof(Uuid128) || memberType == typeof(Uuid96) || memberType == typeof(Uuid80) || memberType == typeof(Uuid64))
										{
											converter = $"{sb.MethodName<JsonString>(nameof(JsonString.Return))}($VALUE$)";
											handlesNullable = true;
										}
										else if (memberType == typeof(DateTime) || memberType == typeof(DateTimeOffset) || memberType == typeof(DateOnly) || memberType == typeof(NodaTime.Instant))
										{
											converter = $"{sb.MethodName<JsonDateTime>(nameof(JsonDateTime.Return))}($VALUE$)";
											handlesNullable = true;
										}
									}
									else
									{
										if (memberType == typeof(string))
										{
											converter = $"{sb.MethodName<JsonString>(nameof(JsonString.Return))}($VALUE$)";
										}
										else if (IsDictionary(memberType, out var keyType, out var valueType))
										{
											if (keyType == typeof(string))
											{
												if (this.TypeMap.ContainsKey(valueType))
												{
													converter = $"{GetLocalSerializerRef(valueType)}.{nameof(JsonSerializerExtensions.JsonPackObject)}($VALUE$, settings, resolver)";
												}
												else
												{
													converter = $"{nameof(JsonSerializerExtensions.JsonPackEnumerable)}($VALUE$, settings, resolver)";
												}
											}
										}
										else if (IsEnumerable(memberType, out var elemType))
										{
											if (this.TypeMap.ContainsKey(elemType))
											{
												if (IsArray(memberType, out _))
												{
													converter = $"{GetLocalSerializerRef(elemType)}.{nameof(JsonSerializerExtensions.JsonPackArray)}($VALUE$, settings, resolver)";
												}
												else if (IsList(memberType, out _))
												{
													converter = $"{GetLocalSerializerRef(elemType)}.{nameof(JsonSerializerExtensions.JsonPackList)}($VALUE$, settings, resolver)";
												}
												else
												{
													converter = $"{GetLocalSerializerRef(elemType)}.{nameof(JsonSerializerExtensions.JsonPackEnumerable)}($VALUE$, settings, resolver)";
												}
											}
											else
											{
												if (IsArray(memberType, out _))
												{
													converter = $"{nameof(JsonSerializerExtensions.JsonPackArray)}($VALUE$, settings, resolver)";
												}
												else if (IsList(memberType, out _))
												{
													converter = $"{nameof(JsonSerializerExtensions.JsonPackList)}($VALUE$, settings, resolver)";
												}
												else
												{
													converter = $"{nameof(JsonSerializerExtensions.JsonPackEnumerable)}($VALUE$, settings, resolver)";
												}
											}
										}
									}

									if (converter == null)
									{
										//note: for Nullable<T>, memberType has already been unwraped to T, so we have to use member.Type to handle the null case!
										converter = $"{nameof(JsonValue)}.{nameof(JsonValue.FromValue)}<{sb.TypeName(member.Type)}>($VALUE$)";
										handlesNullable = true;
									}

									if (member.NullableOfType != null)
									{
										if (handlesNullable)
										{ // the converter should return either null, or JsonNull.Null if the value is null
											sb.AppendLine($"obj.{nameof(JsonObject.AddIfNotNull)}({propertyNameRef}, {converter.Replace("$VALUE$", getter)});");
										}
										else
										{ // we have to hoist in a temp variable, and do the null check manually
											sb.EnterBlock("nullableOfT");
											sb.AppendLine($"var tmp = {getter};");
											sb.AppendLine($"obj.{nameof(JsonObject.AddIfNotNull)}({propertyNameRef}, tmp.HasValue ? {converter.Replace("$VALUE$", "tmp.Value")} : null);");
											sb.LeaveBlock("nullableOfT");
										}
									}
									else if (member.IsNotNull)
									{
										sb.AppendLine($"obj.{nameof(JsonObject.Add)}({propertyNameRef}, {converter.Replace("$VALUE$", getter)});");
									}
									else
									{
										sb.AppendLine($"obj.{nameof(JsonObject.AddIfNotNull)}({propertyNameRef}, {converter.Replace("$VALUE$", getter)});");
									}

									sb.NewLine();
								}

								sb.AppendLine($"return settings.{nameof(CrystalJsonSettingsExtensions.IsReadOnly)}() ? {nameof(CrystalJsonMarshall)}.{nameof(CrystalJsonMarshall.FreezeTopLevel)}(obj) : obj;");
							}
						}
					);

					sb.AppendLine("#endregion");
					sb.NewLine();

					#endregion

					#region Unpack...

					sb.AppendLine("#region Unpacking...");
					sb.NewLine();

					bool isValueType = type.IsValueType;
					bool smallish = false; //typeDef.Members.Length <= 5;

					if (!smallish)
					{
						// we need a set of accessors for each field or property, in order to set readonly or init-only fields

						foreach (var member in typeDef.Members)
						{
							sb.AppendLine($"/// <summary>Unsafe accessor for the <see cref=\"{typeName}.{member.OriginalName}\"/> {(member.Member is PropertyInfo ? "property" : "field")} of the <see cref=\"{typeName}\"/> {(type.IsValueType ? "struct" : "class")}</summary>");
							sb.AppendLine($"/// <remarks><code>{member.Type.GetFriendlyName().Replace("<", "&lt;").Replace(">", "&gt;")} {member.OriginalName} {{ get;{(member.IsInitOnly ? " init;" : !member.ReadOnly ? " set;" : "")} }}</code></remarks>");
							sb.Attribute<UnsafeAccessorAttribute>([ sb.Constant(UnsafeAccessorKind.Field) ], [ ("Name", sb.Constant("<" + member.OriginalName + ">k__BackingField")) ]);
							sb.AppendLine($"private static extern ref {sb.TypeName(member.Type)}{(member.IsNullableRefType ? "?" : "")} {GetPropertyAccessorName(member)}({(isValueType ? "ref " : "")}{typeName} instance);");
							sb.NewLine();
						}
					}

					sb.AppendLine($"/// <summary>Deserializes a <see cref=\"{nameof(JsonValue)}\"/> into an instance of type <see cref=\"{typeName}\"/></summary>");
					sb.Method(
						"public",
						typeName,
						nameof(IJsonDeserializer<object>.Unpack),
						[ sb.Parameter<JsonValue>("value"), sb.Parameter<ICrystalJsonTypeResolver>("resolver", nullable: true) ],
						() =>
						{

							if (smallish)
							{
								// var obj = value.AsObject();
								// return new TValue()
								// {
								//    Foo = obj.Get<TValue.Foo>("foo", default),
								//    Bar = value.Get<TValue.Bar>("bar", default),
								//    ...
								// }

								sb.AppendLine("var obj = value.AsObject();");
								sb.AppendLine("return new()");
								sb.EnterBlock("new");
								foreach (var member in typeDef.Members)
								{
									sb.AppendLine($"{member.OriginalName} = obj.Get<{sb.TypeName(member.Type)}>({sb.Constant(member.Name)}, default),");
								}
								sb.LeaveBlock("new", semicolon: true);
							}
							else
							{
								// var obj = value.AsObject();
								// var instance = Activator.CreateInstance<TValue>();
								// foreach(var kv in obj)
								// {
								//     switch (kv.Key)
								//     {
								//         case "foo": FooAccessor(instance) = kv.Value.As<TValue.Foo>(resolver); break;
								//         case "bar": BarAccessor(instnace) = kv.Value.As<TValue.TBar>(resolver); break;
								//         ...
								//     }
								// }
								// return instance;

								sb.AppendLine("var obj = value.AsObject();");
								sb.AppendLine($"var instance = {sb.MethodName(typeof(Activator), nameof(Activator.CreateInstance))}<{typeName}>();");
								sb.NewLine();
								sb.AppendLine("foreach (var kv in obj)");
								sb.EnterBlock("foreach");
								sb.AppendLine("switch (kv.Key)");
								sb.EnterBlock("switch");
								foreach (var member in typeDef.Members)
								{
									var getter = GenerateGetterForMember(sb, member);

									sb.AppendLine($"case {GetPropertyNameRef(member)}: {GetPropertyAccessorName(member)}({(isValueType ? "ref " : "")}instance) = {getter}; break;");
								}
								sb.LeaveBlock("switch");
								sb.LeaveBlock("foreach");

								sb.NewLine();
								sb.Return("instance");
							}

						}
					);

					sb.AppendLine("#endregion");
					sb.NewLine();

					#endregion

					#region Proxy...

					sb.AppendLine($"/// <summary>Returns a read-only JSON Proxy that wraps a <see cref=\"{nameof(JsonValue)}\"/> into a type-safe emulation of type <see cref=\"{typeName}\"/></summary>");
					sb.AppendLine($"/// <returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(type)}\"/> that wraps <paramref name=\"value\"/> and exposes all the original members of <see cref=\"{typeName}\"/> as getter-only properties.</returns>\r\n");
					sb.AppendLine($"/// <remarks>");
					sb.AppendLine($"/// <para>The read-only view cannot modify the original JSON value but, unless <paramref name=\"value\"/> is itself read-only, any changes to the original will be reflected in the view.</para>");
					sb.AppendLine($"/// <para>How to use:<code>");
					sb.AppendLine($"/// JsonValue json = {nameof(JsonValue)}.{nameof(JsonValue.Parse)}(/* JSON text */);");
					sb.AppendLine($"/// var proxy = {this.SerializerContainerName}.{GetSerializerName(type)}.AsReadOnly();");
					sb.AppendLine($"/// var value = proxy.{typeDef.Members[0].OriginalName}; // returns the value of the {sb.Constant(typeDef.Members[0].Name)} field exposed as <see cref=\"{sb.TypeName(typeDef.Members[0].Type)}\"/>");
					sb.AppendLine($"/// proxy.{typeDef.Members[0].OriginalName} = newValue; // ERROR: will not compile (there is no setter defined for this member)");
					sb.AppendLine($"/// </code></para>");
					sb.AppendLine($"/// </remarks>");
					sb.AppendLine($"/// <seealso cref=\"ToMutable({nameof(JsonValue)})\">If you need a writable view</seealso>");
					sb.AppendLine($"public {GetLocalReadOnlyProxyRef(type)} AsReadOnly({nameof(JsonValue)} value) => {GetLocalReadOnlyProxyRef(type)}.Create(value, this);");
					sb.NewLine();

					sb.AppendLine($"/// <summary>Converts an instance of type <see cref=\"{typeName}\"/> into a read-only type-safe JSON Proxy.</summary>");
					sb.AppendLine($"/// <returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(type)}\"/> that exposes all the original members of <see cref=\"{typeName}\"/> as getter-only properties.</returns>\r\n");
					sb.AppendLine($"/// <remarks>");
					sb.AppendLine($"/// <para>How to use:<code>");
					sb.AppendLine($"/// var instance = new {type.GetFriendlyName()}() {{ {typeDef.Members[0].OriginalName} = ..., ... }};");
					sb.AppendLine($"/// // ...");
					sb.AppendLine($"/// var proxy = {this.SerializerContainerName}.{GetSerializerName(type)}.AsReadOnly(instance);");
					sb.AppendLine($"/// var value = proxy.{typeDef.Members[0].OriginalName};");
					sb.AppendLine($"/// proxy.{typeDef.Members[0].OriginalName} = /* ... */; // ERROR: will not compile (there is no setter defined for this member)");
					sb.AppendLine($"/// </code></para>");
					sb.AppendLine($"/// </remarks>");
					sb.AppendLine($"public {GetLocalReadOnlyProxyRef(type)} AsReadOnly({typeName}{(type.IsValueType ? "" : "?")} instance) => {GetLocalReadOnlyProxyRef(type)}.Create(instance);");
					sb.NewLine();

					sb.AppendLine($"/// <summary>Returns a writable JSON Proxy that wraps a <see cref=\"{nameof(JsonValue)}\"/> into a type-safe emulation of type <see cref=\"{typeName}\"/></summary>");
					sb.AppendLine($"/// <returns>An instance of <see cref=\"{GetLocalMutableProxyRef(type)}\"/> that wraps <paramref name=\"value\"/> and exposes all the original members of <see cref=\"{typeName}\"/> as writable properties.</returns>\r\n");
					sb.AppendLine($"/// <remarks>");
					sb.AppendLine($"/// <para>If <paramref name=\"value\"/> is read-only, a mutable copy will be created and used instead.</para>");
					sb.AppendLine($"/// <para>If <paramref name=\"value\"/> is mutable, then it will be modified in-place. You can call <see cref=\"{nameof(JsonValue)}.{nameof(JsonValue.ToMutable)}\"/> if you need to make a copy in all cases.</para>");
					sb.AppendLine($"/// <para>How to use:<code>");
					sb.AppendLine($"/// JsonValue json = {nameof(JsonValue)}.{nameof(JsonValue.Parse)}(/* JSON text */);");
					sb.AppendLine($"/// var proxy = {this.SerializerContainerName}.{GetSerializerName(type)}.AsMutable();");
					sb.AppendLine($"/// var value = proxy.{typeDef.Members[0].OriginalName}; // returns the value of the {sb.Constant(typeDef.Members[0].Name)} field exposed as <see cref=\"{sb.TypeName(typeDef.Members[0].Type)}\"/>");
					sb.AppendLine($"/// proxy.{typeDef.Members[0].OriginalName} = newValue; // change the value of the {sb.Constant(typeDef.Members[0].Name)} field");
					sb.AppendLine($"/// </code></para>");
					sb.AppendLine($"/// </remarks>");
					sb.AppendLine($"/// <seealso cref=\"AsReadOnly({nameof(JsonValue)})\">If you need a read-only view</seealso>");
					sb.AppendLine($"public {GetLocalMutableProxyRef(type)} ToMutable({nameof(JsonValue)} value) => {GetLocalMutableProxyRef(type)}.Create(value, converter: this);");
					sb.NewLine();

					sb.AppendLine($"/// <summary>Converts an instance of type <see cref=\"{typeName}\"/> into a read-only type-safe JSON Proxy.</summary>");
					sb.AppendLine($"/// <returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(type)}\"/> that exposes all the original members of <see cref=\"{typeName}\"/> as writable properties.</returns>\r\n");
					sb.AppendLine($"/// <remarks>");
					sb.AppendLine($"/// <para>How to use:<code>");
					sb.AppendLine($"/// var instance = new {type.GetFriendlyName()}() {{ {typeDef.Members[0].OriginalName} = ..., ... }};");
					sb.AppendLine($"/// // ...");
					sb.AppendLine($"/// var proxy = {this.SerializerContainerName}.{GetSerializerName(type)}.ToMutable(instance);");
					sb.AppendLine($"/// var value = proxy.{typeDef.Members[0].OriginalName};");
					sb.AppendLine($"/// proxy.{typeDef.Members[0].OriginalName} = newValue;");
					sb.AppendLine($"/// </code></para>");
					sb.AppendLine($"/// </remarks>");
					sb.AppendLine($"public {GetLocalMutableProxyRef(type)} ToMutable({typeName}{(type.IsValueType ? "" : "?")} instance) => {GetLocalMutableProxyRef(type)}.Create(instance);");
					sb.NewLine();

					#endregion
				}
			);

			string readOnlyProxyTypeName = GetReadOnlyProxyName(type);
			string mutableProxyTypeName = GetMutableProxyName(type);

			string readOnlyProxyInterfaceName = sb.TypeNameGeneric(typeof(IJsonReadOnlyProxy<,,>), [typeName, readOnlyProxyTypeName, mutableProxyTypeName]);
			string mutableProxyInterfaceName = sb.TypeNameGeneric(typeof(IJsonMutableProxy<,,>), [typeName, mutableProxyTypeName, readOnlyProxyTypeName]);

			// IJsonReadOnlyProxy<T>
			sb.AppendLine($"/// <summary>Wraps a <see cref=\"{nameof(JsonObject)}\"/> into a read-only type-safe view that emulates the type <see cref=\"{typeName}\"/></summary>");
			sb.AppendLine($"/// <seealso cref=\"{nameof(IJsonReadOnlyProxy<object>)}{{T}}\"/>");
			sb.Struct(
				"public readonly record",
				readOnlyProxyTypeName,
				[ readOnlyProxyInterfaceName ],
				[ ],
				() =>
				{
					sb.AppendLine($"/// <summary>JSON Object that is wrapped</summary>");
					sb.AppendLine($"private readonly {nameof(JsonObject)} m_obj;");
					sb.NewLine();

					// ctor()
					sb.AppendLine($"public {readOnlyProxyTypeName}({nameof(JsonValue)} value) => m_obj = value.AsObject();"); //HACKACK: BUGBUG:
					sb.NewLine();

					#region Methods...

					sb.AppendLine("#region Public Methods...");
					sb.NewLine();

					// static Create()
					sb.AppendLine($"/// <inheritdoc />");
					sb.AppendLine($"public static {readOnlyProxyTypeName} Create({nameof(JsonValue)} value, {sb.TypeName(jsonConverterType)}? converter = null) => new(value.AsObject());");
					sb.NewLine();

					// static Create()
					sb.AppendLine($"/// <inheritdoc />");
					sb.AppendLine($"public static {readOnlyProxyTypeName} Create({typeName}{(type.IsValueType ? "" : "?")} value, {nameof(CrystalJsonSettings)}? settings = null, {nameof(ICrystalJsonTypeResolver)}? resolver = null) => new({GetLocalSerializerRef(type)}.{nameof(IJsonPacker<object>.Pack)}(value, settings.AsReadOnly(), resolver));");
					sb.NewLine();

					// static Converter
					sb.AppendLine($"/// <inheritdoc />");
					sb.AppendLine($"public static {sb.TypeName(jsonConverterType)} Converter => {GetLocalSerializerRef(type)};");
					sb.NewLine();

					// TValue ToValue()
					sb.AppendLine($"/// <inheritdoc />");
					sb.AppendLine($"public {typeName} ToValue() => {GetLocalSerializerRef(type)}.{nameof(IJsonDeserializer<object>.Unpack)}(m_obj);"); //TODO: resolver?
					sb.NewLine();

					// JsonObject ToJson()
					sb.AppendLine($"/// <inheritdoc />");
					sb.AppendLine($"public {nameof(JsonValue)} ToJson() => m_obj;");
					sb.NewLine();

					// TMutable ToMutable()
					sb.AppendLine($"/// <inheritdoc />");
					sb.AppendLine($"public {mutableProxyTypeName} ToMutable() => new(m_obj.Copy());");
					sb.NewLine();

					// TReadOnly With(Action<TMutable>)
					sb.AppendLine($"/// <inheritdoc />");
					sb.AppendLine($"public {readOnlyProxyTypeName} With(Action<{mutableProxyTypeName}> modifier)");
					sb.EnterBlock();
					sb.AppendLine($"var copy = m_obj.Copy();");
					sb.AppendLine($"modifier(new(copy));");
					sb.AppendLine($"return new(copy.{nameof(JsonObject.Freeze)}());");
					sb.LeaveBlock();
					sb.NewLine();

					// IJsonSerializable
					sb.AppendLine($"void {nameof(IJsonSerializable)}.{nameof(IJsonSerializable.JsonSerialize)}({nameof(CrystalJsonWriter)} writer) => m_obj.{nameof(IJsonSerializable.JsonSerialize)}(writer);");
					sb.NewLine();

					// IJsonPackable
					sb.AppendLine($"{nameof(JsonValue)} {nameof(IJsonPackable)}.{nameof(IJsonPackable.JsonPack)}({nameof(CrystalJsonSettings)} settings, {nameof(ICrystalJsonTypeResolver)} resolver) => m_obj;");
					sb.NewLine();

					sb.AppendLine("#endregion");
					sb.NewLine();

					#endregion

					#region Members

					sb.AppendLine("#region Public Members...");
					sb.NewLine();

					foreach (var member in typeDef.Members)
					{
						sb.AppendLine($"/// <inheritdoc cref=\"{type.GetFriendlyName()}.{member.OriginalName}\" />");

						var defaultValue = sb.Constant(member.Type, member.DefaultValue);

						string? getterExpr = null;
						string proxyType = sb.TypeName(member.Type);
						if (member.IsNullableRefType)
						{
							proxyType += "?";
						}

						if (this.TypeMap.ContainsKey(member.Type))
						{
							getterExpr = $"new(m_obj.{(member.IsNullableRefType ? "GetObjectOrDefault" : member.IsRequired ? "GetObject" : "GetObjectOrEmpty")}({sb.Constant(member.Name)}))";
							proxyType = GetLocalReadOnlyProxyRef(member.Type);
						}
						else if (IsStringLike(member.Type) || IsStringLike(member.Type) || IsBooleanLike(member.Type) || IsNumberLike(member.Type) || IsDateLike(member.Type))
						{
							//use default getter
							getterExpr = null;
						}
						else if (member.Type.IsAssignableTo(typeof(JsonValue)))
						{
							if (member.Type == typeof(JsonObject))
							{
								getterExpr = $"m_obj.{(member.IsNullableRefType ? "GetObjectOrDefault" : member.IsRequired ? "GetObject" : "GetObjectOrEmpty")}({sb.Constant(member.Name)})";
							}
							else if (member.Type == typeof(JsonArray))
							{
								getterExpr = $"m_obj.{(member.IsNullableRefType ? "GetArrayOrDefault" : member.IsRequired ? "GetArray" : "GetArrayOrEmpty")}({sb.Constant(member.Name)})";
							}
							else
							{
								getterExpr = $"m_obj[{sb.Constant(member.Name)}]";
							}
						}
						else if (member.Type.IsAssignableTo(typeof(IJsonSerializable)))
						{
							getterExpr = null; //TODO?
						}
						else if (IsDictionary(member.Type, out var keyType, out var valueType))
						{
							if (keyType == typeof(string))
							{
								if (this.TypeMap.ContainsKey(valueType))
								{
									getterExpr = $"new(m_obj.{(member.IsNullableRefType ? "GetObjectOrDefault" : member.IsRequired ? "GetObject" : "GetObjectOrEmpty")}({sb.Constant(member.Name)}))";
									proxyType = $"{nameof(JsonReadOnlyProxyObject<object>)}<{sb.TypeName(valueType)}, {GetLocalReadOnlyProxyRef(valueType)}>";
								}
								else
								{
									getterExpr = $"new(m_obj.{(member.IsNullableRefType ? "GetObjectOrDefault" : member.IsRequired ? "GetObject" : "GetObjectOrEmpty")}({sb.Constant(member.Name)}))";
									proxyType = $"{nameof(JsonReadOnlyProxyObject<object>)}<{sb.TypeName(valueType)}>";
								}
							}
						}
						else if (IsEnumerable(member.Type, out var elemType))
						{
							if (this.TypeMap.ContainsKey(elemType))
							{
								getterExpr = $"new(m_obj.{(member.IsNullableRefType ? "GetArrayOrDefault" : member.IsRequired ? "GetArray" : "GetArrayOrEmpty")}({sb.Constant(member.Name)}))";
								proxyType = $"{nameof(JsonReadOnlyProxyArray<object>)}<{sb.TypeName(elemType)}, {GetLocalReadOnlyProxyRef(elemType)}>";
							}
							else
							{
								getterExpr = $"new(m_obj.{(member.IsNullableRefType ? "GetArrayOrDefault" : member.IsRequired ? "GetArray" : "GetArrayOrEmpty")}({sb.Constant(member.Name)}))";
								proxyType = $"{nameof(JsonReadOnlyProxyArray<object>)}<{sb.TypeName(elemType)}>";
							}
						}

						if (getterExpr == null)
						{
							if (member.IsNullableRefType)
							{
								getterExpr = $"m_obj.Get<{sb.TypeName(member.Type)}?>({sb.Constant(member.Name)}, {defaultValue})";
							}
							else if (member.IsRequired)
							{
								getterExpr = $"m_obj.Get<{sb.TypeName(member.Type)}>({sb.Constant(member.Name)})";
							}
							else if (member.IsNonNullableValueType)
							{
								getterExpr = $"m_obj.Get<{sb.TypeName(member.Type)}>({sb.Constant(member.Name)}, {defaultValue})";
							}
							else
							{
								getterExpr = $"m_obj.Get<{sb.TypeName(member.Type)}>({sb.Constant(member.Name)}, {defaultValue}!)";
							}
						}

						sb.AppendLine($"public {proxyType} {member.OriginalName} => {getterExpr};");

						sb.NewLine();
					}

					sb.AppendLine("#endregion");
					sb.NewLine();

					#endregion
				}
			);

			// IJsonMutableProxy<T>
			sb.AppendLine($"/// <summary>Wraps a <see cref=\"{nameof(JsonObject)}\"/> into a writable type-safe view that emulates the type <see cref=\"{typeName}\"/></summary>");
			sb.AppendLine($"/// <seealso cref=\"{nameof(IJsonMutableProxy<object>)}{{T}}\"/>");
			sb.Record(
				"public sealed",
				mutableProxyTypeName,
				[
					sb.TypeName<JsonMutableProxyObjectBase>(),
					mutableProxyInterfaceName
				],
				[],
				() =>
				{
					//sb.AppendLine($"private readonly {nameof(JsonObject)} m_obj;");
					//sb.NewLine();

					// ctor()
					sb.AppendLine($"public {mutableProxyTypeName}({nameof(JsonValue)} value, {nameof(IJsonMutableParent)}? parent = null, {nameof(JsonEncodedPropertyName)}? name = null, int index = 0) : base(value, parent, name, index)");
					sb.EnterBlock();
					sb.LeaveBlock();
					sb.NewLine();

					#region Methods...

					sb.AppendLine("#region Public Methods...");
					sb.NewLine();

					// static Create()
					sb.AppendLine($"/// <inheritdoc />");
					sb.AppendLine($"public static {mutableProxyTypeName} Create({nameof(JsonValue)} value, {nameof(IJsonMutableParent)}? parent = null, {nameof(JsonEncodedPropertyName)}? name = null, int index = 0, {sb.TypeName(jsonConverterType)}? converter = null) => new(value, parent, name, index);");
					sb.NewLine();

					// static Create()
					sb.AppendLine($"/// <inheritdoc />");
					sb.AppendLine($"public static {mutableProxyTypeName} Create({typeName}{(type.IsValueType ? "" : "?")} value, {nameof(CrystalJsonSettings)}? settings = null, {nameof(ICrystalJsonTypeResolver)}? resolver = null) => new({GetLocalSerializerRef(type)}.{nameof(IJsonPacker<object>.Pack)}(value, settings.AsMutable(), resolver));");
					sb.NewLine();

					// static Converter
					sb.AppendLine($"/// <inheritdoc />");
					sb.AppendLine($"public static {sb.TypeName(jsonConverterType)} Converter => {GetLocalSerializerRef(type)};");
					sb.NewLine();

					// TMutable FromValue(TValue)
					sb.AppendLine($"/// <summary>Pack an instance of <see cref=\"{sb.TypeName(type)}\"/> into a mutable JSON proxy</summary>");
					sb.AppendLine($"public static {mutableProxyTypeName} FromValue({typeName} value)");
					sb.EnterBlock();
					sb.AppendLine($"{sb.MethodName(typeof(Contract), nameof(Contract.NotNull))}(value);");
					sb.AppendLine($"return new({GetLocalSerializerRef(type)}.{nameof(IJsonPacker<object>.Pack)}(value, {nameof(CrystalJsonSettings)}.{nameof(CrystalJsonSettings.Json)}));");
					sb.LeaveBlock();
					sb.NewLine();

					// TValue ToValue()
					sb.AppendLine($"/// <inheritdoc />");
					sb.AppendLine($"public {typeName} ToValue() => {GetLocalSerializerRef(type)}.{nameof(IJsonDeserializer<object>.Unpack)}(m_obj);"); //TODO: resolver?
					sb.NewLine();

					//// JsonObject ToJson()
					//sb.AppendLine($"/// <inheritdoc />");
					//sb.AppendLine($"public {nameof(JsonValue)} ToJson() => m_obj;");
					//sb.NewLine();

					// TReadOnly ToReadOnly()
					sb.AppendLine($"/// <inheritdoc />");
					sb.AppendLine($"public {readOnlyProxyTypeName} ToReadOnly() => new (m_obj.{nameof(JsonObject.ToReadOnly)}());");
					sb.NewLine();

					//// IJsonSerializable
					//sb.AppendLine("/// <inheritdoc />");
					//sb.AppendLine($"void {nameof(IJsonSerializable)}.{nameof(IJsonSerializable.JsonSerialize)}({nameof(CrystalJsonWriter)} writer) => m_obj.{nameof(IJsonSerializable.JsonSerialize)}(writer);");
					//sb.NewLine();

					//// IJsonPackable
					//sb.AppendLine("/// <inheritdoc />");
					//sb.AppendLine($"{nameof(JsonValue)} {nameof(IJsonPackable)}.{nameof(IJsonPackable.JsonPack)}({nameof(CrystalJsonSettings)} settings, {nameof(ICrystalJsonTypeResolver)} resolver) => settings.{nameof(CrystalJsonSettingsExtensions.IsReadOnly)}() ? m_obj.{nameof(JsonObject.ToReadOnly)}() : m_obj;");
					//sb.NewLine();

					sb.AppendLine("#endregion");
					sb.NewLine();

					#endregion

					#region Members

					sb.AppendLine("#region Public Members...");
					sb.NewLine();
					foreach (var member in typeDef.Members)
					{
						var defaultValue = sb.Constant(member.Type, member.DefaultValue);

						string proxyType = sb.TypeName(member.Type);
						if (member.IsNullableRefType)
						{
							proxyType += "?";
						}

						string? setterExpr = null;
						string? getterExpr = null;

						if (this.TypeMap.ContainsKey(member.Type))
						{
							proxyType = GetLocalMutableProxyRef(member.Type);
							getterExpr = $"new(m_obj.{(member.IsRequired ? "GetObject" : "GetObjectOrEmpty")}({sb.Constant(member.Name)}), name: {serializerTypeName}.{GetPropertyEncodedNameRef(member)})";
							setterExpr = $"m_obj[{sb.Constant(member.Name)}] = value.ToJson()";
						}
						else if (IsStringLike(member.Type) || IsStringLike(member.Type) || IsBooleanLike(member.Type) || IsNumberLike(member.Type) || IsDateLike(member.Type))
						{
							if (member.IsNullableRefType)
							{
								if (member.Type == typeof(string))
								{
									getterExpr ??= $"m_obj[{sb.Constant(member.Name)}].ToStringOrDefault({defaultValue})";
								}
								else
								{
									getterExpr ??= $"m_obj.Get<{sb.TypeName(member.Type)}?>({sb.Constant(member.Name)}, {defaultValue})";
								}
							}
							else if (member.IsRequired)
							{
								if (member.Type == typeof(string))
								{
									getterExpr ??= $"m_obj.{nameof(JsonObject.GetValue)}({sb.Constant(member.Name)}).ToString()";
								}
								else if (member.Type == typeof(int))
								{
									getterExpr ??= $"m_obj.{nameof(JsonObject.GetValue)}({sb.Constant(member.Name)}).ToInt32()";
								}
								else
								{
									getterExpr ??= $"m_obj.Get<{sb.TypeName(member.Type)}>({sb.Constant(member.Name)})";
								}
							}
							else if (member.IsNonNullableValueType)
							{
								if (member.Type == typeof(int))
								{
									getterExpr ??= $"m_obj[{sb.Constant(member.Name)}].ToInt32({defaultValue})";
								}
								else
								{
									getterExpr ??= $"m_obj.Get<{sb.TypeName(member.Type)}>({sb.Constant(member.Name)}, {defaultValue})";
								}
							}
							else
							{
								getterExpr ??= $"m_obj.Get<{sb.TypeName(member.Type)}>({sb.Constant(member.Name)}, {defaultValue}!)";
							}

							if (IsStringLike(member.Type, allowNullables: true))
							{
								setterExpr = $"m_obj[{sb.Constant(member.Name)}] = {nameof(JsonString)}.{nameof(JsonString.Return)}(value)";
							}
							else if (IsBooleanLike(member.Type, allowNullables: true))
							{
								setterExpr = $"m_obj[{sb.Constant(member.Name)}] = {nameof(JsonBoolean)}.{nameof(JsonBoolean.Return)}(value)";
							}
							else if (IsNumberLike(member.Type, allowNullables: true))
							{
								setterExpr = $"m_obj[{sb.Constant(member.Name)}] = {nameof(JsonNumber)}.{nameof(JsonNumber.Return)}(value)";
							}
							else if (IsDateLike(member.Type, allowNullables: true))
							{
								setterExpr = $"m_obj[{sb.Constant(member.Name)}] = {nameof(JsonDateTime)}.{nameof(JsonDateTime.Return)}(value)";
							}
						}
						else if (member.Type.IsAssignableTo(typeof(JsonValue)))
						{
							setterExpr = $"m_obj[{sb.Constant(member.Name)}] = value ?? JsonNull.Null";

							if (member.Type == typeof(JsonObject))
							{
								getterExpr = $"m_obj.{(member.IsRequired ? "GetObject" : member.IsNotNull ? "GetObjectOrEmpty" : "GetObjectOrDefault")}({sb.Constant(member.Name)}){(member.IsNotNull ? "" : "?")}.ToMutable()";
							}
							else if (member.Type == typeof(JsonArray))
							{
								getterExpr = $"m_obj.{(member.IsRequired ? "GetArray" : member.IsNotNull ? "GetArrayOrEmpty" : "GetArrayOrDefault")}({sb.Constant(member.Name)}){(member.IsNotNull ? "" : "?")}.ToMutable()";
							}
							else
							{
								getterExpr = $"m_obj.{(member.IsRequired ? "GetValue" : "GetValueOrDefault")}({sb.Constant(member.Name)}){(member.IsNotNull ? "" : "?")}.ToMutable()";
							}
						}
						else if (IsDictionary(member.Type, out var keyType, out var valueType))
						{
							if (keyType == typeof(string))
							{
								if (this.TypeMap.ContainsKey(valueType))
								{
									proxyType = sb.TypeNameGeneric(typeof(JsonMutableProxyDictionary<,>), sb.TypeName(valueType), GetLocalMutableProxyRef(valueType));
									getterExpr = $"new(m_obj[{sb.Constant(member.Name)}], parent: this, name: {serializerTypeName}.{GetPropertyEncodedNameRef(member)})";
									setterExpr = $"m_obj[{sb.Constant(member.Name)}] = value.ToJson()";
								}
							}
						}
						else if (IsEnumerable(member.Type, out var elemType))
						{
							if (this.TypeMap.ContainsKey(elemType))
							{
								proxyType = sb.TypeNameGeneric(typeof(JsonMutableProxyArray<,>), sb.TypeName(elemType), GetLocalMutableProxyRef(elemType));
								getterExpr = $"new(m_obj[{sb.Constant(member.Name)}], parent: this, name: {serializerTypeName}.{GetPropertyEncodedNameRef(member)})";
								setterExpr = $"m_obj[{sb.Constant(member.Name)}] = value.ToJson()";
							}
						}

						if (getterExpr == null)
						{
							if (member.IsNullableRefType)
							{
								getterExpr = $"m_obj.Get<{sb.TypeName(member.Type)}?>({sb.Constant(member.Name)}, {defaultValue})";
							}
							else if (member.IsRequired)
							{
								getterExpr = $"m_obj.Get<{sb.TypeName(member.Type)}>({sb.Constant(member.Name)})";
							}
							else if (member.IsNonNullableValueType)
							{
								getterExpr = $"m_obj.Get<{sb.TypeName(member.Type)}>({sb.Constant(member.Name)}, {defaultValue})";
							}
							else
							{
								getterExpr = $"m_obj.Get<{sb.TypeName(member.Type)}>({sb.Constant(member.Name)}, {defaultValue}!)";
							}
						}

						if (setterExpr == null)
						{
							if (member.IsNullableRefType)
							{
								setterExpr ??= $"m_obj.Set<{sb.TypeName(member.Type)}?>({sb.Constant(member.Name)}, value)";
							}
							else
							{
								setterExpr ??= $"m_obj.Set<{sb.TypeName(member.Type)}>({sb.Constant(member.Name)}, value)";
							}
						}

						sb.AppendLine($"/// <inheritdoc cref=\"{type.GetFriendlyName()}.{member.OriginalName}\" />");
						sb.AppendLine($"public {proxyType} {member.OriginalName}");
						sb.EnterBlock();
						sb.AppendLine($"get => {getterExpr};");
						sb.AppendLine($"set => {setterExpr};");
						sb.LeaveBlock();
						sb.NewLine();
					}

					sb.AppendLine("#endregion");
					sb.NewLine();

					#endregion

				}
			);

			sb.AppendLine("#endregion");
			sb.NewLine();

		}

		private string? GenerateGetterForMember(CodeBuilder sb, CrystalJsonMemberDefinition member)
		{
			var memberType = member.Type;
			var defaultValue = sb.Constant(member.Type, member.DefaultValue);
			var defaultValueOrEmpty = member.HasDefaultValue ? defaultValue : "";

			// Do we have codegen for this type?
			if (this.TypeMap.ContainsKey(memberType))
			{
				if (member.IsNullableRefType)
				{
					return $"{GetLocalSerializerRef(memberType)}.{nameof(IJsonDeserializer<object>.Unpack)}(kv.Value, resolver)";
				}
				else if (member.IsRequired)
				{
					return $"{GetLocalSerializerRef(memberType)}.{nameof(JsonSerializerExtensions.UnpackRequired)}(kv.Value, resolver: resolver, fieldName: {sb.Constant(member.OriginalName)})";
				}
				else
				{
					return $"{GetLocalSerializerRef(memberType)}.{nameof(IJsonDeserializer<object>.Unpack)}(kv.Value, resolver)!";
				}
			}

			// Does the type can deserialize itself?
			if (memberType.IsGenericInstanceOf(typeof(IJsonDeserializable<>)))
			{
				// some types implement this method explicitly, so we have to go through a helper in JsonValueExtensions
				// => JsonSerializerExtensions.Deserialize<T>(kv.Value, resolver)

				if (member.IsNullableRefType)
				{
					return $"/* nullable_deserializable */ {nameof(JsonSerializerExtensions.Unpack)}<{sb.TypeName(memberType)}>(kv.Value, {defaultValue}, resolver: resolver)";
				}
				else if (member.IsRequired)
				{
					return $"/* required_deserializable */ {nameof(JsonSerializerExtensions.UnpackRequired)}<{sb.TypeName(memberType)}>(kv.Value, resolver: resolver, fieldName: {sb.Constant(member.OriginalName)})";
				}
				else
				{
					return $"/* notnull_deserializable */ {nameof(JsonSerializerExtensions.Unpack)}<{sb.TypeName(memberType)}>(kv.Value, {defaultValue}, resolver: resolver)!";
				}
			}

			if (member.NullableOfType != null && member.NullableOfType.IsGenericInstanceOf(typeof(IJsonDeserializable<>)))
			{
				return $"/* vt_nullable_deserializable */ {nameof(JsonSerializerExtensions.UnpackNullable)}<{sb.TypeName(member.NullableOfType)}>(kv.Value, resolver: resolver)";
			}

			if (memberType.IsValueType)
			{
				if (!memberType.IsNullableType())
				{
					if (memberType == typeof(bool)) return $"kv.Value.{nameof(JsonValue.ToBoolean)}({defaultValueOrEmpty})";
					if (memberType == typeof(int)) return $"kv.Value.{nameof(JsonValue.ToInt32)}({defaultValueOrEmpty})";
					if (memberType == typeof(long)) return $"kv.Value.{nameof(JsonValue.ToInt64)}({defaultValueOrEmpty})";
					if (memberType == typeof(float)) return $"kv.Value.{nameof(JsonValue.ToSingle)}({defaultValueOrEmpty})";
					if (memberType == typeof(double)) return $"kv.Value.{nameof(JsonValue.ToDouble)}({defaultValueOrEmpty})";
					if (memberType == typeof(Guid)) return $"kv.Value.{nameof(JsonValue.ToGuid)}({defaultValueOrEmpty})";
					if (memberType == typeof(Uuid128)) return $"kv.Value.{nameof(JsonValue.ToUuid128)}({defaultValueOrEmpty})";
					if (memberType == typeof(Uuid64)) return $"kv.Value.{nameof(JsonValue.ToUuid64)}({defaultValueOrEmpty})";
					if (memberType == typeof(DateTime)) return $"kv.Value.{nameof(JsonValue.ToDateTime)}({defaultValueOrEmpty})";
					if (memberType == typeof(DateTimeOffset)) return $"kv.Value.{nameof(JsonValue.ToDateTimeOffset)}({defaultValueOrEmpty})";
					if (memberType == typeof(NodaTime.Instant)) return $"kv.Value.{nameof(JsonValue.ToInstant)}({defaultValueOrEmpty})";
					if (memberType == typeof(NodaTime.Duration)) return $"kv.Value.{nameof(JsonValue.ToDuration)}({defaultValueOrEmpty})";
					if (memberType == typeof(char)) return $"kv.Value.{nameof(JsonValue.ToChar)}({defaultValueOrEmpty})";
					if (memberType == typeof(uint)) return $"kv.Value.{nameof(JsonValue.ToUInt32)}({defaultValueOrEmpty})";
					if (memberType == typeof(ulong)) return $"kv.Value.{nameof(JsonValue.ToUInt64)}({defaultValueOrEmpty})";
					if (memberType == typeof(byte)) return $"kv.Value.{nameof(JsonValue.ToByte)}({defaultValueOrEmpty})";
					if (memberType == typeof(sbyte)) return $"kv.Value.{nameof(JsonValue.ToSByte)}({defaultValueOrEmpty})";
					if (memberType == typeof(decimal)) return $"kv.Value.{nameof(JsonValue.ToDecimal)}({defaultValueOrEmpty})";
					if (memberType == typeof(Half)) return $"kv.Value.{nameof(JsonValue.ToDecimal)}({defaultValueOrEmpty})";
#if NET8_0_OR_GREATER
					if (memberType == typeof(Int128)) return $"kv.Value.{nameof(JsonValue.ToInt128)}({defaultValueOrEmpty})";
					if (memberType == typeof(UInt128)) return $"kv.Value.{nameof(JsonValue.ToUInt128)}({defaultValueOrEmpty})";
#endif

					return member.HasDefaultValue
						? $"/* vt_has_default */ kv.Value.As<{sb.TypeName(memberType)}>(defaultValue: {defaultValue}, resolver: resolver)"
						: $"/* vt_no_default */ kv.Value.As<{sb.TypeName(memberType)}>(resolver: resolver)";
				}
				else
				{
					if (memberType == typeof(bool?)) return $"kv.Value.{nameof(JsonValue.ToBooleanOrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(int?)) return $"kv.Value.{nameof(JsonValue.ToInt32OrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(long?)) return $"kv.Value.{nameof(JsonValue.ToInt64OrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(float?)) return $"kv.Value.{nameof(JsonValue.ToSingleOrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(double?)) return $"kv.Value.{nameof(JsonValue.ToDoubleOrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(Guid?)) return $"kv.Value.{nameof(JsonValue.ToGuidOrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(Uuid128?)) return $"kv.Value.{nameof(JsonValue.ToUuid128OrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(Uuid64?)) return $"kv.Value.{nameof(JsonValue.ToUuid64OrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(DateTime?)) return $"kv.Value.{nameof(JsonValue.ToDateTimeOrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(DateTimeOffset?)) return $"kv.Value.{nameof(JsonValue.ToDateTimeOffsetOrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(NodaTime.Instant?)) return $"kv.Value.{nameof(JsonValue.ToInstantOrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(NodaTime.Duration?)) return $"kv.Value.{nameof(JsonValue.ToDurationOrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(char?)) return $"kv.Value.{nameof(JsonValue.ToCharOrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(uint?)) return $"kv.Value.{nameof(JsonValue.ToUInt32OrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(ulong?)) return $"kv.Value.{nameof(JsonValue.ToUInt64OrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(byte?)) return $"kv.Value.{nameof(JsonValue.ToByteOrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(sbyte?)) return $"kv.Value.{nameof(JsonValue.ToSByteOrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(decimal?)) return $"kv.Value.{nameof(JsonValue.ToDecimalOrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(Half?)) return $"kv.Value.{nameof(JsonValue.ToDecimalOrDefault)}({defaultValueOrEmpty})";
#if NET8_0_OR_GREATER
					if (memberType == typeof(Int128?)) return $"kv.Value.{nameof(JsonValue.ToInt128OrDefault)}({defaultValueOrEmpty})";
					if (memberType == typeof(UInt128?)) return $"kv.Value.{nameof(JsonValue.ToUInt128OrDefault)}({defaultValueOrEmpty})";
#endif
				}
			}
			else
			{
				if (memberType == typeof(string))
				{
					return member.IsRequired ? $"kv.Value.RequiredField({sb.Constant(member.OriginalName)}).{nameof(JsonValue.ToString)}()"
					     : member.IsNullableRefType ? $"kv.Value.{nameof(JsonValue.ToStringOrDefault)}({defaultValueOrEmpty})"
					     : $"kv.Value.{nameof(JsonValue.ToStringOrDefault)}({defaultValueOrEmpty})!";
				}

				if (memberType == typeof(JsonValue))
				{
					return member.IsRequired ? $"kv.Value.RequiredField({sb.Constant(member.OriginalName)})" : "kv.Value";
				}

				if (memberType == typeof(JsonObject))
				{
					if (member.IsNullableRefType)
					{
						return $"kv.Value.{nameof(JsonValueExtensions.AsObjectOrDefault)}()";
					}
					else if (member.IsRequired)
					{
						return $"kv.Value.RequiredField({sb.Constant(member.OriginalName)}).{nameof(JsonValueExtensions.AsObject)}()";
					}
					else
					{
						return $"kv.Value.{nameof(JsonValueExtensions.AsObjectOrEmpty)}()";
					}
				}

				if (memberType == typeof(JsonArray))
				{
					if (member.IsNullableRefType)
					{
						return $"kv.Value.{nameof(JsonValueExtensions.AsArrayOrDefault)}()";
					}
					else if (member.IsRequired)
					{
						return $"kv.Value.RequiredField({sb.Constant(member.OriginalName)}).{nameof(JsonValueExtensions.AsArray)}()";
					}
					else
					{
						return $"kv.Value.{nameof(JsonValueExtensions.AsArrayOrEmpty)}()";
					}
				}

				if (memberType.IsDictionaryType(out var keyType, out var valueType))
				{
					if (keyType == typeof(string))
					{
						if (member.HasDefaultValue)
						{
							return $"{GetLocalSerializerRef(valueType)}.{nameof(JsonSerializerExtensions.JsonDeserializeDictionary)}(kv.Value, defaultValue: {defaultValue}, resolver: resolver)";
						}
						else
						{
							return $"{GetLocalSerializerRef(valueType)}.{nameof(JsonSerializerExtensions.JsonDeserializeDictionary)}(kv.Value, resolver: resolver)!";
						}
					}
					//TODO??
				}
				else if (memberType.IsEnumerableType(out var elemType))
				{
					//TODO: should this be controlled by an attribute?
					var elemDefaultValue = sb.DefaultOf(elemType);

					if (this.TypeMap.ContainsKey(elemType))
					{
						if (IsArray(memberType, out _))
						{
							if (member.IsRequired)
							{
								return $"/* array_required */ {GetLocalSerializerRef(elemType)}.{nameof(JsonSerializerExtensions.JsonDeserializeArrayRequired)}(kv.Value, resolver: resolver, fieldName: {sb.Constant(member.OriginalName)})";
							}
							else if (member.HasDefaultValue)
							{
								return $"{GetLocalSerializerRef(elemType)}.{nameof(JsonSerializerExtensions.JsonDeserializeArray)}(kv.Value, defaultValue: {defaultValue}, resolver: resolver, fieldName: {sb.Constant(member.OriginalName)})";
							}
							else
							{
								return $"{GetLocalSerializerRef(elemType)}.{nameof(JsonSerializerExtensions.JsonDeserializeArray)}(kv.Value, resolver: resolver, fieldName: {sb.Constant(member.OriginalName)})!";
							}
						}
						if (IsList(memberType, out _))
						{
							if (member.IsRequired)
							{
								return $"/* list_required */ {GetLocalSerializerRef(elemType)}.{nameof(JsonSerializerExtensions.JsonDeserializeListRequired)}(kv.Value, resolver: resolver, fieldName: {sb.Constant(member.OriginalName)})";
							}
							else if (member.HasDefaultValue)
							{
								return $"{GetLocalSerializerRef(elemType)}.{nameof(JsonSerializerExtensions.JsonDeserializeList)}(kv.Value, defaultValue: {defaultValue}, resolver: resolver, fieldName: {sb.Constant(member.OriginalName)})!";
							}
							else
							{
								return $"{GetLocalSerializerRef(elemType)}.{nameof(JsonSerializerExtensions.JsonDeserializeList)}(kv.Value, resolver: resolver, fieldName: {sb.Constant(member.OriginalName)})";
							}
						}
					}
					else
					{
						if (IsArray(memberType, out _))
						{
							if (member.IsNullableRefType)
							{
								return $"/* array_nullable */ kv.Value.{nameof(JsonValueExtensions.AsArrayOrDefault)}()?.{nameof(JsonArray.ToArray)}<{sb.TypeName(elemType)}>({elemDefaultValue}, resolver)!";
							}
							else if (member.IsRequired)
							{
								return $"/* array_required */ kv.Value.RequiredField({sb.Constant(member.OriginalName)}).{nameof(JsonValueExtensions.AsArray)}().{nameof(JsonArray.ToArray)}<{sb.TypeName(elemType)}>({elemDefaultValue}, resolver)";
							}
							else
							{
								return $"/* array_notnull */ kv.Value.{nameof(JsonValueExtensions.AsArrayOrEmpty)}().{nameof(JsonArray.ToArray)}<{sb.TypeName(elemType)}>({elemDefaultValue}, resolver)!";
							}
						}
						if (IsList(memberType, out _))
						{
							if (member.IsNullableRefType)
							{
								return $"/* list_nullable */ kv.Value.{nameof(JsonValueExtensions.AsArrayOrDefault)}?.{nameof(JsonArray.ToList)}<{sb.TypeName(elemType)}>({elemDefaultValue}, resolver)";
							}
							else if (member.IsRequired)
							{
								return $"/* list_required */ kv.Value.RequiredField({sb.Constant(member.OriginalName)}).{nameof(JsonValueExtensions.AsArray)}().{nameof(JsonArray.ToList)}<{sb.TypeName(elemType)}>({elemDefaultValue}, resolver)";
							}
							else
							{
								return $"/* list_notnull */ kv.Value.{nameof(JsonValueExtensions.AsArrayOrEmpty)}().{nameof(JsonArray.ToList)}<{sb.TypeName(elemType)}>({elemDefaultValue}, resolver)";
							}
						}
					}
				}
			}

			if (member.IsNullableValueType)
			{
				return $"/* vt_nullable */ kv.Value.As<{sb.TypeName(memberType)}>(resolver: resolver)";
			}

			return member.HasDefaultValue
				? $"/* fallback_has_default */ kv.Value.As<{sb.TypeName(memberType)}>(defaultValue: {defaultValue}, resolver: resolver)"
				: $"/* fallback_no_default */ kv.Value.As<{sb.TypeName(memberType)}>(resolver: resolver)!";
		}

	}

	internal sealed class CodeBuilder
	{

		public readonly StringBuilder Output = new();

		public readonly Stack<string?> Structure = [ ];

		public int Depth;

		public string ToStringAndClear()
		{
			if (this.Depth != 0) throw new InvalidOperationException("Code seems to be truncated");
			var s = this.Output.ToString();
			this.Output.Clear();
			return s;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return this.Output.ToString();
		}

		public void AppendLine(string text)
		{
			this.Output.Append('\t', this.Depth).AppendLine(text);
		}

		public void AppendLine(ref DefaultInterpolatedStringHandler text)
		{
			this.Output.Append('\t', this.Depth).AppendLine(string.Create(CultureInfo.InvariantCulture, ref text));
		}

		public void NewLine()
		{
			this.Output.AppendLine();
		}

		public string Constant(string literal) => "\"" + literal.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; //TODO: more!

		public string Constant(bool literal) => literal ? "true" : "false";

		public string Constant(char c) => c == 0 ? "'\0'" : char.IsLetter(c) || char.IsDigit(c) ? "'{c}'" : $"'\\u{(int) c:x04}'";

		public string Constant(int literal) => literal.ToString(CultureInfo.InvariantCulture);

		public string Constant(long literal) => literal.ToString(CultureInfo.InvariantCulture);

		public string Constant(float literal) => float.IsNaN(literal) ? "float.NaN" : literal.ToString("R", CultureInfo.InvariantCulture);

		public string Constant(double literal) => double.IsNaN(literal) ? "double.NaN" : literal.ToString("R", CultureInfo.InvariantCulture);

		public string Constant(Guid literal)
		{
			if (literal == Guid.Empty) return nameof(Guid) + "." + nameof(Guid.Empty);

			var (hi, lo) = (Uuid128) literal;
			return $"new Uuid128(0x{hi.ToUInt64():x}UL, 0x{lo.ToUInt64():x}UL)).ToGuid()";
		}

		public string Constant(Uuid128 literal)
		{
			if (literal == Uuid128.Empty) return nameof(Uuid128) + "." + nameof(Uuid128.Empty);
			if (literal == Uuid128.AllBitsSet) return nameof(Uuid128) + "." + nameof(Uuid128.AllBitsSet);

			var (hi, lo) = literal;
			return $"new Uuid128(0x{hi.ToUInt64():x}UL, 0x{lo.ToUInt64():x}UL))";
		}

		public string Constant(DateTime literal)
		{
			if (literal == DateTime.MinValue) return "DateTime.MinValue";
			if (literal == DateTime.MaxValue) return "DateTime.MaxValue";

			switch (literal.Kind)
			{
				case DateTimeKind.Utc: return $"new DateTime({literal.Ticks}L, DateTimeKind.Utc)";
				case DateTimeKind.Local: return $"new DateTime({literal.Ticks}L, DateTimeKind.Local)";
				default: return $"new DateTime({literal.Ticks}L)";
			}
		}

		public string Constant(DateTimeOffset literal)
		{
			if (literal == DateTimeOffset.MinValue) return "DateTimeOffset.MinValue";
			if (literal == DateTimeOffset.MaxValue) return "DateTimeOffset.MaxValue";

			if (literal.Offset == TimeSpan.Zero)
			{
				return $"new DateTimeOffset({literal.Ticks}L, TimeSpan.Zero)";
			}
			return $"new DateTimeOffset({literal.Ticks}L, new TimeSpan({literal.Offset.Ticks}L))";
		}

		public string Constant(Type type, object? boxed)
		{
			return boxed switch
			{
				null => type.IsValueType ? "default" : "null",
				string s => Constant(s),
				bool b => Constant(b),
				int i => Constant(i),
				long l => Constant(l),
				float f => Constant(f),
				double d => Constant(d),
				char c => Constant(c),
				Guid g => Constant(g),
				Uuid128 u => Constant(u),
				DateTime dt => Constant(dt),
				DateTimeOffset dto => Constant(dto),
				_ => boxed.Equals(type.GetDefaultValue()) ? $"default({TypeName(type)})" : throw new NotSupportedException($"Does not know how to transform value '{boxed}' of type {type.GetFriendlyName()} into a C# constant")
			};
		}

		public string Constant<TEnum>(TEnum literal) where TEnum : Enum
		{
			return TypeName<TEnum>() + "." + literal.ToString("G");
		}

		public string DefaultOf(Type type)
		{
			if (type == typeof(bool)) return "false";
			if (type == typeof(int) || type == typeof(long) || type == typeof(uint) || type == typeof(ulong) || type == typeof(float) || type == typeof(double))
			{
				return "0";
			}
			if (type == typeof(Guid)) return "Guid.Empty";
			if (type == typeof(DateTime)) return "DateTime.MinValue";
			if (type == typeof(DateTimeOffset)) return "DateTimeOffset.MinValue";
			if (!type.IsValueType || type.IsNullableType())
			{
				return "null";
			}
			return $"default";
		}

		public string DefaultOf<T>() => $"default({TypeName<T>()})";

		private static readonly string DefaultJsonNamespace = typeof(JsonValue).Namespace!;

		public bool CanUseShortName(Type t)
		{
			if (t.Namespace == DefaultJsonNamespace) return true;
			return false;
		}

		public string TypeName(Type t) => CanUseShortName(t)
			? TypeHelper.GetCompilableTypeName(t, omitNamespace: true, global: false)
			: TypeHelper.GetCompilableTypeName(t, omitNamespace: false, global: true);

		public string TypeName<T>() => TypeName(typeof(T));

		public string TypeNameGeneric(Type genericType, params string[] arguments)
		{
			var name = CanUseShortName(genericType) ? genericType.Name : ("global::" + genericType.FullName!);
			var suffix = "`" + arguments.Length;
			if (!name.EndsWith(suffix)) throw new InvalidOleVariantTypeException("genericType type argument count mismatch");
			name = name[..(^suffix.Length)];
			return $"{name}<{string.Join(", ", arguments)}>";
		}

		public string Parameter(string type, string name, bool nullable = false) => !nullable ? $"{type} {name}" : $"{type}? {name} = default";

		public string Parameter<T>(string name, bool nullable = false) => Parameter(TypeName<T>(), name, nullable);

		public string MethodName(Type parent, string name) => TypeName(parent) + "." + name;

		public string MethodName<T>(string name) => TypeName<T>() + "." + name;

		public string Singleton(Type type, string name) => TypeName(type) + "." + name;

		public string Singleton<T>(string name) => TypeName<T>() + "." + name;

		public void EnterBlock(string? type = null, string? comment = null)
		{
			this.Output.Append('\t', this.Depth).AppendLine(comment == null ? "{" : "{ // " + comment);
			this.Structure.Push(type);
			++this.Depth;
		}

		public void LeaveBlock(string? type = null, bool semicolon = false)
		{
			if (!this.Structure.TryPeek(out var expected) || expected != type)
			{
				throw new InvalidOperationException($"Code structure mismatch: cannot leave '{type}' while inside a '{expected}'");
			}

			--this.Depth;
			this.Structure.Pop();
			this.Output.Append('\t', this.Depth).AppendLine(semicolon ? "};" : "}");
		}

		public void Block(Action statement)
		{
			EnterBlock("block");
			statement();
			LeaveBlock("block");
		}

		public void Call(string method, params string[] args)
		{
			AppendLine($"{method}({string.Join(", ", args)});");
		}

		public void Return()
		{
			AppendLine("return;");
		}

		public void Return(string expr)
		{
			AppendLine($"return {expr};");
		}

		public void WriteNamespace(string name)
		{
			AppendLine($"namespace {name}");
		}

		public void Namespace(string name, Action block)
		{
			WriteNamespace(name);
			EnterBlock("namespace:" + name);
			block();
			LeaveBlock("namespace:" + name);
		}

		public void WriteUsing(string name, string? alias = null)
		{
			if (alias == null)
			{
				AppendLine($"using {name};");
			}
			else
			{
				AppendLine($"using {alias} = {name};");
			}
		}

		public void Class(string modifiers, string name, string[] implements, string[] where, Action block)
		{
			if (implements.Length > 0)
			{
				AppendLine($"{modifiers} class {name} : {string.Join(", ", implements!)}");
			}
			else
			{
				AppendLine($"{modifiers} class {name}");
			}

			foreach (var w in where)
			{
				AppendLine($"\twhere {w}");
			}

			EnterBlock("class:" + name);
			NewLine();

			block();

			LeaveBlock("class:" + name);
			NewLine();
		}

		public void Record(string modifiers, string name, string[] implements, string[] where, Action block)
		{
			if (implements.Length > 0)
			{
				AppendLine($"{modifiers} record {name} : {string.Join(", ", implements!)}");
			}
			else
			{
				AppendLine($"{modifiers} record {name}");
			}

			foreach (var w in where)
			{
				AppendLine($"\twhere {w}");
			}

			EnterBlock("record:" + name);
			NewLine();

			block();

			LeaveBlock("record:" + name);
			NewLine();
		}

		public void Struct(string modifiers, string name, string[] implements, string[] where, Action block)
		{
			if (implements.Length > 0)
			{
				AppendLine($"{modifiers} struct {name} : {string.Join(", ", implements!)}");
			}
			else
			{
				AppendLine($"{modifiers} struct {name}");
			}

			foreach (var w in where)
			{
				AppendLine($"\twhere {w}");
			}

			EnterBlock("struct:" + name);
			NewLine();

			block();

			LeaveBlock("struct:" + name);
			NewLine();
		}

		public void Method(string modifiers, string returnType, string name, string[] parameters, Action block)
		{
			AppendLine($"{modifiers} {returnType} {name}({string.Join(", ", parameters)})");
			EnterBlock("method:" + name);
			block();
			LeaveBlock("method:" + name);
			NewLine();
		}

		public void Comment(string comment)
		{
			if (comment.Contains("\r\n"))
			{
				foreach (var line in comment.Split("\r\n"))
				{
					this.Output.Append('\t', this.Depth).Append("// ").AppendLine(line.TrimEnd());
				}
			}
			else
			{
				this.Output.Append('\t', this.Depth).Append("// ").AppendLine(comment.TrimEnd());
			}
		}

		public void If(string conditionText, Action thenBlock, Action? elseBock = null)
		{
			AppendLine($"if ({conditionText})");

			EnterBlock("then:" + conditionText);
			thenBlock();
			LeaveBlock("then:" + conditionText);

			if (elseBock != null)
			{
				EnterBlock("else:" + conditionText);
				elseBock();
				LeaveBlock("else:" + conditionText);
			}
		}

		public void Ternary(string conditionText, string ifTrue, string ifFalse)
		{
			this.Output.Append($"{conditionText} ? {ifTrue} : {ifFalse}");
		}

		public void Attribute(string name, string[]? args = null, (string Name, string Value)[]? extras = null)
		{
			args ??= [ ];
			extras ??= [ ];

			if (extras.Length == 0)
			{
				AppendLine($"[{name}({string.Join(", ", args)})]");
			}
			else
			{
				var sb = new StringBuilder();
				sb.Append('[').Append(name).Append('(');
				for (int i = 0; i < args.Length; i++)
				{
					if (i != 0) sb.Append(", ");
					sb.Append(args[i]);
				}
				foreach (var kv in extras)
				{
					sb.Append($", {kv.Name} = {kv.Value}");
				}
				sb.Append(")]");
				AppendLine(sb.ToString());
			}
		}

		public void Attribute<TAttribute>(string[]? args = null, (string Name, string Value)[]? extras = null)
			where TAttribute : Attribute
		{
			var name = TypeName<TAttribute>();
			if (name.EndsWith("Attribute")) name = name[..^"Attribute".Length];
			Attribute(name, args, extras);
		}

	}

}

#endif
