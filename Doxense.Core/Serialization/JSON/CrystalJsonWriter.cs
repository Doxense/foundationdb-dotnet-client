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

//#define DEBUG_JSON_SERIALIZER

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Doxense.IO;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Classe capable d'écrire du JSON dans un buffer</summary>
	[DebuggerDisplay("Json={!m_javascript}, Formatted={m_formatted}, Depth={m_objectGraphDepth}")]
	public sealed class CrystalJsonWriter
	{
		private const int MaximumObjectGraphDepth = 16;
		internal const string FormatDateTimeO = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.fffffffK";

		public enum NodeType
		{
			TopLevel = 0,
			Object,
			Array,
			Property
		}

		[DebuggerDisplay("Node={Node}, Tail={Tail}")]
		public struct State
		{
			/// <summary>False si c'est le premier "élément" (d'un objet ou d'une array)</summary>
			internal bool Tail;
			/// <summary>Type de node actuel</summary>
			internal NodeType Node;
			/// <summary>Valeur de l'indentation actuelle</summary>
			internal string Indentation;
		}

		// Settings
		private readonly TextWriter m_buffer;
		private readonly CrystalJsonSettings m_settings;
		private readonly ICrystalJsonTypeResolver m_resolver;
		private readonly bool m_javascript;
		private readonly bool m_formatted;
		private readonly bool m_indented;
		private readonly CrystalJsonSettings.DateFormat m_dateFormat;
		private readonly CrystalJsonSettings.FloatFormat m_floatFormat;
		private readonly bool m_discardDefaults;
		private readonly bool m_discardNulls;
		private readonly bool m_discardClass;
		private readonly bool m_markVisited;
		private readonly bool m_camelCase;
		private readonly bool m_enumAsString;
		private readonly bool m_enumCamelCased;
		// State
		private JsonPropertyAttribute? m_attributes;
		private State m_state;
		private object[]? m_visitedObjects;
		private int m_visitedCursor;
		private int m_objectGraphDepth;
		private char[]? m_tmpBuffer;

		public CrystalJsonWriter(TextWriter? buffer, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			m_buffer = buffer ?? new FastStringWriter(512);
			m_settings = settings ?? CrystalJsonSettings.Json;
			m_resolver = resolver ?? CrystalJson.DefaultResolver;
			//
			m_javascript = m_settings.TargetLanguage == CrystalJsonSettings.Target.JavaScript;
			m_formatted = m_settings.TextLayout != CrystalJsonSettings.Layout.Compact;
			m_indented = m_settings.TextLayout == CrystalJsonSettings.Layout.Indented;
			if (m_indented) m_state.Indentation = string.Empty;
			m_dateFormat = m_settings.DateFormatting != CrystalJsonSettings.DateFormat.Default ? m_settings.DateFormatting : (m_javascript ? CrystalJsonSettings.DateFormat.JavaScript : CrystalJsonSettings.DateFormat.TimeStampIso8601);
			m_floatFormat = m_settings.FloatFormatting != CrystalJsonSettings.FloatFormat.Default ? m_settings.FloatFormatting : (m_javascript ? CrystalJsonSettings.FloatFormat.JavaScript : CrystalJsonSettings.FloatFormat.Symbol);
			m_discardDefaults = m_settings.HideDefaultValues;
			m_discardNulls = m_discardDefaults || !m_settings.ShowNullMembers;
			m_discardClass = m_settings.HideClassId;
			m_camelCase = m_settings.UseCamelCasingForNames;
			m_enumAsString = m_settings.EnumsAsString;
			m_enumCamelCased = m_settings.UseCamelCasingForEnums;
			m_markVisited = !m_settings.DoNotTrackVisitedObjects;
		}

		public CrystalJsonWriter(StringBuilder? buffer, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
			: this(buffer != null ? new FastStringWriter(buffer) : null, settings, resolver)
		{ }

		public TextWriter Buffer => m_buffer;

		public CrystalJsonSettings Settings => m_settings;

		public ICrystalJsonTypeResolver Resolver => m_resolver;

		/// <summary>Indique si on cible du JavaScript et non pas du JSON</summary>
		/// <remarks>Si true, les strings sont encodées avec des quotes ('), et les noms de propriétés ne sont quoted que si nécessaire</remarks>
		public bool JavaScript => m_javascript;

		/// <summary>Indique s'il faut ignorer les membres nulls/vides</summary>
		public bool DiscardDefaults => m_discardDefaults;

		/// <summary>Indique s'il faut ignorer les membres nulls</summary>
		public bool DiscardNulls => m_discardNulls;

		/// <summary>Indique s'il faut ignorer l'attribut "_class"</summary>
		public bool DiscardClass => m_discardClass;

		/// <summary>Format actuel de conversion de date (en fonction du mode si Default)</summary>
		public CrystalJsonSettings.DateFormat DateFormatting => m_dateFormat;

		/// <summary>Format actuel de conversion de nombres à virgule</summary>
		public CrystalJsonSettings.FloatFormat FloatFormatting => m_floatFormat;

		/// <summary>Profondeur actuelle de sérialisation</summary>
		public int Depth => m_objectGraphDepth;

		/// <summary>Indique si le writer indente automatiquement les valeurs</summary>
		public bool Indented => m_indented;

		/// <summary>Indique si le writer insert des espaces entre les tokens</summary>
		public bool Formatted => m_formatted;

		/// <summary>Retourne un petit buffer utilisable pour le formatage de données (ex: floats, dates, ...)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private char[] GetTempBuffer()
		{
			return m_tmpBuffer ??= new char[64];
		}

		/// <summary>Retourne le nom formaté d'un champ</summary>
		/// <param name="name">Nom d'un champ (ex: "FooBar")</param>
		/// <returns>Nom éventuellement formaté ("fooBar" en Camel Casing)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal string FormatName(string name)
		{
			return m_camelCase ? CamelCase(name) : name;
		}

		internal static string CamelCase(string name)
		{
			// check si le premier n'est pas déjà en minuscules
			char first = name[0];
			if (first == '_' || (first >= 'a' && first <= 'z')) return name;
			// convertir le premier caractère en minuscules
			var chars = name.ToCharArray();
			chars[0] = char.ToLowerInvariant(first);
			return new string(chars);
		}

		/// <summary>Ecrit l'attribut "_class" avec l'id résolvé du type</summary>
		/// <param name="type">Type à résolver</param>
		public void WriteClassId(Type type)
		{
			var typeDef = this.Resolver.ResolveJsonType(type);
			if (typeDef == null) throw CrystalJson.Errors.Serialization_CouldNotResolveTypeDefinition(type);
			WriteField(JsonTokens.CustomClassAttribute, typeDef.ClassId);
		}

		/// <summary>Ecrit l'attribut "_class" avec un id de class spécifique</summary>
		/// <param name="classId">Identifiant de la class</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteClassId(string classId)
		{
			WriteField(JsonTokens.CustomClassAttribute, classId);
		}

		public void WriteComment(string comment)
		{
			m_buffer.Write("/* ");
			m_buffer.Write(comment.Replace("*/", "* /"));
			m_buffer.Write(" */");
		}

		/// <summary>Ecrit "null"</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteNull()
		{
			m_buffer.Write(JsonTokens.Null);
		}

		/// <summary>Ecrit "{}", en respectant le formatage</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteEmptyObject()
		{
			m_buffer.Write(m_formatted ? JsonTokens.EmptyObjectFormatted : JsonTokens.EmptyObjectCompact);
		}

		/// <summary>Ecrit "[]", en respectant le formatage</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteEmptyArray()
		{
			m_buffer.Write(m_formatted ? JsonTokens.EmptyArrayFormatted : JsonTokens.EmptyArrayCompact);
		}

		/// <summary>Ecrit le "," qui sépare deux fields (sauf si c'est le premier item d'un objet ou d'une array), en respectant le formatage</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteFieldSeparator()
		{
			if (m_state.Tail)
			{
				WriteTailSeparator();
			}
			else
			{
				WriteHeadSeparator();
			}
		}

		public void WriteHeadSeparator()
		{
			Contract.Debug.Requires(!m_state.Tail);
			m_state.Tail = true;
			var buffer = m_buffer;
			if (m_indented)
			{
				buffer.Write(JsonTokens.NewLine);
				buffer.Write(m_state.Indentation);
			}
			else if (m_formatted)
			{
				buffer.Write(' ');
			}
		}

		public void WriteTailSeparator()
		{
			Contract.Debug.Requires(m_state.Tail);
			var buffer = m_buffer;
			if (m_indented)
			{
				buffer.Write(JsonTokens.CommaIndented);
				buffer.Write(m_state.Indentation);
			}
			else if (m_formatted)
			{
				buffer.Write(JsonTokens.CommaFormatted);
			}
			else
			{
				buffer.Write(',');
			}
		}

		/// <summary>Ecrit le "," qui sépare deux fields (sauf si c'est le premier item d'un objet ou d'une array), en respectant le formatage</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteInlineFieldSeparator()
		{
			if (m_state.Tail)
			{
				WriteInlineTailSeparator();
			}
			else
			{
				WriteInlineHeadSeparator();
			}
		}

		public void WriteInlineHeadSeparator()
		{
			Contract.Debug.Requires(!m_state.Tail);
			m_state.Tail = true;
			var buffer = m_buffer;
			if (m_indented | m_formatted)
			{
				buffer.Write(' ');
			}
		}

		public void WriteInlineTailSeparator()
		{
			Contract.Debug.Requires(m_state.Tail);
			var buffer = m_buffer;
			if (m_indented | m_formatted)
			{
				buffer.Write(JsonTokens.CommaFormatted);
			}
			else
			{
				buffer.Write(',');
			}
		}

		public JsonPropertyAttribute? PushAttributes(JsonPropertyAttribute attributes)
		{
			var tmp = m_attributes;
			m_attributes = attributes;
			return tmp;
		}

		public void PopAttributes(JsonPropertyAttribute? attributes)
		{
			m_attributes = attributes;
		}

		/// <summary>Démarre un nouvel état courant, et retourne le précédent</summary>
		/// <param name="type">Type du nouvel état</param>
		/// <returns>Etat précédent</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal State PushState(NodeType type)
		{
			var state = m_state;
			m_state.Tail = false;
			m_state.Node = type;
			return state;
		}

		/// <summary>Restaure un état précédent, et retourne l'état courant</summary>
		/// <param name="state">Copie d'un précédent état</param>
		/// <returns>Etat actuel</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal State PopState(State state)
		{
			var tmp = m_state;
			m_state = state;
			return tmp;
		}

		/// <summary>Réinitialise l'état du writer, comme s'il était au début d'un nouveau document JSON</summary>
		/// <remarks>
		/// A utiliser si on réutilise plusieurs fois un même writer.
		/// L'appelant doit faire attention a reset également l'état interne du TextWriter utilisé par ce writer!
		/// </remarks>
		public void ResetState()
		{
			//note: on doit garder le mode d'indentation!
			m_state.Node = NodeType.TopLevel;
			m_state.Tail = false;
		}

		/// <summary>Retourne une copie de l'état courant du writer</summary>
		internal State CurrentState
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_state;
		}

		/// <summary>Marque le début d'un nouvel item dans une array, ou d'un field dans un objet</summary>
		/// <returns>False si c'est le premier élément du context courant, ou True s'il y a déjà des éléments.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal bool MarkNext()
		{
			bool tail = m_state.Tail;
			m_state.Tail = true;
			return tail;
		}

		public State BeginObject()
		{
			var state = m_state;
			m_buffer.Write('{');
			m_state.Tail = false;
			m_state.Node = NodeType.Object;
			if (m_indented) m_state.Indentation += '\t';
			return state;
		}

		/// <summary>Ecrit le "}" pour terminer un objet, en respectant le formatage</summary>
		public void EndObject(State state)
		{
			Paranoid.Requires(m_state.Node == NodeType.Object);
			var buffer = m_buffer;
			if (m_indented)
			{
				buffer.Write(JsonTokens.NewLine);
				buffer.Write(state.Indentation);
				buffer.Write('}');
			}
			else if (m_formatted)
			{
				buffer.Write(JsonTokens.CurlyCloseFormatted);
			}
			else
			{
				buffer.Write('}');
			}
			m_state = state;
		}

		/// <summary>Ecrit le "[" pour démarrer un tableau, en respectant le formatage</summary>
		/// <returns>Etat actuel (à retourner lors de l'appel de EndArray)</returns>
		public State BeginArray()
		{
			var state = m_state;
			m_buffer.Write('[');
			m_state.Tail = false;
			m_state.Node = NodeType.Array;
			if (m_indented) m_state.Indentation += '\t';
			return state;
		}

		/// <summary>Ecrit le "[" pour démarrer un tableau, en respectant le formatage</summary>
		/// <returns>Etat actuel (à retourner lors de l'appel de EndArray)</returns>
		public State BeginInlineArray()
		{
			var state = m_state;
			m_buffer.Write('[');
			m_state.Tail = false;
			m_state.Node = NodeType.Array;
			return state;
		}

		/// <summary>Ecrit le "]" pour terminer un tableau, en respectant le formatage</summary>
		/// <param name="state">Valeur qui a été retournée par l'appel à BeginArray</param>
		public void EndArray(State state)
		{
			Paranoid.Requires(m_state.Node == NodeType.Array);
			var buffer = m_buffer;
			if (m_indented)
			{
				buffer.Write(JsonTokens.NewLine);
				buffer.Write(state.Indentation);
				buffer.Write(']');
			}
			else if (m_formatted)
			{
				buffer.Write(JsonTokens.BracketCloseFormatted); // " ]"
			}
			else
			{
				buffer.Write(']');
			}
			m_state = state;
		}

		/// <summary>Ecrit le "]" pour terminer un tableau, en respectant le formatage</summary>
		/// <param name="state">Valeur qui a été retournée par l'appel à BeginArray</param>
		public void EndInlineArray(State state)
		{
			Paranoid.Requires(m_state.Node == NodeType.Array);
			var buffer = m_buffer;
			if (m_indented | m_formatted)
			{
				buffer.Write(JsonTokens.BracketCloseFormatted); // " ]"
			}
			else
			{
				buffer.Write(']');
			}
			m_state = state;
		}

		#region WritePair ...

		public void WritePair(int key, bool value)
		{
			var state = BeginArray();
			WriteHeadSeparator();
			WriteValue(key);
			WriteTailSeparator();
			WriteValue(value);
			EndArray(state);
		}

		public void WritePair(int key, int value)
		{
			var state = BeginArray();
			WriteHeadSeparator();
			WriteValue(key);
			WriteTailSeparator();
			WriteValue(value);
			EndArray(state);
		}

		public void WritePair(int key, string? value)
		{
			var state = BeginArray();
			WriteHeadSeparator();
			WriteValue(key);
			WriteTailSeparator();
			WriteValue(value);
			EndArray(state);
		}

		public void WritePair(int key, JsonValue? value)
		{
			var state = BeginArray();
			WriteHeadSeparator();
			WriteValue(key);
			WriteTailSeparator();
			(value ?? JsonNull.Missing).JsonSerialize(this);
			EndArray(state);
		}

		public void WritePair<TValue>(int key, TValue value)
		{
			var state = BeginArray();
			WriteHeadSeparator();
			WriteValue(key);
			WriteTailSeparator();
			VisitValue<TValue>(value);
			EndArray(state);
		}

		public void WritePair(string? key, bool value)
		{
			var state = BeginArray();
			WriteHeadSeparator();
			WriteValue(key);
			WriteTailSeparator();
			WriteValue(value);
			EndArray(state);
		}

		public void WritePair(string? key, int value)
		{
			var state = BeginArray();
			WriteHeadSeparator();
			WriteValue(key);
			WriteTailSeparator();
			WriteValue(value);
			EndArray(state);
		}

		public void WritePair(string? key, string? value)
		{
			var state = BeginArray();
			WriteHeadSeparator();
			WriteValue(key);
			WriteTailSeparator();
			WriteValue(value);
			EndArray(state);
		}

		public void WritePair(string? key, JsonValue? value)
		{
			var state = BeginArray();
			WriteHeadSeparator();
			WriteValue(key);
			WriteTailSeparator();
			(value ?? JsonNull.Missing).JsonSerialize(this);
			EndArray(state);
		}

		public void WritePair<TValue>(string? key, TValue value)
		{
			var state = BeginArray();
			WriteHeadSeparator();
			WriteValue(key);
			WriteTailSeparator();
			VisitValue<TValue>(value);
			EndArray(state);
		}

		#endregion

		#region WriteInlinePair...

		public void WriteInlinePair(int key, bool value)
		{
			var state = BeginInlineArray();
			WriteInlineHeadSeparator();
			WriteValue(key);
			WriteInlineTailSeparator();
			WriteValue(value);
			EndInlineArray(state);
		}

		public void WriteInlinePair(int key, int value)
		{
			var state = BeginInlineArray();
			WriteInlineHeadSeparator();
			WriteValue(key);
			WriteInlineTailSeparator();
			WriteValue(value);
			EndInlineArray(state);
		}

		public void WriteInlinePair(int key, string? value)
		{
			var state = BeginInlineArray();
			WriteInlineHeadSeparator();
			WriteValue(key);
			WriteInlineTailSeparator();
			WriteValue(value);
			EndInlineArray(state);
		}

		public void WriteInlinePair(int key, JsonValue value)
		{
			var state = BeginInlineArray();
			WriteInlineHeadSeparator();
			WriteValue(key);
			WriteInlineTailSeparator();
			(value ?? JsonNull.Missing).JsonSerialize(this);
			EndInlineArray(state);
		}

		public void WriteInlinePair<TValue>(int key, TValue value)
		{
			var state = BeginInlineArray();
			WriteInlineHeadSeparator();
			WriteValue(key);
			WriteInlineTailSeparator();
			VisitValue<TValue>(value);
			EndInlineArray(state);
		}

		public void WriteInlinePair(string? key, bool value)
		{
			var state = BeginInlineArray();
			WriteInlineHeadSeparator();
			WriteValue(key);
			WriteInlineTailSeparator();
			WriteValue(value);
			EndInlineArray(state);
		}

		public void WriteInlinePair(string? key, int value)
		{
			var state = BeginInlineArray();
			WriteInlineHeadSeparator();
			WriteValue(key);
			WriteInlineTailSeparator();
			WriteValue(value);
			EndInlineArray(state);
		}

		public void WriteInlinePair(string? key, string? value)
		{
			var state = BeginInlineArray();
			WriteInlineHeadSeparator();
			WriteValue(key);
			WriteInlineTailSeparator();
			WriteValue(value);
			EndInlineArray(state);
		}

		public void WriteInlinePair(string? key, JsonValue value)
		{
			var state = BeginInlineArray();
			WriteInlineHeadSeparator();
			WriteValue(key);
			WriteInlineTailSeparator();
			(value ?? JsonNull.Missing).JsonSerialize(this);
			EndInlineArray(state);
		}

		public void WriteInlinePair<TValue>(string? key, TValue value)
		{
			var state = BeginInlineArray();
			WriteInlineHeadSeparator();
			WriteValue(key);
			WriteInlineTailSeparator();
			VisitValue<TValue>(value);
			EndInlineArray(state);
		}

		#endregion

		/// <summary>Marque l'objet comme étant déjà traité</summary>
		/// <param name="value">Objet en cours de traitement</param>
		/// <exception cref="System.InvalidOperationException">Si cet objet a déjà été marqué</exception>
		public void MarkVisited(object? value)
		{
			if (m_objectGraphDepth >= MaximumObjectGraphDepth)
			{ // protection contre les object graph gigantesques
				throw CrystalJson.Errors.Serialization_FailTooDeep(m_objectGraphDepth, value);
			}
			if (value != null && m_markVisited)
			{ // protection contre les chaînes récursives d'objet (=> stack overflow)
				if (m_visitedObjects == null)
				{
					m_visitedObjects = new object[4];
					m_visitedCursor = 0;
				}
				else if (AlreadyVisited(m_visitedObjects.AsSpan(0, m_visitedCursor), m_visitedCursor))
				{
					if (!TypeSafeForRecursion(value.GetType()))
					{
						throw CrystalJson.Errors.Serialization_ObjectRecursionIsNotAllowed(m_visitedObjects, value, m_objectGraphDepth);
					}
				}
				PushVisited(ref m_visitedObjects, ref m_visitedCursor, value);
			}
			++m_objectGraphDepth;

			static bool AlreadyVisited(ReadOnlySpan<object> stack, object value)
			{
				foreach (var item in stack)
				{
					if (ReferenceEquals(item, value))
					{
						return true;
					}
				}
				return false;
			}

			static void PushVisited(ref object[] buffer, ref int cursor, object value)
			{
				if (cursor >= buffer.Length)
				{
					Array.Resize(ref buffer, checked(buffer.Length + 4));
				}
				buffer[cursor++] = value;
			}
		}

		internal static bool TypeSafeForRecursion(Type type)
		{
			// liste de reference types qui peuvent être répétés plusieurs fois, et qui ne peuvent pas provoquer de stackoverflow
			return type.IsValueType || type == typeof(string) || type == typeof(System.Net.IPAddress);
		}

		public void Leave(object? value)
		{
			if (m_objectGraphDepth == 0) throw CrystalJson.Errors.Serialization_InternalDepthInconsistent();
			if (value != null && m_markVisited && m_visitedObjects != null && m_visitedCursor > 0)
			{
				var previous = PopVisited(ref m_visitedObjects, ref m_visitedCursor);
				if (!object.ReferenceEquals(previous, value)) throw CrystalJson.Errors.Serialization_LeaveNotSameThanMark(m_objectGraphDepth, value);
			}
			--m_objectGraphDepth;

			static object PopVisited(ref object[] buffer, ref int cursor)
			{
				Contract.Debug.Requires(buffer != null && cursor > 0 && cursor <= buffer.Length);
				--cursor;
				var obj = buffer[cursor];
				buffer[cursor] = default!;
				return obj;
			}
		}

		#region Basic Type Serializers...

		/// <summary>[DANGEROUS] Ecrit un bloc de JSON brut dans le buffer de sortie</summary>
		/// <param name="rawJson">Snippet de JSON brut à écrire tel quel (sans encodage)</param>
		/// <remarks>"Danger, Will Robinson !!!"
		/// A n'utiliser que lorsque vous êtes certain de ce que vous faites, car vous pouvez facilement corrompre le JSON généré !</remarks>
		public void WriteRaw(string? rawJson)
		{
			if (!string.IsNullOrEmpty(rawJson))
			{
				m_buffer.Write(rawJson);
			}
		}

		/// <summary>Ecrit un nom de propriété qui est GARANTIT comme ne nécessitant pas d'encodage!</summary>
		/// <param name="name">Nom de propriété QUI NE DOIT PAS NECESSITER D'ENCODAGE ! (nom d'une propriété d'un objet C# = OK, key d'un dictionnaire = NOT OK !)</param>
		public void WriteName(string name)
		{
			// ajoute le séparateur
			WriteFieldSeparator();
			WritePropertyName(name);
		}

		internal void WritePropertyName(string name)
		{
			if (!m_javascript)
			{
				var buffer = m_buffer;
				buffer.Write('"');
				buffer.Write(FormatName(name));
				buffer.Write(m_formatted ? JsonTokens.QuoteColonFormatted : JsonTokens.QuoteColonCompact);
			}
			else
			{
				WriteJavaScriptName(name);
			}
		}

		internal void WriteJavaScriptName(string name)
		{
			var buffer = m_buffer;
			buffer.Write(Doxense.Web.JavaScriptEncoding.EncodePropertyName(FormatName(name)));
			buffer.Write(m_formatted ? JsonTokens.ColonFormatted : JsonTokens.ColonCompact);
		}

		/// <summary>Ecrit un nom de propriété qui est GARANTIT comme ne nécessitant pas d'encodage!</summary>
		/// <param name="name">Nom de propriété QUI NE DOIT PAS NECESSITER D'ENCODAGE ! (nom d'une propriété d'un objet C# = OK, key d'un dictionnaire = NOT OK !)</param>
		public void WriteName(long name)
		{
			// ajoute le séparateur
			WriteFieldSeparator();
			WritePropertyName(name);
		}

		internal void WritePropertyName(long name)
		{
			if (!m_javascript)
			{
				m_buffer.Write('"');
				WriteValue(name);
				m_buffer.Write(m_formatted ? JsonTokens.QuoteColonFormatted : JsonTokens.QuoteColonCompact);
			}
			else
			{
				WriteJavaScriptName(name);
			}
		}

		internal void WriteJavaScriptName(long name)
		{
			WriteValue(name);
			m_buffer.Write(m_formatted ? JsonTokens.ColonFormatted : JsonTokens.ColonCompact);
		}

		/// <summary>Ecrit un nom de propriété numérique</summary>
		/// <param name="name">Nom de la propriété</param>
		public void WriteUnsafeName(int name)
		{
			// ajoute le séparateur
			WriteFieldSeparator();
			var buffer = m_buffer;
			if (!m_javascript)
			{
				buffer.Write('"');
				WriteValue(name);
				buffer.Write('"');
			}
			else
			{
				buffer.Write('\'');
				WriteValue(name);
				buffer.Write('\'');
			}
			buffer.Write(m_formatted ? JsonTokens.ColonFormatted : JsonTokens.ColonCompact);
		}

		public void WriteUnsafeName(string name)
		{
			// ajoute le séparateur
			WriteFieldSeparator();
			var buffer = m_buffer;
			if (!m_javascript)
			{
				CrystalJsonFormatter.WriteJsonString(buffer, name);
			}
			else
			{
				CrystalJsonFormatter.WriteJavaScriptString(buffer, FormatName(name));
			}
			buffer.Write(m_formatted ? JsonTokens.ColonFormatted : JsonTokens.ColonCompact);
		}

		#region WriteValue...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(JsonValue? value)
		{
			if (value != null)
				value.JsonSerialize(this);
			else
				WriteNull();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(string? value)
		{
			if (!m_javascript)
				CrystalJsonFormatter.WriteJsonString(m_buffer, value);
			else
				CrystalJsonFormatter.WriteJavaScriptString(m_buffer, value);
		}

		public void WriteValue(char value)
		{
			// on remplace le char NUL par 'null', ce qui est plus logique en javascript...
			var buffer = m_buffer;
			if (value == '\0')
			{
				buffer.Write(JsonTokens.Null);
			}
			else if (!JsonEncoding.NeedsEscaping(value))
			{
				buffer.Write('"');
				buffer.Write(value);
				buffer.Write('"');
			}
			else
			{
				//TODO: trouver un moyen plus optimisé ?
				buffer.Write(JsonEncoding.AppendSlow(new StringBuilder(), new string(value, 1), true).ToString());
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(char? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(StringBuilder? value)
		{
			WriteValue(value?.ToString());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(bool value)
		{
			m_buffer.Write(value ? JsonTokens.True : JsonTokens.False);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(bool? value)
		{
			m_buffer.Write(value == null ? JsonTokens.Null : value.Value ? JsonTokens.True : JsonTokens.False);
		}

		public void WriteValue(int value)
		{
			if ((uint) value < 10U)
			{ // single char
				m_buffer.Write((char)(48 + value));
			}
			else
			{
				CrystalJsonFormatter.WriteSignedIntegerUnsafe(m_buffer, value, GetTempBuffer());
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(int? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(uint value)
		{
			if (value < 10U)
			{ // single char
				m_buffer.Write((char)(48 + (int)value));
			}
			else
			{
				CrystalJsonFormatter.WriteUnsignedIntegerUnsafe(m_buffer, value, GetTempBuffer());
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(uint? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(long value)
		{
			if ((ulong) value < 10UL)
			{ // single char
				m_buffer.Write((char)(48 + (int) value));
			}
			else
			{
				CrystalJsonFormatter.WriteSignedIntegerUnsafe(m_buffer, value, GetTempBuffer());
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(long? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(ulong value)
		{
			if (value < 10UL)
			{ // single char
				m_buffer.Write((char)(48 + (int)value));
			}
			else
			{
				CrystalJsonFormatter.WriteUnsignedIntegerUnsafe(m_buffer, value, GetTempBuffer());
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(ulong? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(float value)
		{
			CrystalJsonFormatter.WriteSingleUnsafe(m_buffer, value, GetTempBuffer(), m_floatFormat);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(float? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(double value)
		{
			CrystalJsonFormatter.WriteDoubleUnsafe(m_buffer, value, GetTempBuffer(), m_floatFormat);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(double? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteEnumInteger<TEnum>(TEnum value)
			where TEnum : System.Enum
		{
			//note: on pourrait convertir l'enum en (int) et appeler WriteInt32(...) mais certaines enums ne dérivent pas de Int32 :(
			m_buffer.Write(value.ToString("D"));
		}

		public void WriteEnumInteger(Enum value)
		{
			//note: on pourrait convertir l'enum en (int) et appeler WriteInt32(...) mais certaines enums ne dérivent pas de Int32 :(
			m_buffer.Write(value.ToString("D"));
		}

		public void WriteEnumString<TEnum>(Enum value)
			where TEnum: System.Enum
		{
			string str = value.ToString("G");
			if (m_enumCamelCased) str = CamelCase(str);
			WriteValue(str);
		}

		public void WriteEnumString(Enum value)
		{
			string str = value.ToString("G");
			if (m_enumCamelCased) str = CamelCase(str);
			WriteValue(str);
		}

		internal void WriteEnum(Enum? value, EnumStringTable.Cache cache)
		{
			if (value == null)
			{
				WriteNull();
				return;
			}

			var fmt = m_attributes?.EnumFormat ?? JsonEnumFormat.Inherits;
			if ((fmt == JsonEnumFormat.Inherits && m_enumAsString) || fmt == JsonEnumFormat.String)
			{
				//TODO: on peut supposer que les enum.ToString() sont safe au niveau encodage ?
				WriteValue(m_enumCamelCased ? cache.GetNameCamelCased(value) : cache.GetName(value));
			}
			else
			{
				//note: on pourrait convertir l'enum en (int) et appeler WriteInt32(...) mais certaines enums ne dérivent pas de Int32 :(
				m_buffer.Write(cache.GetLiteral(value));
			}
		}

		public void WriteEnum(Enum? value)
		{
			if (value == null)
			{
				WriteNull();
				return;
			}

			var fmt = m_attributes?.EnumFormat ?? JsonEnumFormat.Inherits;
			if ((fmt == JsonEnumFormat.Inherits && m_enumAsString) || fmt == JsonEnumFormat.String)
			{
				string str = value.ToString("G");
				if (m_enumCamelCased) str = CamelCase(str);
				WriteValue(str);
			}
			else
			{
				//note: on pourrait convertir l'enum en (int) et appeler WriteInt32(...) mais certaines enums ne dérivent pas de Int32 :(
				string str = value.ToString("D");
				m_buffer.Write(str);
			}
		}

		public void WriteValue(decimal value)
		{
			if (value == 0)
			{ // le plus courant (objets vides)
				m_buffer.Write('0');
			}
			else
			{ // conversion directe
				m_buffer.Write(value.ToString(null, NumberFormatInfo.InvariantInfo));
				// note: on n'ajoute pas le '.0' pour les entiers, car un 'decimal' peut être n'importe quoi, surtout dans le cas d'un objet dynamic,
				// ou '1' est représenté par un decimal s'il n'y a pas de précision du type dans l'invocation dynamic.
				// Si on rajoute un '.0', on casse la règle: jsonText == Serialize(DeserializeDynamic(jsonText))
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(decimal? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		/// <summary>Ecrit une date dont au format indiqué par le paramétrage</summary>
		public void WriteValue(DateTime value)
		{
			switch (m_dateFormat)
			{
				// CrystalJsonSettings.DateFormat.Default:
				// CrystalJsonSettings.DateFormat.TimeStampIso8601:
				default:
				{  // ISO 8601 "YYYY-MM-DDTHH:MM:SS.00000"
					WriteDateTimeIso8601(value);
					break;
				}
				case CrystalJsonSettings.DateFormat.Microsoft:
				{ // "\/Date(#####)\/" pour UTC, ou "\/Date(####+HHMM)\/" pour LocalTime
					WriteDateTimeMicrosoft(value);
					break;
				}
				case CrystalJsonSettings.DateFormat.JavaScript:
				{ // "new Date(123456789)"
					WriteDateTimeJavaScript(value);
					break;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(DateTime? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		/// <summary>Ecrit une date dont au format indiqué par le paramétrage</summary>
		public void WriteValue(DateTimeOffset value)
		{
			switch(m_dateFormat)
			{
				// CrystalJsonSettings.DateFormat.Default:
				// CrystalJsonSettings.DateFormat.TimeStampIso8601:
				default:
				{  // ISO 8601 "YYYY-MM-DDTHH:MM:SS.00000"
					WriteDateTimeIso8601(value);
					break;
				}
				case CrystalJsonSettings.DateFormat.Microsoft:
				{ // "\/Date(#####)\/" pour UTC, ou "\/Date(####+HHMM)\/" pour LocalTime
					WriteDateTimeMicrosoft(value);
					break;
				}
				case CrystalJsonSettings.DateFormat.JavaScript:
				{ // "new Date(123456789)"
					WriteDateTimeJavaScript(value);
					break;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(DateTimeOffset? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		/// <summary>Ecrit une date au format Microsoft: "\/Date(....)\/"</summary>
		public void WriteDateTimeMicrosoft(DateTime date)
		{
			if (date == DateTime.MinValue)
			{ // pour éviter de s'embrouiller avec les TimeZones...
				m_buffer.Write(JsonTokens.MicrosoftDateTimeMinValue);
			}
			else if (date == DateTime.MaxValue)
			{ // idem
				m_buffer.Write(JsonTokens.MicrosoftDateTimeMaxValue);
			}
			else
			{ // "\/Date(######)\/" ou "\/Date(######+HHMM)\/"

				var sb = new StringBuilder(36)
					.Append(JsonTokens.DateBeginMicrosoft)
					.Append(CrystalJson.DateToJavaScriptTicks(date).ToString(null, NumberFormatInfo.InvariantInfo));
				if (date.Kind != DateTimeKind.Utc)
				{ // précise la timezone pour savoir la reconvertir correctement en LocalTime après
					// => "/Date(.....+HHMM)/"  ou "/Date(...-HHMM)/"
					var offset = TimeZoneInfo.Local.GetUtcOffset(date);
					WriteDateTimeMicrosoftTimeZone(sb, offset);
				}
				sb.Append(JsonTokens.DateEndMicrosoft);
				m_buffer.Write(sb.ToString());
			}
		}

		/// <summary>Ecrit une date au format Microsoft: "\/Date(....)\/"</summary>
		public void WriteDateTimeMicrosoft(DateTimeOffset date)
		{
			if (date == DateTimeOffset.MinValue)
			{ // pour éviter de s'embrouiller avec les TimeZones...
				m_buffer.Write(JsonTokens.MicrosoftDateTimeMinValue);
			}
			else if (date == DateTimeOffset.MaxValue)
			{ // idem
				m_buffer.Write(JsonTokens.MicrosoftDateTimeMaxValue);
			}
			else
			{ // "\/Date(######+HHMM)\/"
				var sb = new StringBuilder(36)
					.Append(JsonTokens.DateBeginMicrosoft)
					.Append(CrystalJson.DateToJavaScriptTicks(date).ToString(null, NumberFormatInfo.InvariantInfo));
				// précise la timezone pour savoir la reconvertir correctement en LocalTime après
				// => "/Date(.....+HHMM)/"  ou "/Date(...-HHMM)/"
				var offset = date.Offset;
				WriteDateTimeMicrosoftTimeZone(sb, offset);
				sb.Append(JsonTokens.DateEndMicrosoft);
				m_buffer.Write(sb.ToString());
			}
		}

		/// <summary>Ecrit le "+HHMM"/"-HHMM" correspondant à l'offset UTC d'une TimeZone</summary>
		internal static void WriteDateTimeMicrosoftTimeZone(StringBuilder sb, TimeSpan offset)
		{
			//note: si GMT-xxx, Hours et Minutes sont négatifs !!!
			int h = Math.Abs(offset.Hours);
			int m = Math.Abs(offset.Minutes);
			sb.Append(offset < TimeSpan.Zero ? '-' : '+').Append((char)('0' + (h / 10))).Append((char)('0' + (h % 10))).Append((char)('0' + (m / 10))).Append((char)('0' + (m % 10)));
		}

		/// <summary>Ecrit une date au format ISO 8601: "YYYY-MM-DDTHH:mm:ss.ffff+TZ"</summary>
		public void WriteDateTimeIso8601(DateTime date)
		{
			if (date == DateTime.MinValue)
			{ // MinValue est sérialisée comme une chaine vide
				m_buffer.Write(JsonTokens.EmptyString);
			}
			else if (date == DateTime.MaxValue)
			{ // MaxValue ne doit pas mentioner la TZ
				m_buffer.Write(JsonTokens.Iso8601DateTimeMaxValue);
			}
			else
			{
				var buf = GetTempBuffer();
				int n = CrystalJsonFormatter.FormatIso8601DateTime(buf, date, date.Kind, default(TimeSpan?), '"');
				m_buffer.Write(buf, 0, n);
			}
		}

		/// <summary>Ecrit une date au format ISO 8601: "YYYY-MM-DDTHH:mm:ss.ffff+TZ"</summary>
		public void WriteDateTimeIso8601(DateTimeOffset date)
		{
			if (date == DateTimeOffset.MinValue)
			{ // MinValue est sérialisée comme une chaine vide
				m_buffer.Write(JsonTokens.EmptyString);
			}
			else if (date == DateTimeOffset.MaxValue)
			{ // MaxValue ne doit pas mentioner la TZ
				m_buffer.Write(JsonTokens.Iso8601DateTimeMaxValue);
			}
			else
			{
				var buf = GetTempBuffer();
				int n = CrystalJsonFormatter.FormatIso8601DateTime(buf, date.DateTime, DateTimeKind.Local, date.Offset, '"');
				m_buffer.Write(buf, 0, n);
			}
		}

		/// <summary>Ecrit une date au format JavaScript: new Date(123456789)</summary>
		public void WriteDateTimeJavaScript(DateTime date)
		{
			var buffer = m_buffer;
			if (date == DateTime.MinValue)
			{ // pour éviter de s'embrouiller avec les TimeZones...
				buffer.Write(JsonTokens.JavaScriptDateTimeMinValue);
			}
			else if (date == DateTime.MaxValue)
			{ // idem
				buffer.Write(JsonTokens.JavaScriptDateTimeMaxValue);
			}
			else
			{ // "new Date(#####)"
				buffer.Write(JsonTokens.DateBeginJavaScript);
				buffer.Write(CrystalJson.DateToJavaScriptTicks(date).ToString(NumberFormatInfo.InvariantInfo));
				buffer.Write(')');
			}
		}

		/// <summary>Ecrit une date au format JavaScript: new Date(123456789)</summary>
		public void WriteDateTimeJavaScript(DateTimeOffset date)
		{
			var buffer = m_buffer;
			if (date == DateTimeOffset.MinValue)
			{ // pour éviter de s'embrouiller avec les TimeZones...
				buffer.Write(JsonTokens.JavaScriptDateTimeMinValue);
			}
			else if (date == DateTimeOffset.MaxValue)
			{ // idem
				buffer.Write(JsonTokens.JavaScriptDateTimeMaxValue);
			}
			else
			{ // "new Date(#####)"
				buffer.Write(JsonTokens.DateBeginJavaScript);
				buffer.Write(CrystalJson.DateToJavaScriptTicks(date).ToString(null, NumberFormatInfo.InvariantInfo));
				buffer.Write(')');
			}
		}

		public void WriteValue(TimeSpan value)
		{
			if (value == TimeSpan.Zero)
				m_buffer.Write(JsonTokens.Zero); //.DecimalZero);
			else
				WriteValue(value.TotalSeconds);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(TimeSpan? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(Guid value)
		{
			var buffer = m_buffer;
			if (value == Guid.Empty)
			{
				buffer.Write(JsonTokens.Null);
			}
			else if (!m_javascript)
			{
				buffer.Write('"');
				buffer.Write(value.ToString());
				buffer.Write('"');
			}
			else
			{
				buffer.Write('\'');
				buffer.Write(value.ToString());
				buffer.Write('\'');
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(Guid? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(Uuid128 value)
		{
			var buffer = m_buffer;
			if (value == Uuid128.Empty)
			{
				buffer.Write(JsonTokens.Null);
			}
			else if (!m_javascript)
			{
				buffer.Write('"');
				buffer.Write(value.ToString());
				buffer.Write('"');
			}
			else
			{
				buffer.Write('\'');
				buffer.Write(value.ToString());
				buffer.Write('\'');
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(Uuid128? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(Uuid96 value)
		{
			var buffer = m_buffer;
			if (value == Uuid96.Empty)
			{
				buffer.Write(JsonTokens.Null);
			}
			else if (!m_javascript)
			{
				buffer.Write('"');
				buffer.Write(value.ToString());
				buffer.Write('"');
			}
			else
			{
				buffer.Write('\'');
				buffer.Write(value.ToString());
				buffer.Write('\'');
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(Uuid96? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(Uuid80 value)
		{
			var buffer = m_buffer;
			if (value == Uuid80.Empty)
			{
				buffer.Write(JsonTokens.Null);
			}
			else if (!m_javascript)
			{
				buffer.Write('"');
				buffer.Write(value.ToString());
				buffer.Write('"');
			}
			else
			{
				buffer.Write('\'');
				buffer.Write(value.ToString());
				buffer.Write('\'');
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(Uuid80? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(Uuid64 value)
		{
			var buffer = m_buffer;
			if (value == Uuid64.Empty)
			{
				buffer.Write(JsonTokens.Null);
			}
			else if (!m_javascript)
			{
				buffer.Write('"');
				buffer.Write(value.ToString());
				buffer.Write('"');
			}
			else
			{
				buffer.Write('\'');
				buffer.Write(value.ToString());
				buffer.Write('\'');
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(Uuid64? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(NodaTime.Duration value)
		{
			if (value == NodaTime.Duration.Zero)
			{
				m_buffer.Write(JsonTokens.Zero); //.DecimalZero);
				return;
			}

			double sec = value.TotalSeconds;
			if (sec < 100_000_000)
			{
				WriteValue(value.TotalSeconds);
				return;
			}

			// on doit décomposer (days, nanosOfDays) en (seconds, nanosOfSeconds)
			int days = value.Days;
			long nanosOfDay = value.NanosecondOfDay;
			long secsOfDay = nanosOfDay / 1_000_000_000;
			long nanos = nanosOfDay - (secsOfDay * 1_000_000_000);
			long secs = secsOfDay + (days * 86400);

			CrystalJsonFormatter.WriteFixedIntegerWithDecimalPartUnsafe(m_buffer, secs, nanos, 9, GetTempBuffer());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(NodaTime.Duration? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(NodaTime.Instant date)
		{
			if (date == NodaTime.Instant.MinValue)
			{ // MinValue est sérialisée comme une chaine vide
				m_buffer.Write(JsonTokens.EmptyString);
			}
			else if (date == NodaTime.Instant.MaxValue)
			{ // MaxValue ne doit pas mentioner la TZ
				m_buffer.Write(JsonTokens.Iso8601DateTimeMaxValue);
			}
			else
			{ // "2013-07-26T16:45:20.1234567Z"
				//TODO: optimisation pour pas avoir a encoder la string...
				WriteValue(CrystalJsonNodaPatterns.Instants.Format(date));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(NodaTime.Instant? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(NodaTime.LocalDateTime date)
		{
			// "1988-04-19T00:35:56" ou "1988-04-19T00:35:56.342" (pas de 'Z' ou de timezone)
			//TODO: optimisation pour pas avoir a encoder la string...
			WriteValue(CrystalJsonNodaPatterns.LocalDateTimes.Format(date));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(NodaTime.LocalDateTime? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(NodaTime.ZonedDateTime date)
		{
			//TODO: optimisation pour pas avoir a encoder la string...
			WriteValue(CrystalJsonNodaPatterns.ZonedDateTimes.Format(date));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(NodaTime.ZonedDateTime? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(NodaTime.OffsetDateTime date)
		{
			//TODO: optimisation pour pas avoir a encoder la string...
			WriteValue(CrystalJsonNodaPatterns.OffsetDateTimes.Format(date));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(NodaTime.OffsetDateTime? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(NodaTime.Offset offset)
		{
			// "+01:00"
			WriteValue(CrystalJsonNodaPatterns.Offsets.Format(offset));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(NodaTime.Offset? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(NodaTime.LocalDate date)
		{
			// "2014-07-22"
			WriteValue(CrystalJsonNodaPatterns.LocalDates.Format(date));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(NodaTime.LocalDate? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(NodaTime.LocalTime time)
		{
			// "11:39:42.123457"
			WriteValue(CrystalJsonNodaPatterns.LocalTimes.Format(time));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(NodaTime.LocalTime? value)
		{
			if (value.HasValue) WriteValue(value.Value); else WriteNull();
		}

		public void WriteValue(NodaTime.DateTimeZone? zone)
		{
			if (zone == null)
			{
				WriteNull();
			}
			else
			{ // "Europe/Paris"
				WriteValue(zone.Id);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(Version? value)
		{
			WriteValue(value?.ToString());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(System.Net.IPAddress? value)
		{
			WriteValue(value?.ToString());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteValue(Uri? value)
		{
			WriteValue(value?.OriginalString);
		}

		#endregion

		public void WriteBuffer(byte[]? bytes)
		{
			var buffer = m_buffer;
			if (bytes == null)
			{
				buffer.Write(JsonTokens.Null);
			}
			else if (bytes.Length == 0)
			{
				buffer.Write(JsonTokens.EmptyString);
			}
			else
			{ // note: Base64 ne contient ni ' ni " donc pas besoin d'escaper !
				buffer.Write('"');
				buffer.Write(Convert.ToBase64String(bytes));
				buffer.Write('"');
			}
		}

		public void WriteBuffer(byte[]? bytes, int offset, int count)
		{
			var buffer = m_buffer;
			if (bytes == null)
			{
				buffer.Write(JsonTokens.Null);
			}
			else if (count == 0)
			{
				buffer.Write(JsonTokens.EmptyString);
			}
			else
			{ // note: Base64 ne contient ni ' ni " donc pas besoin d'escaper !
				buffer.Write('"');
				//TODO: Si count est très grand, on pourrait switcher sur ToBase64CharArray et buffer ?
				buffer.Write(Convert.ToBase64String(bytes, offset, count));
				buffer.Write('"');
			}
		}

		public void WriteBuffer(ReadOnlySpan<byte> bytes)
		{
			var buffer = m_buffer;
			if (bytes.Length == 0)
			{
				buffer.Write(JsonTokens.EmptyString);
			}
			else
			{ // note: Base64 ne contient ni ' ni " donc pas besoin d'escaper !
				buffer.Write('"');
				//TODO: Si count est très grand, on pourrait switcher sur ToBase64CharArray et buffer ?
#if USE_SPAN_API
				buffer.Write(Convert.ToBase64String(bytes, Base64FormattingOptions.None));
#else
				buffer.Write(Base64Encoding.ToBase64String(bytes));
#endif
				buffer.Write('"');
			}
		}

		public void WriteBuffer(Slice bytes)
		{
			var buffer = m_buffer;
			if (bytes.Count == 0)
			{
				if (bytes.Array == null)
				{
					buffer.Write(JsonTokens.Null);
				}
				else
				{
					buffer.Write(JsonTokens.EmptyString);
				}
			}
			else
			{ // note: Base64 ne contient ni ' ni " donc pas besoin d'escaper !
				buffer.Write('"');
				//TODO: Si count est très grand, on pourrait switcher sur ToBase64CharArray et buffer ?
				buffer.Write(Convert.ToBase64String(bytes.Array, bytes.Offset, bytes.Count));
				buffer.Write('"');
			}
		}

#endregion

		#region Field Writers...

		public void WriteFieldNull(string name)
		{
			if (!m_discardNulls)
			{
				WriteName(name);
				WriteNull();
			}
		}

		public void WriteField(string name, string? value)
		{
			if (value != null || !m_discardNulls)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, bool value)
		{
			if (value || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, bool? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, int value)
		{
			if (value != 0 || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, int? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, long value)
		{
			if (value != 0L || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, long? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, float value)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (value != 0f || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, float? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, double value)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (value != 0d || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, double? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, DateTime value)
		{
			if (value != DateTime.MinValue || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, DateTime? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, Guid value)
		{
			if (value != Guid.Empty || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, Guid? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, Uuid128 value)
		{
			if (value != Uuid128.Empty|| !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, Uuid128? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, Uuid96 value)
		{
			if (value != Uuid96.Empty || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, Uuid96? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, Uuid80 value)
		{
			if (value != Uuid80.Empty || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, Uuid80? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, Uuid64 value)
		{
			if (value != Uuid64.Empty || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, Uuid64? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		#region NodaTime Types...

		public void WriteField(string name, NodaTime.Instant value)
		{
			if (value.ToUnixTimeTicks() != 0 || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, NodaTime.Instant? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, NodaTime.Duration value)
		{
			if (value.BclCompatibleTicks != 0 || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, NodaTime.Duration? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, NodaTime.ZonedDateTime value)
		{
			//TODO: defaults?
			WriteName(name);
			WriteValue(value);
		}

		public void WriteField(string name, NodaTime.ZonedDateTime? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, NodaTime.LocalDateTime value)
		{
			//TODO: defaults?
			WriteName(name);
			WriteValue(value);
		}

		public void WriteField(string name, NodaTime.LocalDateTime? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, NodaTime.LocalDate value)
		{
			//TODO: defaults?
			WriteName(name);
			WriteValue(value);
		}

		public void WriteField(string name, NodaTime.LocalDate? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, NodaTime.LocalTime value)
		{
			if (value.TickOfDay != 0 || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, NodaTime.LocalTime? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, NodaTime.OffsetDateTime value)
		{
			//TODO: defaults?
			WriteName(name);
			WriteValue(value);
		}

		public void WriteField(string name, NodaTime.OffsetDateTime? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, NodaTime.Offset value)
		{
			if (value.Milliseconds != 0 || !m_discardDefaults)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		public void WriteField(string name, NodaTime.Offset? value)
		{
			if (value.HasValue)
			{
				WriteName(name);
				WriteValue(value.Value);
			}
			else
			{
				WriteFieldNull(name);
			}
		}

		public void WriteField(string name, NodaTime.DateTimeZone value)
		{
			if (value != null || !m_discardNulls)
			{
				WriteName(name);
				WriteValue(value);
			}
		}

		#endregion

		public void WriteField(string name, JsonValue value)
		{
			//note: on écrit JsonNull.Null, mais pas JsonNull.Missing!
			if (!value.IsNullOrMissing() || !m_discardNulls || object.ReferenceEquals(value, JsonNull.Null))
			{
				WriteName(name);
				value.JsonSerialize(this);
			}
		}

		public void WriteField(string name, object value, Type declaredType)
		{
			if (value != null || !m_discardNulls)
			{
				WriteName(name);
				CrystalJsonVisitor.VisitValue(value, declaredType, this);
			}
		}

		public void WriteField<T>(string name, T value)
		{
			WriteName(name);
			VisitValue(value);
		}

		public void WriteField<T>(string name, T? value)
			where T : struct
		{
			if (value.HasValue)
			{
				WriteName(name);
				VisitValue<T>(value.Value);
			}
			else if (!m_discardNulls)
			{
				WriteName(name);
				WriteNull();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void VisitValue(object? value, Type declaredType)
		{
			CrystalJsonVisitor.VisitValue(value, declaredType, this);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void VisitValue<T>(T value)
		{
			CrystalJsonVisitor.VisitValue<T>(value, this);
		}

		public void VisitArray<T>([InstantHandle] IEnumerable<T> array, Action<CrystalJsonWriter, T> action)
		{
			Contract.NotNull(action);
			if (array == null)
			{
				WriteNull();
			}
			else
			{
				var state = BeginArray();
				foreach (var item in array)
				{
					WriteFieldSeparator();
					action(this, item);
				}
				EndArray(state);
			}
		}

		/// <summary>Visite un collection d'éléments</summary>
		/// <typeparam name="T">Type des éléments d'une collection</typeparam>
		/// <param name="array"></param>
		public void WriteArray<T>([InstantHandle] IEnumerable<T>? array)
		{
			if (array == null)
			{
				WriteNull();
			}
			else
			{
				var state = BeginArray();
				foreach (var item in array)
				{
					WriteFieldSeparator();
					CrystalJsonVisitor.VisitValue<T>(item, this);
				}
				EndArray(state);
			}
		}

		public void WriteArray<T>(T[] array, int offset, int count)
		{
			//TODO: check params ?

			if (count == 0)
			{
				WriteEmptyArray();
				return;
			}

			var state = BeginArray();
			int end = offset + count;
			for (int i = offset; i < end; i++)
			{
				WriteFieldSeparator();
				CrystalJsonVisitor.VisitValue<T>(array[i], this);
			}
			EndArray(state);
		}

		public void WriteArray<TKey, TValue>(ICollection<KeyValuePair<TKey, TValue>> source)
		{
			if (source == null)
			{
				WriteNull();
			}
			else if (source.Count == 0)
			{
				WriteEmptyArray();
			}
			else
			{
				var s1 = BeginArray();
				foreach (var kvp in source)
				{
					WriteFieldSeparator();
					var s2 = BeginArray();
					{
						WriteHeadSeparator();
						VisitValue<TKey>(kvp.Key);
						WriteTailSeparator();
						VisitValue<TValue>(kvp.Value);
					}
					EndArray(s2);
				}
				EndArray(s1);
			}
		}

		public void WriteDictionary(IDictionary<string, object> map)
		{
			CrystalJsonVisitor.VisitGenericObjectDictionary(map, this);
		}

		public void WriteDictionary(IDictionary<string, string> map)
		{
			CrystalJsonVisitor.VisitStringDictionary(map, this);
		}

		public void WriteDictionary<TValue>(Dictionary<string, TValue> map)
		{
			CrystalJsonVisitor.VisitGenericDictionary<TValue>(map, this);
		}

		public void VisitXmlNode(System.Xml.XmlNode node)
		{
			CrystalJsonVisitor.VisitXmlNode(node, this);
		}

		#endregion

	}

}
