#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using Doxense.IO;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;

	/// <summary>Classe capable de générer manuellement un objet JSON simple</summary>
	public sealed class CrystalJsonBuilder
	{
		#region Private Members...
		private readonly FastStringWriter m_buffer;
		private readonly CrystalJsonWriter m_writer;
		private readonly Stack<CrystalJsonWriter.State> m_states = new Stack<CrystalJsonWriter.State>();
		#endregion

		#region Constructors...

		public CrystalJsonBuilder(StringBuilder? buffer = null, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null)
		{
			m_buffer = new FastStringWriter(buffer ?? new StringBuilder(512));
			m_writer = new CrystalJsonWriter(m_buffer, settings, customResolver);
		}

		#endregion

		#region Public Properties...

		public CrystalJsonWriter Writer => m_writer;

		public TextWriter BaseWriter => m_buffer;

		public StringBuilder Buffer => m_buffer.GetStringBuilder();

		#endregion

		#region Public Methods...

		public void BeginObject()
		{
			m_states.Push(m_writer.BeginObject());
		}

		public void EndObject()
		{
			if (m_states.Count == 0) throw new InvalidOperationException();
			m_writer.EndObject(m_states.Pop());
		}

		public void BeginArray()
		{
			m_states.Push(m_writer.BeginObject());
		}

		public void EndArray()
		{
			if (m_states.Count == 0) throw new InvalidOperationException();
			m_writer.EndArray(m_states.Pop());
		}

		#region Add ("attr":"value")

		public void AddNull(string attribute)
		{
			m_writer.WriteField(attribute, default(string));
		}

		public void Add(string attribute, string? value)
		{
			m_writer.WriteField(attribute, value);
		}

		public void Add(string attribute, bool value)
		{
			m_writer.WriteField(attribute, value);
		}

		public void Add(string attribute, int value)
		{
			m_writer.WriteField(attribute, value);
		}

		public void Add(string attribute, long value)
		{
			m_writer.WriteField(attribute, value);
		}

		public void Add(string attribute, float value)
		{
			m_writer.WriteField(attribute, value);
		}

		public void Add(string attribute, double value)
		{
			m_writer.WriteField(attribute, value);
		}

		public void Add(string attribute, DateTime value)
		{
			m_writer.WriteField(attribute, value);
		}

		public void Add(string attribute, Guid value)
		{
			m_writer.WriteField(attribute, value);
		}

		public void Add(string attribute, TimeSpan value)
		{
			m_writer.WriteField(attribute, value);
		}

		#endregion

		public override string ToString()
		{
			// ferme automatiquement les objets ouverts (DANGEREUX!)
			while (m_states.Count > 0) EndObject();
			return m_buffer.ToString();
		}

		#endregion

	}

}
