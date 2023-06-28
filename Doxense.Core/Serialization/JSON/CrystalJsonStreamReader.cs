#region Copyright Doxense 2014-2021
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.IO;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Classe capable de dé-sérialiser des fragments de JSON, en mode stream</summary>
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
					this.Tokenizer.Source.Reader?.Dispose();
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
