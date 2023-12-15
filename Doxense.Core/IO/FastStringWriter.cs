#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.IO
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Runtime;
	using System.Text;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization;

	/// <summary>Version "rapide" de StringWriter, moins secure que StringWriter, adaptée dans des scenario spécifique (parsers, ...)</summary>
	/// <remarks>Il est préférable d'éviter d'exposer cette classe a des consommateurs externes!</remarks>
	public sealed class FastStringWriter : TextWriter
	{
		// Version "bare metal" de StringWriter qui:
		// * Désactive les tests d'ouverture/fermeture du stream (ie: on peut écrire après Dispose!)
		// * Optimise les cas les plus fréquents dans un parseur (string, nombres, ...)
		// * Toute les opérations sont en InvariantCulture
		// * Si possible, vise l'inlining du code

		// Dans l'absolu, c'est les méthodes du StringBuilder qui feront le check des paramètres

		private readonly StringBuilder m_buffer;

		private static volatile UnicodeEncoding? s_encoding;

		#region Constructors...

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public FastStringWriter()
			: this(new StringBuilder())
		{ }

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public FastStringWriter(int capacity)
			: this(new StringBuilder(capacity))
		{ }

		public FastStringWriter(StringBuilder buffer)
			 : base(CultureInfo.InvariantCulture)
		{
			Contract.NotNull(buffer);

			m_buffer = buffer;
		}

		#endregion

		/// <summary>Retourne le buffer utilisé par ce writer</summary>
		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public StringBuilder GetStringBuilder()
		{
			return m_buffer;
		}

		/// <summary>Retourne le contenu du buffer sous forme de chaîne</summary>
		/// <returns>Texte contenu dans le buffer</returns>
		/// <remarks>Attention, il faut éviter d'appeler ToString() puis de continuer a écrire des données, car cela provoque des réallocation mémoire au niveau du StringBuilder interne !</remarks>
		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override string ToString()
		{
			return m_buffer.ToString();
		}

		/// <summary>Retourne les caractères contenus dans le buffer</summary>
		/// <returns></returns>
		public char[] ToCharArray()
		{
			char[] data = new char[m_buffer.Length];
			m_buffer.CopyTo(0, data, 0, m_buffer.Length);
			return data;
		}

		/// <summary>Retourne le contenu du stream sous forme binaire</summary>
		/// <param name="encoding">Encoding utilisé (ex: Encoding.UTF8)</param>
		/// <returns>Octets correspondant au contenu du buffer</returns>
		public byte[] GetBytes(Encoding encoding)
		{
			return encoding.GetBytes(m_buffer.ToString());
		}

		/// <summary>Copie le contenu de ce stream vers un TextWriter</summary>
		/// <param name="output">Writer dans lequel écrire le contenu de ce stream</param>
		/// <param name="buffer">Buffer de caractère utilisé pour la copie (ou null)</param>
		/// <remarks>Effectue une copie "optimisée" en évitant d'allouer la string du StringBuilder</remarks>
		public void CopyTo(TextWriter output, char[]? buffer = null)
		{
			Contract.NotNull(output);

			if (m_buffer.Length == 0) return;

			buffer ??= new char[Math.Min(m_buffer.Length, 0x400)];
			if (buffer.Length == 0) throw new ArgumentException("Buffer cannot be empty", nameof(buffer));

			int remaining = m_buffer.Length;
			int p = 0;
			while (remaining > 0)
			{
				int n = Math.Min(remaining, buffer.Length);
				m_buffer.CopyTo(p, buffer, 0, remaining);
				output.Write(buffer, 0, n);
				p += n;
				remaining -= n;
			}
		}

		#region TextWriter Implementation...

		public override Encoding Encoding
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get { return s_encoding ??= new UnicodeEncoding(false, false); }
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void Close()
		{
			// la version initiale fait un GC.SuppressFinalize(this) qui est inutile ici
			this.Dispose(true);
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void Write(char value)
		{
			m_buffer.Append(value);
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void Write(string value)
		{
			m_buffer.Append(value);
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void Write(char[] value)
		{
			m_buffer.Append(value);
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void Write(char[] buffer, int index, int count)
		{
			m_buffer.Append(buffer, index, count);
		}

		public override void Write(int value)
		{
			m_buffer.Append(StringConverters.ToString(value));
		}

		public override void Write(long value)
		{
			m_buffer.Append(StringConverters.ToString(value));
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void WriteLine(string value)
		{
			m_buffer.AppendLine(value);
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void WriteLine()
		{
			m_buffer.AppendLine();
		}

		#endregion

		#region Async Implementation...

		// Note: les version Async sont overridable uniquement à partir de .NET 4.5 !
		public override Task FlushAsync()
		{
			return Task.CompletedTask;
		}

		public override Task WriteAsync(string value)
		{
			m_buffer.Append(value);
			return Task.CompletedTask;
		}

		public override Task WriteAsync(char value)
		{
			m_buffer.Append(value);
			return Task.CompletedTask;
		}

		public override Task WriteAsync(char[] value, int index, int count)
		{
			m_buffer.Append(value, index, count);
			return Task.CompletedTask;
		}

		#endregion

	}

}
