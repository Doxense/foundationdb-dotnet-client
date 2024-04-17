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
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;

	public enum JPathToken
	{
		/// <summary>Fin du path</summary>
		End = 0,
		Identifier,
		ObjectAccess,
		ArrayIndex,

		///// <summary>Nom d'un champ qui sera un objet ("Foo.")</summary>
		//SubObject,
		///// <summary>Nom d'un champ qui sera une array ("Foo[")</summary>
		//SubArray,
		///// <summary>Nom de champ terminal (dernier élément d'un path)</summary>
		//Identifier,
		///// <summary>Indexer dans une array ("[123]")</summary>
		//ArrayIndex,
	}

	/// <summary>Structure utilisée pour parser un JPath (ex: "foo.bar[1].baz") en réduisant le nombre d'allocations nécessaires</summary>
	[DebuggerDisplay("Path={Path}, Offset={Offset}, Cursor={Cursor}, IndexOrSize={IndexOrSize}")]
	[Obsolete("Use JsonPath instead")]
	public struct JPathTokenizer
	{

		/// <summary>Valeur du JPath</summary>
		public readonly string Path;

		/// <summary>Position de début du token courrant</summary>
		public int Offset;
		/// <summary>Position juste après la fin du token courrant</summary>
		public int Cursor;
		/// <summary>Contient la valeur du dernier array index parsé, le nombre de caractère du dernier nom parsé, ou -1 si on est a la fin du path</summary>
		public int IndexOrSize;
		public bool IndexFromEnd;

		public JPathTokenizer(string path)
		{
			Contract.Debug.Requires(path != null);
			this.Path = path;
			this.Offset = 0;
			this.Cursor = 0;
			this.IndexOrSize = 0;
			this.IndexFromEnd = false;
		}

		/// <summary>Retourne la valeur du token actuel, s'il est de type nom de champ</summary>
		public string GetIdentifierName()
		{
			var path = this.Path;
			int offset = this.Offset;

			if (offset >= path.Length) throw FailReachedEndOfString();
			Contract.Debug.Assert(path[offset] != '[', "The current token is not an identifier name");
			int count = this.IndexOrSize;
			Contract.Debug.Assert(!this.IndexFromEnd);

			// optimisation pour les cas où le path est le nom direct d'un champ
			if (offset == 0 && count == path.Length) return path;

			// découpe le token
			return path.Substring(offset, count);
		}

		/// <summary>Retourne la valeur du token actuel, s'il est de type index dans une array</summary>
		/// <returns></returns>
		public Index GetArrayIndex()
		{
			if (this.Offset >= this.Path.Length) throw FailReachedEndOfString();
			Contract.Debug.Assert(this.Path[this.Offset] == '[', "The current token is not an array index");
			return new Index(this.IndexOrSize, this.IndexFromEnd);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailReachedEndOfString()
		{
			return ThrowHelper.InvalidOperationException("JPath tokenizer has already reached past the end.");
		}

		/// <summary>Indique s'il y a encore des tokens dans le path</summary>
		public bool HasMore => this.Cursor < this.Path.Length;

		/// <summary>Retourne le segment de texte correspondant au dernier token parsé (pour formatter un message d'erreur, par exemple)</summary>
		public string GetSourceToken()
		{
			if (this.Offset >= this.Path.Length) return String.Empty;
			if (this.Offset == 0 && this.Cursor == this.Path.Length) return this.Path;
			return this.Path.Substring(this.Offset, this.Cursor - this.Offset);
		}

		/// <summary>Lit le prochain token dans le path</summary>
		/// <returns>Type du token</returns>
		/// <remarks>Retourne <see cref="JPathToken.End"/> si on est arrivé en fin de stream. Un nouvel appel provoquera une exception</remarks>
		public JPathToken ReadNext()
		{
			var pos = this.Cursor;
			var path = this.Path;
			var end = path.Length;
			if (pos >= end)
			{
				if (this.IndexOrSize < 0)
				{
					throw ThrowHelper.InvalidOperationException("Called JPathTokenizer.MoveNext() too many times.");
				}

				this.Offset = end;
				this.IndexOrSize = -1;
				this.IndexFromEnd = false;
				return JPathToken.End;
			}

			int start = pos;
			this.Offset = start;
			char c = path[start];

			if (c == '.')
			{
				++pos;
				this.Cursor = pos;
				this.IndexOrSize = 0;
				this.IndexFromEnd = false;
				return JPathToken.ObjectAccess;
			}

			if (c == '[')
			{ // array_index ::= '[' [0-9]+ ']'
				++pos;
				int? index = null;
				bool fromEnd = false;

				while (pos < end)
				{
					c = path[pos++];
					if (c == ']')
					{
						if (index == null) break; // => fail
						this.IndexOrSize = index.Value;
						this.IndexFromEnd = fromEnd;
						this.Cursor = pos;
						return JPathToken.ArrayIndex;
					}

					if (c == '^')
					{
						if (index != null) break; // => fail
						fromEnd = true;
						continue;
					}
					if (c is < '0' or > '9')
					{
						break; // => fail
					}
					index = checked((index ?? 0) * 10 + (c - 48));
				}

				throw ThrowHelper.FormatException($"Invalid JPath array index at offset {start}: '{path}'");
			}

			while (pos < end)
			{
				c = path[pos];
				if (c is '.' or '[')
				{
					break;
				}
				++pos;
			}

			this.Cursor = pos;
			this.IndexOrSize = pos - start;
			return JPathToken.Identifier;
		}

	}


}
