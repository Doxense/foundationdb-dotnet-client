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

	/// <summary>Classe capable de dé-sérialiser des fragments de JSON, en mode stream</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public sealed class CrystalJsonStreamReader : IDisposable //TODO: IAsyncDisposable !
	{

		/// <summary>Lit des fragments JSON depuis un Stream</summary>
		public CrystalJsonStreamReader(Stream input, CrystalJsonSettings? settings, bool ownSource = false)
		{
			Contract.NotNull(input);
			if (!input.CanRead) throw new InvalidOperationException("The input stream must be readable");

			var source = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
			this.OwnSource = ownSource;
			this.Tokenizer = new CrystalJsonTokenizer<JsonTextReader>(new JsonTextReader(source), settings ?? CrystalJsonSettings.Json);
		}

		/// <summary>Lit des fragments JSON depuis un TextReader</summary>
		public CrystalJsonStreamReader(TextReader input, CrystalJsonSettings? settings, bool ownSource = false)
		{
			Contract.NotNull(input);
			this.OwnSource = ownSource;
			this.Tokenizer = new CrystalJsonTokenizer<JsonTextReader>(new JsonTextReader(input), settings ?? CrystalJsonSettings.Json);
		}

		/// <summary>Lit le prochain fragment dans le fichier</summary>
		/// <returns>Fragment suivant, ou null si on est arrivé en fin de fichier</returns>
		[Pure]
		public JsonValue? ReadNextFragment()
		{
			if (this.Disposed) throw FailObjectDisposed();
			return CrystalJsonParser<JsonTextReader>.ParseJsonValue(ref this.Tokenizer);
		}

		/// <summary>Si true, cette instance a déjà été disposée</summary>
		private bool Disposed { get; set; }

		/// <summary>JSON Reader used to read values from the source</summary>
		private CrystalJsonTokenizer<JsonTextReader> Tokenizer;

		/// <summary>Si true, il faut disposer m_stream lorsque cette instance est elle même disposée</summary>
		private bool OwnSource { get; }

		#region IDisposable...

		public void Dispose()
		{
			if (!this.Disposed)
			{
				this.Disposed = true;
				if (this.OwnSource)
				{
					this.Tokenizer.Source.Reader.Dispose();
				}
				this.Tokenizer.Dispose();
			}
		}

		//TODO: DisposeAsync()?
		// => tant que Stream n'implémente pas IAsyncDisposable, on a pas vraiment de plus-value vs Dispose()

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ObjectDisposedException FailObjectDisposed()
		{
			return new ObjectDisposedException(nameof(CrystalJsonStreamReader));
		}

		#endregion

		#region Static Helpers...

		public static CrystalJsonStreamReader Open(string path, CrystalJsonSettings? settings = null)
		{
			Contract.NotNullOrEmpty(path);

			Stream? stream = null;
			CrystalJsonStreamReader? sw = null;
			bool failed = true;
			try
			{
				stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 0x400, FileOptions.SequentialScan);
				sw = new CrystalJsonStreamReader(stream, settings, true);
				failed = false;
				return sw;
			}
			finally
			{
				if (failed)
				{
					sw?.Dispose();
					stream?.Dispose();
				}
			}
		}

		public static CrystalJsonStreamReader Create(Stream stream, CrystalJsonSettings? settings = null, bool ownStream = false)
		{
			Contract.NotNull(stream);

			CrystalJsonStreamReader? sr = null;
			bool failed = true;
			try
			{
				sr = new CrystalJsonStreamReader(stream, settings, ownStream);
				failed = false;
				return sr;
			}
			finally
			{
				if (failed)
				{
					sr?.Dispose();
				}
			}
		}

		#endregion

	}

}
