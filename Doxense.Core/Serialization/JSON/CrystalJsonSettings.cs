#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Caching;
	using JetBrains.Annotations;

	/// <summary>Param�tres de s�rialisation JSON</summary>
	/// <remarks>Les instances de ce type son immutable</remarks>
	[DebuggerDisplay("Flags={m_flags.ToString(\"X\")}, Target={TargetLanguage}, Layout={TextLayout}, Dates={DateFormatting}, HideDefault={HideDefaultValues}, ShowNulls={ShowNullMembers}, Large={OptimizeForLargeData}, Interning={InterningMode}")]
	[DebuggerNonUserCode]
	public sealed class CrystalJsonSettings : IEquatable<CrystalJsonSettings>
	{
		#region Nested Enums ...

		/// <summary>Mode de formatage du texte JSON g�n�r�</summary>
		public enum Layout
		{
			Formatted = 0,
			Indented = 1,
			Compact = 2,

			//Reserved = 3
		}

		/// <summary>Format d'encodage des dates</summary>
		public enum DateFormat
		{
			// IMPORTANT: maximum 4 valeurs, car cette �num�ration est stock�e avec 2 bits dans les flags ! (sinon, il faudra updater OptionFlags)

			Default = 0,
			TimeStampIso8601 = 1,
			Microsoft = 2,
			JavaScript = 3,
		}

		/// <summary>Mode d'interning des strings</summary>
		/// <remarks>L'interning permet de r�duire la taille occup�e par un JSON Object en m�moire, en faisant en sorte que toutes les occurrences d'une m�me string pointent vers la m�me variable
		/// C'est surtout int�ressant par exemple pour une Array d'Object, o� les noms des propri�t�s de l'objet est r�p�t� N fois en m�moire.
		/// Interne les valeurs peut �tre aussi utile s'il y a beaucoup de redondance dans l'espace de valeur possible (mot cl�, �num�ration sous forme cha�ne, ...)
		/// Le parseur doit faire lookup de chaque cha�ne dans un dictionnaire, ce qui a un co�t si le document est volumineux et avec quasiment aucune redondance...</remarks>
		public enum StringInterning
		{
			// IMPORTANT: maximum 4 valeurs, car cette �num�ration est stock�e avec 2 bits dans les flags ! (sinon, il faudra updater OptionFlags)

			/// <summary>Seul les noms de propri�t� d'objets, et les petits nombres (3 caract�res ou moins) seront intern�es</summary>
			Default = 0,
			/// <summary>Aucune string ne sera intern�e</summary>
			Disabled = 1,
			/// <summary>M�me que Default, mais inclue �galement tout les nombres</summary>
			IncludeNumbers = 2,
			/// <summary>Tous les types de champs (nom de propri�t�, texte, guid, nombres), excluant les dates, seront interned</summary>
			IncludeValues = 3, //REVIEW: renommer en "All" ?
			//README: cette enum ne prend que 2 bits dans le champ "m_flags"! S'il faut rajouter des entr�es, il faudra modifier le layout des flags pour rajouter 1 ou plusieurs bits!
		}

		public enum FloatFormat
		{
			/// <summary>Formatage par d�faut, qui est identique � TDB</summary> //TODO: pour l'instant c'est Symbol, mais ca va devenir String!
			Default = 0,
			/// <summary>Utilise les symbols <c>NaN</c>, <c>Infinity</c> ou <c>-Infinity</c>. Note: le JSON g�n�r� n'est *PAS* strictement conforme a la RFC7159 qui ne sp�cifie pas ces symbols!)</summary>
			Symbol = 1,
			/// <summary>Utilise les cha�nes <c>"NaN"</c>, <c>"Infinity"</c> et <c>"-Infinity"</c>, de mani�re similaire � JSON.NET. Le JSON g�n�r� est conforme � la RFC7159 mais le consommateur doit savoir qu'il peut avoir des strings � la place d'un nombre!</summary>
			String = 2,
			/// <summary>Utilise le token <c>null</c> pour s�rialiser <see cref="double.NaN"/>, <see cref="double.PositiveInfinity"/> et <see cref="double.NegativeInfinity"/>. Le JSON g�n�r� est conforme � la RFC7159, mais il ne peut plus �tre utilis� pour faire un roundtrip parfait d'objet .NET (les NaN seront remplac�s par null qui sera d�s�rialis� en null ou 0)</summary>
			Null = 3,
			/// <summary>Utilise la notation JavaScript (<c>Number.NaN</c>, <c>Number.POSITIVE_INFINITY</c>, ...)</summary>
			JavaScript = 4,
		}

		// ReSharper disable InconsistentNaming
		[Flags]
		internal enum OptionFlags
		{
			None = 0,

			UseCamelCasingForName = 0x1,
			ShowNullMembers = 0x2,
			HideDefaultValues = 0x4,
			EnumsAsString = 0x8,

			OptimizeForLargeData = 0x10,
			HideClassId = 0x20,
			FieldsIgnoreCase = 0x40,
			DoNotTrackVisited = 0x80,

			// Layout Enum
			Layout_Formatted = 0x00,
			Layout_Indented = 0x100,
			Layout_Compact = 0x200,
			Layout_Reserved = 0x300, // NOT USED
			Layout_Mask = 0x300, // tous les bits � 1

			// DateFormat Enum
			DateFormat_Default = 0x000,
			DateFormat_TimeStampIso8601 = 0x400,
			DateFormat_Microsoft = 0x800,
			DateFormat_JavaScript = 0xC00,
			DateFormat_Mask = 0xC00, // tous les bits � 1

			// StringInterning Enum
			StringInterning_Default = 0x0000,
			StringInterning_Disabled = 0x1000,
			StringInterning_IncludeNumbers = 0x2000,
			StringInterning_IncludeValues = 0x3000,
			StringInterning_Mask = 0x3000, // tous les bits � 1

			// Target Enum
			Target_Json = 0x00000,
			Target_JavaScript = 0x10000,
			Target_Reserved1 = 0x20000,
			Target_Reserved2 = 0x30000,
			Target_Mask = 0x30000,

			// Misc
			UseCamelCasingForEnums = 0x100000,
			DenyTrailingComma = 0x200000,
			OverwriteDuplicateFields = 0x400000,

			// Number Formatting
			FloatFormat_Default    = 0x0_0_000000,
			FloatFormat_Symbol     = 0x0_1_000000,
			FloatFormat_String     = 0x0_2_000000,
			FloatFormat_Null       = 0x0_3_000000,
			FloatFormat_JavaScript = 0x0_4_000000,
			FloatFormat_Mask       = 0x0_7_000000, // tous les bits � 1
		}
		// ReSharper restore InconsistentNaming

		public enum Target
		{
			Json = 0,
			JavaScript = 1,

			//Reserved1 = 2,
			//Reserved2 = 3,
		}

		#endregion

		#region Private Members...

		/// <summary>Flags contenant les options de s�rialisation de type on/off</summary>
		private readonly OptionFlags m_flags;

		#endregion

		#region Constructors...

		public CrystalJsonSettings()
		{ }

		internal CrystalJsonSettings(OptionFlags flags)
		{
			m_flags = flags;
		}

		#endregion

		#region Public Properties...

		/// <summary>Flags correspondants au param�trage</summary>
		internal OptionFlags Flags
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return m_flags; } }

		/// <summary>Language cible de la s�rialisation (JSON, JavaScript, ...)</summary>
		public Target TargetLanguage
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (Target)(((int)m_flags >> 16) & 0x3); }
		}

		private static OptionFlags SetTargetLanguage(OptionFlags flags, Target value)
		{
			if (value < Target.Json || value > Target.JavaScript) throw new ArgumentException("Invalid target language mode", nameof(value));
			return (flags & ~OptionFlags.Target_Mask) | (OptionFlags)(((int)value & 0x3) << 16);
		}

		/// <summary>Mode de formattage du texte</summary>
		public Layout TextLayout
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (Layout) (((int)m_flags >> 8) & 0x3); }
		}

		private static OptionFlags SetTextLayout(OptionFlags flags, Layout value)
		{
			if (value < Layout.Formatted || value > Layout.Compact) throw new ArgumentException("Invalid text layout mode", nameof(value));
			return (flags & ~OptionFlags.Layout_Mask) | (OptionFlags)(((int)value & 0x3) << 8);
		}

		/// <summary>Format de conversion de dates</summary>
		public DateFormat DateFormatting
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (DateFormat)(((int)m_flags >> 10) & 0x3); }
		}

		private static OptionFlags SetDateFormatting(OptionFlags flags, DateFormat value)
		{
			if (value < DateFormat.Default || value > DateFormat.JavaScript) throw new ArgumentException("Invalid date format mode", nameof(value));
			return (flags & ~OptionFlags.DateFormat_Mask) | (OptionFlags)(((int)value & 0x3) << 10);
		}

		/// <summary>Si true, n'interne pas les noms de propri�t�s des objets</summary>
		public StringInterning InterningMode
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (StringInterning)(((int)m_flags >> 12) & 0x3); }
		}

		private static OptionFlags SetInterningMode(OptionFlags flags, StringInterning value)
		{
			if (value < StringInterning.Default || value > StringInterning.IncludeValues) throw new ArgumentException("Invalid string interning mode", nameof(value));
			return (flags & ~OptionFlags.StringInterning_Mask) | (OptionFlags) (((int) value & 0x3) << 12);
		}

		/// <summary>Si true, convertit les noms de propri�t�s en camelCasing</summary>
		public bool UseCamelCasingForNames
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (m_flags & OptionFlags.UseCamelCasingForName) != 0; }
		}

		private static OptionFlags SetUseCamelCasingForNames(OptionFlags flags, bool value)
		{
			return value ? flags | OptionFlags.UseCamelCasingForName : flags & ~OptionFlags.UseCamelCasingForName;
		}

		/// <summary>Si true, ignore la casse sur les noms de champs lors de la d�s�rialisation</summary>
		public bool IgnoreCaseForNames
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (m_flags & OptionFlags.FieldsIgnoreCase) != 0; }
		}

		private static OptionFlags SetIgnoreCaseForNames(OptionFlags flags, bool value)
		{
			return value ? flags | OptionFlags.FieldsIgnoreCase : flags & ~OptionFlags.FieldsIgnoreCase;
		}

		/// <summary>Si true, s�rialise quand m�me les membres null (class ou Nullable) d'un objet.</summary>
		/// <remarks>Ignor� si HideDefaultValues = true</remarks>
		public bool ShowNullMembers
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (m_flags & OptionFlags.ShowNullMembers) != 0; }
		}

		private static OptionFlags SetShowNullMembers(OptionFlags flags, bool value)
		{
			return value ? flags | OptionFlags.ShowNullMembers : flags & ~OptionFlags.ShowNullMembers;
		}

		/// <summary>Si true, ne s�rialise pas les members �gal � default(T) (null, 0, false, DateTime.MinValue, etc..)</summary>
		/// <remarks>Override ShowNullMembers si true</remarks>
		public bool HideDefaultValues
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (m_flags & OptionFlags.HideDefaultValues) != 0; }
		}

		private static OptionFlags SetHideDefaultValues(OptionFlags flags, bool value)
		{
			return value ? flags | OptionFlags.HideDefaultValues : flags & ~OptionFlags.HideDefaultValues;
		}

		/// <summary>Si true, ne s�rialise pas les members �gal � default(T) (null, 0, false, DateTime.MinValue, etc..)</summary>
		public bool EnumsAsString
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (m_flags & OptionFlags.EnumsAsString) != 0; }
		}

		private static OptionFlags SetEnumsAsString(OptionFlags flags, bool value)
		{
			return value ? flags | OptionFlags.EnumsAsString : flags & ~OptionFlags.EnumsAsString;
		}

		/// <summary>Si true, convertit les �num�rations en camelCasing</summary>
		public bool UseCamelCasingForEnums
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (m_flags & OptionFlags.UseCamelCasingForEnums) != 0; }
		}

		private static OptionFlags SetUseCamelCasingForEnums(OptionFlags flags, bool value)
		{
			return value ? flags | OptionFlags.UseCamelCasingForEnums : flags & ~OptionFlags.UseCamelCasingForEnums;
		}

		/// <summary>Si true, ne track pas les objets visit�s (protection contre la r�cursion)</summary>
		/// <remarks>Il reste toujours la protection contre la profondeur maximale</remarks>
		public bool DoNotTrackVisitedObjects
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (m_flags & OptionFlags.DoNotTrackVisited) != 0; }
		}

		private static OptionFlags SetDoNotTrackVisitedObjects(OptionFlags flags, bool value)
		{
			return value ? flags | OptionFlags.DoNotTrackVisited : flags & ~OptionFlags.DoNotTrackVisited;
		}

		/// <summary>Si true, on s'attend a ce que le JSON g�n�r� soit de taille cons�quente.</summary>
		/// <remarks>Augmente la taille des buffer utilis�s pour la s�rialisation / d�s�rialisation</remarks>
		public bool OptimizeForLargeData
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (m_flags & OptionFlags.OptimizeForLargeData) != 0; }
		}

		private static OptionFlags SetOptimizeForLargeData(OptionFlags flags, bool value)
		{
			return value ? flags | OptionFlags.OptimizeForLargeData : flags & ~OptionFlags.OptimizeForLargeData;
		}

		/// <summary>Si true, ne g�n�re pas l'attribut "_class" dans le JSON g�n�r�</summary>
		public bool HideClassId
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (m_flags & OptionFlags.HideClassId) != 0; }
		}

		private static OptionFlags SetHideClassId(OptionFlags flags, bool value)
		{
			return value ? flags | OptionFlags.HideClassId : flags & ~OptionFlags.HideClassId;
		}

		/// <summary>Si true, interdit les ',' en trops � la fin d'une array ou d'un objet.</summary>
		public bool DenyTrailingCommas
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (m_flags & OptionFlags.DenyTrailingComma) != 0; }
		}

		private static OptionFlags SetDenyTrailingComma(OptionFlags flags, bool value)
		{
			return value ? flags | OptionFlags.DenyTrailingComma : flags & ~OptionFlags.DenyTrailingComma;
		}

		/// <summary>Si true, �crase les champs en doublons dans un objet en ne gardant que la derni�re valeur. Si false, throw un exception</summary>
		public bool OverwriteDuplicateFields
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (m_flags & OptionFlags.OverwriteDuplicateFields) != 0; }
		}

		private static OptionFlags SetOverwriteDuplicateFields(OptionFlags flags, bool value)
		{
			return value ? flags | OptionFlags.OverwriteDuplicateFields : flags & ~OptionFlags.OverwriteDuplicateFields;
		}

		/// <summary>Format de conversion de dates</summary>
		public FloatFormat FloatFormatting
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (FloatFormat) (((int) m_flags >> 24) & 0x7); }
		}

		private static OptionFlags SetFloatFormatting(OptionFlags flags, FloatFormat value)
		{
			if (value < FloatFormat.Default || value > FloatFormat.Null) throw new ArgumentException("Invalid float formatting mode", nameof(value));
			return (flags & ~OptionFlags.FloatFormat_Mask) | (OptionFlags) (((int) value & 0x7) << 24);
		}

		#endregion

		#region Equality ...

		public override string ToString()
		{
			return m_flags.ToString();
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as CrystalJsonSettings);
		}

		public bool Equals(CrystalJsonSettings? other)
		{
			return other != null && other.m_flags == m_flags;
		}

		public override int GetHashCode()
		{
			return (int) m_flags;
		}

		#endregion

		#region Fluent API...

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal CrystalJsonSettings Update(OptionFlags flags)
		{
			return flags == m_flags ? this : Create(flags);
		}

		[Pure]
		public CrystalJsonSettings WithTextLayout(Layout layout)
		{
			return Update(SetTextLayout(m_flags, layout));
		}

		[Pure]
		public CrystalJsonSettings Compacted()
		{
			return Update(SetTextLayout(m_flags, Layout.Compact));
		}

		[Pure]
		public CrystalJsonSettings Formatted()
		{
			return Update(SetTextLayout(m_flags, Layout.Formatted));
		}

		[Pure]
		public CrystalJsonSettings Indented()
		{
			return Update(SetTextLayout(m_flags, Layout.Indented));
		}

		[Pure]
		public CrystalJsonSettings WithoutNullMembers()
		{
			return Update(SetShowNullMembers(m_flags, false));
		}

		[Pure]
		public CrystalJsonSettings WithNullMembers()
		{
			return Update(SetShowNullMembers(m_flags, true));
		}

		/// <summary>Fluent helper pour fixer HideDefaultValues � true</summary>
		[Pure]
		public CrystalJsonSettings WithoutDefaultValues()
		{
			return Update(SetHideDefaultValues(m_flags, true));
		}

		/// <summary>Fluent helper pour fixer HideDefaultValues � false</summary>
		[Pure]
		public CrystalJsonSettings WithDefaultValues(bool show = false)
		{
			return Update(SetHideDefaultValues(m_flags, show));
		}

		/// <summary>Sp�cifie le format utilis� pour s�rialiser les dates</summary>
		[Pure]
		public CrystalJsonSettings WithDateFormat(DateFormat format)
		{
			return Update(SetDateFormatting(m_flags, format));
		}

		/// <summary>S�rialise les dates en utilisant le format Iso8601 ("YYYY-MM-DDTHH:mm:ss.ffff+TZ")</summary>
		/// <returns></returns>
		[Pure]
		public CrystalJsonSettings WithIso8601Dates()
		{
			return Update(SetDateFormatting(m_flags, DateFormat.TimeStampIso8601));
		}

		/// <summary>S�rialise les dates en utilisant le format Microsoft ("\/Date(xxxxx)\/")</summary>
		[Pure]
		public CrystalJsonSettings WithMicrosoftDates()
		{
			return Update(SetDateFormatting(m_flags, DateFormat.Microsoft));
		}

		/// <summary>S�rialise les dates en utilisant le format JavaScript ("new Date(xxxx)")</summary>
		[Pure]
		public CrystalJsonSettings WithJavaScriptDates()
		{
			return Update(SetDateFormatting(m_flags, DateFormat.JavaScript));
		}

		/// <summary>Fluent helper pour passer en mode de s�rialisation Javascript (format date, ....)</summary>
		[Pure]
		public CrystalJsonSettings ForJavaScript()
		{
			var flags = SetTargetLanguage(m_flags, Target.JavaScript);
			flags = SetDateFormatting(flags, DateFormat.JavaScript);
			return Update(flags);
		}

		/// <summary>S�rialise les noms de propri�t�s en Pascal Case ("FirstName", comme en C#)</summary>
		[Pure]
		public CrystalJsonSettings PascalCased()
		{
			return Update(SetUseCamelCasingForNames(m_flags, false));
		}

		/// <summary>S�rialise les noms de propri�t�s en Camel Case ("firstName", comme en JS)</summary>
		[Pure]
		public CrystalJsonSettings CamelCased()
		{
			return Update(SetUseCamelCasingForNames(m_flags, true));
		}

		/// <summary>Les �num�rations doivent �tre s�rialis�es sous forme de nombre</summary>
		[Pure]
		public CrystalJsonSettings WithEnumAsNumbers()
		{
			return Update(SetEnumsAsString(m_flags, false));
		}

		/// <summary>Les �num�rations doivent �tre s�rialis�es sous forme de cha�nes de texte</summary>
		[Pure]
		public CrystalJsonSettings WithEnumAsStrings()
		{
			return Update(SetEnumsAsString(m_flags, true));
		}

		/// <summary>Fluent helper pour fixer EnumAsStrings � true</summary>
		/// <param name="camelCased">Indique s'il faut convertir les enumeration en camelCased (true) ou les laisser au format natif</param>
		[Pure]
		public CrystalJsonSettings WithEnumAsStrings(bool camelCased)
		{
			var flags = SetEnumsAsString(m_flags, true);
			flags = SetUseCamelCasingForEnums(flags, camelCased);
			return Update(flags);
		}

		/// <summary>Fluent helper pour fixer DoNotTrackVisitedObjects � true</summary>
		[Pure]
		public CrystalJsonSettings WithoutObjectTracking() //REVIEW: "DisableObjectTracking()" ?
		{
			return Update(SetDoNotTrackVisitedObjects(m_flags, true));
		}

		/// <summary>Fluent helper pour fixer DoNotTrackVisitedObjects � false</summary>
		[Pure]
		public CrystalJsonSettings WithObjectTracking(bool enabled = false)
		{
			return Update(SetDoNotTrackVisitedObjects(m_flags, enabled));
		}

		/// <summary>Configure l'interning de strings, pour r�duire la consommation m�moire (suivant les scenario)</summary>
		/// <param name="mode">Mode d'interning des chaines de texte</param>
		/// <returns></returns>
		[Pure]
		public CrystalJsonSettings WithInterning(StringInterning mode)
		{
			return Update(SetInterningMode(m_flags, mode));
		}

		/// <summary>D�sactive compl�tement l'interning des strings</summary>
		[Pure]
		public CrystalJsonSettings DisableInterning()
		{
			return Update(SetInterningMode(m_flags, StringInterning.Disabled));
		}

		/// <summary>Active les optimisation pour un r�sultat JSON de grande taille</summary>
		[Pure]
		public CrystalJsonSettings ExpectLargeData()
		{
			return Update(SetOptimizeForLargeData(m_flags, true));
		}

		/// <summary>Active les optimisation pour un r�sultat JSON de petite taille</summary>
		/// <returns></returns>
		[Pure]
		public CrystalJsonSettings OptimizedFor(bool largeData)
		{
			return Update(SetOptimizeForLargeData(m_flags, largeData));
		}

		/// <summary>Active ou d�sactive la g�n�ration des attributs "_class" dans le JSON g�n�r�</summary>
		[Pure]
		public CrystalJsonSettings WithClassId(bool enabled = false)
		{
			return Update(SetHideClassId(m_flags, enabled));
		}

		/// <summary>D�sactive la g�n�ration des attributs "_class" dans le JSON g�n�r�</summary>
		[Pure]
		public CrystalJsonSettings WithoutClassId()
		{
			return Update(SetHideClassId(m_flags, true));
		}

		/// <summary>Rend la d�s�rialisation case-sensitive ou case insensitive sur le nom des champs d'un objet (�tat par d�faut)</summary>
		[Pure]
		public CrystalJsonSettings WithCaseOnFields(bool ignoreCase = false)
		{
			return Update(SetIgnoreCaseForNames(m_flags, ignoreCase));
		}

		/// <summary>Rend la d�s�rialisation case-insensitive sur le nom des champs d'un objet</summary>
		[Pure]
		public CrystalJsonSettings WithoutCaseOnFields()
		{
			return Update(SetIgnoreCaseForNames(m_flags, true));
		}

		/// <summary>Autorise la pr�sence de virgules suppl�mentaires en fin d'objet ou d'array (�tat par d�faut)</summary>
		[Pure]
		public CrystalJsonSettings WithTrailingCommas()
		{
			return Update(SetDenyTrailingComma(m_flags, false));
		}

		/// <summary>Interdit la pr�sence de virgules suppl�mentaires en fin d'objet ou d'array, en les ignorant</summary>
		[Pure]
		public CrystalJsonSettings WithoutTrailingCommas()
		{
			return Update(SetDenyTrailingComma(m_flags, true));
		}

		/// <summary>Si un objet contient plusieurs fois le m�me champ, seul le dernier est conserv�</summary>
		/// <returns></returns>
		[Pure]
		public CrystalJsonSettings FlattenDuplicateFields()
		{
			return Update(SetOverwriteDuplicateFields(m_flags, true));
		}

		/// <summary>Si un object contient plusieurs fois le m�me champ, une exception est g�n�r�e</summary>
		/// <returns></returns>
		[Pure]
		public CrystalJsonSettings ThrowOnDuplicateFields()
		{
			return Update(SetOverwriteDuplicateFields(m_flags, false));
		}

		/// <summary>D�fini le format de s�rialisation des nombres � virgules</summary>
		[Pure]
		public CrystalJsonSettings WithFloatFormat(FloatFormat format)
		{
			return Update(SetFloatFormatting(m_flags, format));
		}

		#endregion

		#region Default Globals...

		/// <summary>S�rialization JSON (avec le minimum de formatage)</summary>
		public static CrystalJsonSettings Json { get; } = new CrystalJsonSettings();

		/// <summary>S�rialization JSON (le plus compact possible)</summary>
		public static CrystalJsonSettings JsonCompact { get; } = new CrystalJsonSettings(OptionFlags.Target_Json | OptionFlags.Layout_Compact);

		/// <summary>S�rialization JSON (indent�e pour �tre lisible par un �tre humain)</summary>
		public static CrystalJsonSettings JsonIndented { get; } = new CrystalJsonSettings(OptionFlags.Target_Json | OptionFlags.Layout_Indented);

		/// <summary>Parsing JSON en mode stricte</summary>
		/// <remarks>Interdit les trailing commas en fin d'objet ou d'array</remarks>
		public static CrystalJsonSettings JsonStrict { get; } = new CrystalJsonSettings(OptionFlags.Target_Json | OptionFlags.DenyTrailingComma);

		/// <summary>S�rialization JavaScript optimis�e (avec le minimum de formatage)</summary>
		public static CrystalJsonSettings JavaScript { get; } = new CrystalJsonSettings(OptionFlags.Target_JavaScript);

		/// <summary>S�rialization JavaScript (le plus compact possible)</summary>
		public static CrystalJsonSettings JavaScriptCompact { get; } = new CrystalJsonSettings(OptionFlags.Target_JavaScript | OptionFlags.Layout_Compact);

		/// <summary>S�rialization JavaScript (indent�e pour �tre lisible par un �tre humain)</summary>
		public static CrystalJsonSettings JavaScriptIndented { get; } = new CrystalJsonSettings(OptionFlags.Target_JavaScript | OptionFlags.Layout_Indented);

		private static readonly QuasiImmutableCache<int, CrystalJsonSettings> Cached;

		static CrystalJsonSettings()
		{
			// create the initial cache for most defaults
			var defaults = new Dictionary<int, CrystalJsonSettings>();
			foreach (var s in new[] {Json, JsonCompact, JsonIndented, JsonStrict, JavaScript, JavaScriptCompact, JavaScriptIndented})
			{
				defaults[(int) s.Flags] = s;
				//also cache the versions with enum as strings
				var s2 = new CrystalJsonSettings(s.Flags | OptionFlags.EnumsAsString);
				defaults[(int) s2.Flags] = s2;
			}
			Cached = new QuasiImmutableCache<int, CrystalJsonSettings>(defaults, valueFactory: (v) => new CrystalJsonSettings((OptionFlags) v));
		}

		internal static CrystalJsonSettings Create(OptionFlags flags)
		{
			return Cached.GetOrAdd((int) flags);
		}

		#endregion

	}

}
