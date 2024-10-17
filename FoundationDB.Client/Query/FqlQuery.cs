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

//#define FULL_DEBUG

#if NET8_0_OR_GREATER

namespace FoundationDB.Client
{
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using Doxense.Linq;

	public sealed class FqlQuery : IFqlQuery
	{

		public FqlDirectoryExpression? Directory { get; init; }

		public FqlTupleExpression? Tuple { get; init; }

		public override string ToString()
		{
			string? s = null;

			if (this.Directory != null)
			{
				s += this.Directory.ToString();
			}

			if (this.Tuple != null)
			{
				s += this.Tuple.ToString();
			}

			return s ?? "";
		}

		/// <inheritdoc />
		public bool IsPattern => (this.Directory?.IsPattern ?? false) || (this.Tuple?.IsPattern ?? false);

		/// <inheritdoc />
		public void Explain(TextWriter output, int depth = 0, bool recursive = true)
		{
			string indent = new string('\t', depth) + (depth == 0 ? "" : " -");

			if (!recursive)
			{
				output.WriteLine($"{indent}Query: `{ToString()}`");
				return;
			}

			output.WriteLine($"{indent}Query: `{ToString()}`");
			if (this.Directory != null)
			{
				this.Directory.Explain(output, depth + 1);
			}
			else
			{
				output.WriteLine($"{indent}\t- Directory: <none>");
			}

			if (this.Tuple != null)
			{
				this.Tuple.Explain(output, depth + 1);
			}
			else
			{
				output.WriteLine($"{indent}\t- Tuple: <none>");
			}
		}

		[Conditional("FULL_DEBUG")]
		private static void Kenobi(string message)
		{
#if FULL_DEBUG
			System.Diagnostics.Debug.WriteLine(message);
			Console.WriteLine(message);
#endif
		}

		public IAsyncEnumerable<FdbDirectorySubspace> EnumerateDirectories(IFdbReadOnlyTransaction tr)
		{
			return AsyncEnumerable.Pump<FdbDirectorySubspace>(async (channel) =>
			{
				if (this.Directory == null)
				{
					return;
				}

				FdbDirectorySubspace? subspace;

				if (this.Directory.TryGetPath(out FdbPath path))
				{ // this is a fixed path, ex: "/foo/bar/baz", we can open it directly

					subspace = await tr.Database.DirectoryLayer.TryOpenAsync(tr, path).ConfigureAwait(false);
					if (subspace != null)
					{
						await channel.WriteAsync(subspace).ConfigureAwait(false);
					}
					return;
				}

				List<FdbDirectorySubspace> batch = [ ];
				List<FdbDirectorySubspace> nextBatch = [ ];

				int fromIndex = 0;
				var segments = this.Directory.Segments;

				while (fromIndex < segments.Count)
				{
					Kenobi($"from:{fromIndex}, batch:[{batch.Count}] {{ {string.Join(", ", batch.Select(x => x.Path))} }}");

					var (chunk, nextIndex) = this.Directory.GetFixedPrefix(fromIndex);
					Kenobi($"- chunk: {chunk}, nextIndex={nextIndex}");

					if (fromIndex == 0)
					{
						if (chunk.Count == 0)
						{ // starts immediately with any, ex: "/<>/bar/baz/..."
							chunk = FdbPath.Root;
						}

						Kenobi($"Load first chunk {chunk}...");
						subspace = await tr.Database.DirectoryLayer.TryOpenAsync(tr, chunk).ConfigureAwait(false);
						if (subspace == null)
						{
							Kenobi($"No match for first chunk {chunk}");
							// nothing for the first chunk
							return;
						}
						Kenobi($"Found match for first chunk {chunk} => {subspace}");
						batch.Add(subspace);
					}
					else
					{
						if (batch.Count == 0)
						{
							Kenobi("No more match!");
							return;
						}
					}

					if (nextIndex < segments.Count && segments[nextIndex].IsAny)
					{
						Kenobi($"Next is <>, listing child for {batch.Count} candidates...");

						foreach (var candidate in batch)
						{
							// list its children
							var names = await candidate.ListAsync(tr).ConfigureAwait(false);
							Kenobi($"- found {names.Count} children for {candidate.Path}");
							foreach (var name in names)
							{
								var child = await tr.Database.DirectoryLayer.TryOpenAsync(tr, name).ConfigureAwait(false);
								if (child != null)
								{
									Kenobi($"- queueing {name} for next batch");
									nextBatch.Add(child);
								}
							}
						}
						Kenobi($"- Went from {batch.Count} to {nextBatch.Count} candidates");
					}
					else
					{ // we have to add the chunk to each candidate
						Kenobi($"This is the final chunk, completing {batch.Count} candidates...");
						foreach (var candidate in batch)
						{
							var child = await tr.Database.DirectoryLayer.TryOpenAsync(tr, candidate.Path[chunk]).ConfigureAwait(false);
							if (child != null)
							{
								nextBatch.Add(child);
							}
						}
						Kenobi($"- Went from {batch.Count} to {nextBatch.Count} candidates");
					}

					batch.Clear();
					batch.AddRange(nextBatch);
					nextBatch.Clear();
					fromIndex = nextIndex + 1;
				}

				Kenobi($"Victory! We have {batch.Count} matching directories");

				foreach (var s in batch)
				{
					await channel.WriteAsync(s).ConfigureAwait(false);
				}

			}, tr.Cancellation);
		}
	}

}
#endif
