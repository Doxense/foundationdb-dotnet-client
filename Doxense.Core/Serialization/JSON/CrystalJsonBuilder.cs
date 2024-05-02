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

namespace Doxense.Serialization.Json
{
	using System.IO;
	using System.Text;
	using Doxense.IO;

	/// <summary>Classe capable de générer manuellement un objet JSON simple</summary>
	[PublicAPI]
	public sealed class CrystalJsonBuilder
	{

		#region Private Members...
		private readonly FastStringWriter m_buffer;
		private readonly CrystalJsonWriter m_writer;
		private readonly Stack<CrystalJsonWriter.State> m_states = new Stack<CrystalJsonWriter.State>();
		#endregion

		#region Constructors...

		public CrystalJsonBuilder(StringBuilder? buffer = null, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			m_buffer = new FastStringWriter(buffer ?? new StringBuilder(512));
			m_writer = new CrystalJsonWriter(m_buffer, settings, resolver);
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
