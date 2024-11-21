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

namespace Doxense.Serialization.Json.CodeGen
{
	using System.Collections.Generic;
	using System.Globalization;
	using System.Runtime.InteropServices;
	using System.Text;

	public sealed class CodeBuilder
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

		public void NewLine()
		{
			this.Output.AppendLine();
		}

		public void WriteLines(string text)
		{
			// we need to parse the text and insert the proper indentation
			//TODO: optimized version later (the roslyn implementation uses readonly spans to split each line
			
			if (text.Contains("\r\n"))
			{
				foreach (var line in text.Split(CodeBuilder.LineBreakChars, StringSplitOptions.RemoveEmptyEntries))
				{
					this.Output.Append('\t', this.Depth).AppendLine(line.TrimEnd());
				}
			}
			else
			{
				this.Output.Append('\t', this.Depth).AppendLine(text);
			}
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

			throw new NotImplementedException();
			// var (hi, lo) = (Uuid128) literal;
			// return $"new Uuid128(0x{hi.ToUInt64():x}UL, 0x{lo.ToUInt64():x}UL)).ToGuid()";
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
				string s => this.Constant(s),
				bool b => this.Constant(b),
				int i => this.Constant(i),
				long l => this.Constant(l),
				float f => this.Constant(f),
				double d => this.Constant(d),
				char c => this.Constant(c),
				Guid g => this.Constant(g),
				DateTime dt => this.Constant(dt),
				DateTimeOffset dto => this.Constant(dto),
				_ => throw new NotSupportedException($"Does not know how to transform value '{boxed}' of type {type.Name} into a C# constant")
			};
		}

		public string Constant<TEnum>(TEnum literal) where TEnum : Enum
		{
			return this.TypeName<TEnum>() + "." + literal.ToString("G");
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

		public string DefaultOf<T>() => $"default({this.TypeName<T>()})";

		private static readonly string DefaultJsonNamespace = "Doxense.Serialization.Json"; //TODO: how can we find the correct namespace?

		public bool CanUseShortName(Type t)
		{
			if (t.Namespace == CodeBuilder.DefaultJsonNamespace) return true;
			return false;
		}

		public string TypeName(Type t) => this.CanUseShortName(t)
			                                  ? TypeHelper.GetCompilableTypeName(t, omitNamespace: true, global: false)
			                                  : TypeHelper.GetCompilableTypeName(t, omitNamespace: false, global: true);

		public string TypeName<T>() => this.TypeName(typeof(T));

		public string TypeNameGeneric(Type genericType, params string[] arguments)
		{
			var name = this.CanUseShortName(genericType) ? genericType.Name : ("global::" + genericType.FullName!);
			var suffix = "`" + arguments.Length;
			if (!name.EndsWith(suffix)) throw new InvalidOleVariantTypeException("genericType type argument count mismatch");
			name = name.Substring(0, name.Length - suffix.Length);
			return $"{name}<{string.Join(", ", arguments)}>";
		}

		public string Parameter(string type, string name, bool nullable = false) => !nullable ? $"{type} {name}" : $"{type}? {name} = default";

		public string Parameter<T>(string name, bool nullable = false) => this.Parameter(this.TypeName<T>(), name, nullable);

		public string MethodName(Type parent, string name) => this.TypeName(parent) + "." + name;

		public string MethodName<T>(string name) => this.TypeName<T>() + "." + name;

		public string Singleton(Type type, string name) => this.TypeName(type) + "." + name;

		public string Singleton<T>(string name) => this.TypeName<T>() + "." + name;

		public void EnterBlock(string? type = null, string? comment = null)
		{
			this.Output.Append('\t', this.Depth).AppendLine(comment == null ? "{" : "{ // " + comment);
			this.Structure.Push(type);
			++this.Depth;
		}

		public void LeaveBlock(string? type = null, bool semicolon = false)
		{
			var expected = this.Structure.Count > 0 ? this.Structure.Peek() : null;
			if (expected != null && expected != type)
			{
				throw new InvalidOperationException($"Code structure mismatch: cannot leave '{type}' while inside a '{expected}'");
			}

			--this.Depth;
			this.Structure.Pop();
			this.Output.Append('\t', this.Depth).AppendLine(semicolon ? "};" : "}");
		}

		public void Block(Action statement)
		{
			this.EnterBlock("block");
			statement();
			this.LeaveBlock("block");
		}

		public void Call(string method, params string[] args)
		{
			this.AppendLine($"{method}({string.Join(", ", args)});");
		}

		public void Return()
		{
			this.AppendLine("return;");
		}

		public void Return(string expr)
		{
			this.AppendLine($"return {expr};");
		}

		public void WriteNamespace(string name)
		{
			this.AppendLine($"namespace {name}");
		}

		public void Namespace(string name, Action block)
		{
			this.WriteNamespace(name);
			this.EnterBlock("namespace:" + name);
			block();
			this.LeaveBlock("namespace:" + name);
		}

		public void WriteUsing(string name, string? alias = null)
		{
			if (alias == null)
			{
				this.AppendLine($"using {name};");
			}
			else
			{
				this.AppendLine($"using {alias} = {name};");
			}
		}

		public void Class(string modifiers, string name, string[] implements, string[] where, Action block)
		{
			if (implements.Length > 0)
			{
				this.AppendLine($"{modifiers} class {name} : {string.Join(", ", implements!)}");
			}
			else
			{
				this.AppendLine($"{modifiers} class {name}");
			}

			foreach (var w in where)
			{
				this.AppendLine($"\twhere {w}");
			}

			this.EnterBlock("class:" + name);
			this.NewLine();

			block();

			this.LeaveBlock("class:" + name);
			this.NewLine();
		}

		public void Record(string modifiers, string name, string[] implements, string[] where, Action block)
		{
			if (implements.Length > 0)
			{
				this.AppendLine($"{modifiers} record {name} : {string.Join(", ", implements!)}");
			}
			else
			{
				this.AppendLine($"{modifiers} record {name}");
			}

			foreach (var w in where)
			{
				this.AppendLine($"\twhere {w}");
			}

			this.EnterBlock("record:" + name);
			this.NewLine();

			block();

			this.LeaveBlock("record:" + name);
			this.NewLine();
		}

		public void Struct(string modifiers, string name, string[] implements, string[] where, Action block)
		{
			if (implements.Length > 0)
			{
				this.AppendLine($"{modifiers} struct {name} : {string.Join(", ", implements!)}");
			}
			else
			{
				this.AppendLine($"{modifiers} struct {name}");
			}

			foreach (var w in where)
			{
				this.AppendLine($"\twhere {w}");
			}

			this.EnterBlock("struct:" + name);
			this.NewLine();

			block();

			this.LeaveBlock("struct:" + name);
			this.NewLine();
		}

		public void Method(string modifiers, string returnType, string name, string[] parameters, Action block)
		{
			this.AppendLine($"{modifiers} {returnType} {name}({string.Join(", ", parameters)})");
			this.EnterBlock("method:" + name);
			block();
			this.LeaveBlock("method:" + name);
			this.NewLine();
		}

		private static readonly char[] LineBreakChars = "\r\n".ToCharArray();
		
		public void Comment(string comment)
		{
			if (comment.Contains("\r\n"))
			{
				foreach (var line in comment.Split(CodeBuilder.LineBreakChars, StringSplitOptions.RemoveEmptyEntries))
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
			this.AppendLine($"if ({conditionText})");

			this.EnterBlock("then:" + conditionText);
			thenBlock();
			this.LeaveBlock("then:" + conditionText);

			if (elseBock != null)
			{
				this.EnterBlock("else:" + conditionText);
				elseBock();
				this.LeaveBlock("else:" + conditionText);
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
				this.AppendLine($"[{name}({string.Join(", ", args)})]");
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
				this.AppendLine(sb.ToString());
			}
		}

		public void Attribute<TAttribute>(string[]? args = null, (string Name, string Value)[]? extras = null)
			where TAttribute : Attribute
		{
			var name = this.TypeName<TAttribute>();
			if (name.EndsWith("Attribute")) name = name.Substring(0, name.Length -"Attribute".Length);
			this.Attribute(name, args, extras);
		}

	}
}
