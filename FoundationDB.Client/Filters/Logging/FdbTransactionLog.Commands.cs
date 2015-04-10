#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Filters.Logging
{
	using FoundationDB.Async;
	using FoundationDB.Client;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Text;
	using System.Threading.Tasks;
	using Layers.Directories;

	public partial class FdbTransactionLog
	{

		/// <summary>Base class of all types of operations performed on a transaction</summary>
		[DebuggerDisplay("{ToString()}")]
		public abstract class Command
		{

			/// <summary>Return the type of operation</summary>
			public abstract Operation Op { get; }

			/// <summary>If true, the operation was executed in Snapshot mode</summary>
			public bool Snapshot { get; internal set; }

			/// <summary>Return the step number of this command</summary>
			/// <remarks>All commands with the same step number where started in parallel</remarks>
			public int Step { get; internal set; }

			/// <summary>Return the end step number of this command</summary>
			public int EndStep { get; internal set; }

			/// <summary>Number of ticks, since the start of the transaction, when the operation was started</summary>
			public TimeSpan StartOffset { get; internal set; }

			/// <summary>Number of ticks, since the start of the transaction, when the operation completed (or null if it did not complete)</summary>
			public TimeSpan? EndOffset { get; internal set; }

			/// <summary>Exception thrown by this operation</summary>
			public Exception Error { get; internal set; }

			/// <summary>Total size (in bytes) of the arguments</summary>
			/// <remarks>For selectors, only include the size of the keys</remarks>
			public virtual int? ArgumentBytes { get { return default(int?); } }

			/// <summary>Total size (in bytes) of the result, or null if this operation does not produce a result</summary>
			/// <remarks>Includes the keys and values for range reads</remarks>
			public virtual int? ResultBytes { get { return default(int?); } }

			/// <summary>Id of the thread that started the command</summary>
			public int ThreadId { get; internal set; }

			/// <summary>StackTrace of the method that started this operation</summary>
			/// <remarks>Only if the <see cref="FdbLoggingOptions.RecordOperationStackTrace"/> option is set</remarks>
			public StackTrace CallSite { get; internal set; }

			/// <summary>Total duration of the command, or TimeSpan.Zero if the command is not yet completed</summary>
			public TimeSpan Duration
			{
				get
				{
					var start = this.StartOffset;
					var end = this.EndOffset;
					return start == TimeSpan.Zero || !end.HasValue ? TimeSpan.Zero : (end.Value - start);
				}
			}

			/// <summary>Returns a formatted representation of the arguments, for logging purpose</summary>
			public virtual string GetArguments(KeyResolver resolver)
			{
				return String.Empty;
			}

			/// <summary>Returns a formatted representation of the results, for logging purpose</summary>
			public virtual string GetResult(KeyResolver resolver)
			{
				if (this.Error != null)
				{
					var fdbEx = this.Error as FdbException;
					if (fdbEx != null) return "[" + fdbEx.Code.ToString() + "] " + fdbEx.Message;
					return "[" + this.Error.GetType().Name + "] " + this.Error.Message;
				}
				return String.Empty;
			}

			/// <summary>Return the mode of the operation (Read, Write, Metadata, Watch, ...)</summary>
			public virtual Mode Mode
			{
				get
				{
					switch (this.Op)
					{
						case Operation.Invalid:
							return FdbTransactionLog.Mode.Invalid;

						case Operation.Set:
						case Operation.Clear:
						case Operation.ClearRange:
						case Operation.Atomic:
							return FdbTransactionLog.Mode.Write;

						case Operation.Get:
						case Operation.GetKey:
						case Operation.GetValues:
						case Operation.GetKeys:
						case Operation.GetRange:
							return FdbTransactionLog.Mode.Read;

						case Operation.GetReadVersion:
						case Operation.Reset:
						case Operation.Cancel:
						case Operation.AddConflictRange:
						case Operation.Commit:
						case Operation.OnError:
						case Operation.SetOption:
							return FdbTransactionLog.Mode.Meta;

						case Operation.Watch:
							return FdbTransactionLog.Mode.Watch;

						case Operation.Log:
							return FdbTransactionLog.Mode.Annotation;

						default:
						{ 
#if DEBUG
							//FIXME: we probably forgot to add a case for a new type of command !
							if (Debugger.IsAttached) Debugger.Break();
#endif
							return FdbTransactionLog.Mode.Invalid;
						}
					}
				}
			}

			/// <summary>Returns the short version of the command name (up to two characters)</summary>
			public virtual string ShortName
			{
				get
				{
					switch (this.Op)
					{
						case Operation.Commit: return "Co";
						case Operation.Reset: return "Rz";
						case Operation.OnError: return "Er";
						case Operation.GetReadVersion: return "rv";
						case Operation.SetOption: return "op";

						case Operation.Get: return "G ";
						case Operation.GetValues: return "G*";
						case Operation.GetKey: return "K ";
						case Operation.GetKeys: return "K*";
						case Operation.GetRange: return "R ";
						case Operation.Set: return "s ";
						case Operation.Clear: return "c ";
						case Operation.ClearRange: return "cr";
						case Operation.Atomic: return "a ";

						case Operation.Log: return "//";
						case Operation.Watch: return "W ";
					}
					return "??";
				}
			}

			public override sealed string ToString()
			{
				return ToString(null);
			}

			public virtual string ToString(KeyResolver resolver)
			{
				resolver = resolver ?? KeyResolver.Default;
				var arg = this.GetArguments(resolver);
				var res = this.GetResult(resolver);
				var sb = new StringBuilder(255);
				if (this.Snapshot) sb.Append("Snapshot.");
				sb.Append(this.Op.ToString());
				if (!string.IsNullOrEmpty(arg)) sb.Append(' ').Append(arg);
				if (!string.IsNullOrEmpty(res)) sb.Append(" => ").Append(res);
				return sb.ToString();
			}

			protected virtual string ResolveKey(Slice key, Func<Slice, string> resolver)
			{
				if (resolver == null)
				{
					return FdbKey.Dump(key);
				}
				return resolver(key);
			}

		}

		/// <summary>Base class of all types of operations performed on a transaction, that return a result</summary>
		public abstract class Command<TResult> : Command
		{
			private const int MAX_LENGTH = 160;

			/// <summary>Optional result of the operation</summary>
			public Maybe<TResult> Result { get; internal set; }

			public override string GetResult(KeyResolver resolver)
			{
				if (this.Error != null) return base.GetResult(resolver);

				if (this.Result.HasFailed) return "<error>";
				if (!this.Result.HasValue) return "<n/a>";
				if (this.Result.Value == null) return "<null>";

				string res = Dump(this.Result.Value);
				if (res.Length > MAX_LENGTH) res = res.Substring(0, MAX_LENGTH / 2) + "..." + res.Substring(res.Length - (MAX_LENGTH / 2), MAX_LENGTH / 2);
				return res;
			}

			protected virtual string Dump(TResult value)
			{
				return value.ToString();
			}
		}

		public class KeyResolver
		{

			public static readonly KeyResolver Default = new KeyResolver();

			public virtual string Resolve(Slice key)
			{
				return FdbKey.PrettyPrint(key, FdbKey.PrettyPrintMode.Single);
			}

			public virtual string ResolveBegin(Slice key)
			{
				return FdbKey.PrettyPrint(key, FdbKey.PrettyPrintMode.Begin);
			}

			public virtual string ResolveEnd(Slice key)
			{
				return FdbKey.PrettyPrint(key, FdbKey.PrettyPrintMode.End);
			}

		}

		/// <summary>Key resolver which replace key prefixes by the name of their enclosing directory subspace</summary>
		public class DirectoryKeyResolver : KeyResolver
		{

			public readonly Slice[] Prefixes;
			public readonly string[] Paths;

			public DirectoryKeyResolver(Dictionary<Slice, string> knownSubspaces)
			{
				var prefixes = new Slice[knownSubspaces.Count];
				var paths = new string[knownSubspaces.Count];
				int p = 0;
				foreach (var kv in knownSubspaces)
				{
					prefixes[p] = kv.Key;
					paths[p] = kv.Value;
					p++;
				}

				Array.Sort(prefixes, paths, SliceComparer.Default);
				this.Prefixes = prefixes;
				this.Paths = paths;
			}

			/// <summary>Create a key resolver using the content of a DirectoryLayer as the map</summary>
			/// <returns>Resolver that replace each directory prefix by its name</returns>
			public static async Task<DirectoryKeyResolver> BuildFromDirectoryLayer(IFdbReadOnlyTransaction tr, FdbDirectoryLayer directory)
			{
				var location = directory.NodeSubspace.Keys;

				//HACKHACK: for now, we will simply poke inside the node subspace of the directory layer, which is brittle (if the structure changes in future versions!)
				// Entries that correspond to subfolders have the form: NodeSubspace.Pack( (parent_prefix, 0, "child_name") ) = child_prefix
				var keys = await tr.GetRange(location.ToRange()).ToListAsync();

				var map = new Dictionary<Slice, string>(SliceComparer.Default);

				foreach (var entry in keys)
				{
					var t = location.Unpack(entry.Key);
					// look for a tuple of size 3 with 0 as the second element...
					if (t.Count != 3 || t.Get<int>(1) != 0) continue;

					//Slice parent = t.Get<Slice>(0); //TODO: use this to construct the full materialized path of this directory? (would need more than one pass)
					string name = t.Get<string>(2);

					map[entry.Value] = name;
				}

				return new DirectoryKeyResolver(map);
			}

			private bool TryLookup(Slice key, out Slice prefix, out string path)
			{
				prefix = default(Slice);
				path = null;

				if (key.IsNullOrEmpty) return false;

				int p = Array.BinarySearch(this.Prefixes, key, SliceComparer.Default);
				if (p >= 0)
				{ // direct match!
					prefix = this.Prefixes[p];
					path = this.Paths[p];
					return true;
				}

				p = ~p;
				if (p > 0)
				{
					// check if the previous prefix matches
					p = p - 1;
					if (key.StartsWith(this.Prefixes[p]))
					{
						prefix = this.Prefixes[p];
						path = this.Paths[p];
						return true;
					}
				}

				return false;
			}

			public override string Resolve(Slice key)
			{
				Slice prefix;
				string path;
				if (!TryLookup(key, out prefix, out path))
				{
					return base.Resolve(key);
				}

				var s = base.Resolve(key.Substring(prefix.Count));
				if (s != null && s.Length >= 3 && s[0] == '(' && s[s.Length - 1] == ')')
				{ // that was a tuple
					return String.Concat("([", path, "], ", s.Substring(1));
				}
				return String.Concat("[", path, "]:", s);
			}
		}

		public sealed class LogCommand : FdbTransactionLog.Command
		{
			public string Message { get; private set; }

			public override FdbTransactionLog.Operation Op
			{
				get { return FdbTransactionLog.Operation.Log; }
			}

			public LogCommand(string message)
			{
				this.Message = message;
			}

			public override string ToString(KeyResolver resolver)
			{
				return "// " + this.Message;
			}
		}

		public sealed class SetOptionCommand : Command
		{
			/// <summary>Option that is set on the transaction</summary>
			public FdbTransactionOption Option { get; private set; }

			/// <summary>Integer value (if not null)</summary>
			public long? IntValue { get; private set; }

			/// <summary>String value (if not null)</summary>
			public string StringValue { get; private set; }

			public override Operation Op { get { return Operation.SetOption; } }

			public SetOptionCommand(FdbTransactionOption option)
			{
				this.Option = option;
			}

			public SetOptionCommand(FdbTransactionOption option, long value)
			{
				this.Option = option;
				this.IntValue = value;
			}

			public SetOptionCommand(FdbTransactionOption option, string value)
			{
				this.Option = option;
				this.StringValue = value;
			}

			public override string GetArguments(KeyResolver resolver)
			{
				if (this.IntValue.HasValue)
				{
					return String.Format("{0} = {1}", this.Option.ToString(), this.IntValue.Value);
				}
				else if (this.StringValue != null)
				{
					return String.Format("{0} = '{1}'", this.Option.ToString(), this.StringValue);
				}
				else
				{
					return this.Option.ToString();
				}
			}
		}

		public sealed class SetCommand : Command
		{
			/// <summary>Key modified in the database</summary>
			public Slice Key { get; private set; }

			/// <summary>Value written to the key</summary>
			public Slice Value { get; private set; }

			public override Operation Op { get { return Operation.Set; } }

			public SetCommand(Slice key, Slice value)
			{
				this.Key = key;
				this.Value = value;
			}

			public override int? ArgumentBytes
			{
				get { return this.Key.Count + this.Value.Count; }
			}

			public override string GetArguments(KeyResolver resolver)
			{
				return String.Concat(resolver.Resolve(this.Key), " = ", this.Value.ToAsciiOrHexaString());
			}

		}

		public sealed class ClearCommand : Command
		{
			/// <summary>Key cleared from the database</summary>
			public Slice Key { get; private set; }

			public override Operation Op { get { return Operation.Clear; } }

			public ClearCommand(Slice key)
			{
				this.Key = key;
			}

			public override int? ArgumentBytes
			{
				get { return this.Key.Count; }
			}

			public override string GetArguments(KeyResolver resolver)
			{
				return resolver.Resolve(this.Key);
			}

		}

		public sealed class ClearRangeCommand : Command
		{
			/// <summary>Begin of the range cleared</summary>
			public Slice Begin { get; private set; }

			/// <summary>End of the range cleared</summary>
			public Slice End { get; private set; }

			public override Operation Op { get { return Operation.ClearRange; } }

			public ClearRangeCommand(Slice begin, Slice end)
			{
				this.Begin = begin;
				this.End = end;
			}

			public override int? ArgumentBytes
			{
				get { return this.Begin.Count + this.End.Count; }
			}

			public override string GetArguments(KeyResolver resolver)
			{
				return String.Concat(resolver.ResolveBegin(this.Begin), " <= k < ", resolver.ResolveEnd(this.End));
			}

		}

		public sealed class AtomicCommand : Command
		{
			/// <summary>Type of mutation performed on the key</summary>
			public FdbMutationType Mutation { get; private set; }
			/// <summary>Key modified in the database</summary>
			public Slice Key { get; private set; }
			/// <summary>Parameter depending of the type of mutation</summary>
			public Slice Param { get; private set; }

			public override Operation Op { get { return Operation.Atomic; } }

			public AtomicCommand(Slice key, Slice param, FdbMutationType mutation)
			{
				this.Key = key;
				this.Param = param;
				this.Mutation = mutation;
			}

			public override int? ArgumentBytes
			{
				get { return this.Key.Count + this.Param.Count; }
			}

			public override string GetArguments(KeyResolver resolver)
			{
				return String.Concat(resolver.Resolve(this.Key), " ", this.Mutation.ToString(), " ", this.Param.ToAsciiOrHexaString());
			}

			public override string ToString(KeyResolver resolver)
			{
				resolver = resolver ?? KeyResolver.Default;
				var arg = this.GetArguments(resolver);
				var sb = new StringBuilder();
				if (this.Snapshot) sb.Append("Snapshot.");
				sb.Append("Atomic_").Append(this.Mutation.ToString()).Append(' ').Append(resolver.Resolve(this.Key)).Append(", <").Append(this.Param.ToHexaString(' ')).Append('>');
				return sb.ToString();
			}
		}

		public sealed class AddConflictRangeCommand : Command
		{
			/// <summary>Type of conflict</summary>
			public FdbConflictRangeType Type { get; private set; }
			/// <summary>Begin of the conflict range</summary>
			public Slice Begin { get; private set; }
			/// <summary>End of the conflict range</summary>
			public Slice End { get; private set; }

			public override Operation Op { get { return Operation.AddConflictRange; } }

			public AddConflictRangeCommand(Slice begin, Slice end, FdbConflictRangeType type)
			{
				this.Begin = begin;
				this.End = end;
				this.Type = type;
			}

			public override int? ArgumentBytes
			{
				get { return this.Begin.Count + this.End.Count; }
			}

			public override string GetArguments(KeyResolver resolver)
			{
				return String.Concat(this.Type.ToString(), "! ", resolver.ResolveBegin(this.Begin), " <= k < ", resolver.ResolveEnd(this.End));
			}

		}

		public sealed class GetCommand : Command<Slice>
		{
			/// <summary>Key read from the database</summary>
			public Slice Key { get; private set; }

			public override Operation Op { get { return Operation.Get; } }

			public GetCommand(Slice key)
			{
				this.Key = key;
			}

			public override int? ArgumentBytes
			{
				get { return this.Key.Count; }
			}

			public override int? ResultBytes
			{
				get { return !this.Result.HasValue ? default(int?) : this.Result.Value.Count; }
			}

			public override string GetArguments(KeyResolver resolver)
			{
				return resolver.Resolve(this.Key);
			}

			public override string GetResult(KeyResolver resolver)
			{
				if (this.Result.HasValue)
				{
					if (this.Result.Value.IsNull) return "not_found";
					if (this.Result.Value.IsEmpty) return "''";
				}
				return base.GetResult(resolver);
			}

			protected override string Dump(Slice value)
			{
				return value.ToAsciiOrHexaString();
			}

		}

		public sealed class GetKeyCommand : Command<Slice>
		{
			/// <summary>Selector to a key in the database</summary>
			public FdbKeySelector Selector { get; private set; }

			public override Operation Op { get { return Operation.GetKey; } }

			public GetKeyCommand(FdbKeySelector selector)
			{
				this.Selector = selector;
			}

			public override int? ArgumentBytes
			{
				get { return this.Selector.Key.Count; }
			}

			public override int? ResultBytes
			{
				get { return !this.Result.HasValue ? default(int?) : this.Result.Value.Count; }
			}

			public override string GetArguments(KeyResolver resolver)
			{
				//TODO: use resolver!
				return this.Selector.ToString();
			}

		}

		public sealed class GetValuesCommand : Command<Slice[]>
		{
			/// <summary>List of keys read from the database</summary>
			public Slice[] Keys { get; private set; }

			public override Operation Op { get { return Operation.GetValues; } }

			public GetValuesCommand(Slice[] keys)
			{
				this.Keys = keys;
			}

			public override int? ArgumentBytes
			{
				get
				{
					int sum = 0;
					for (int i = 0; i < this.Keys.Length; i++) sum += this.Keys[i].Count;
					return sum;
				}
			}

			public override int? ResultBytes
			{
				get
				{
					if (!this.Result.HasValue || this.Result.Value == null) return null;
					var array = this.Result.Value;
					int sum = 0;
					for (int i = 0; i < array.Length; i++) sum += array[i].Count;
					return sum;
				}
			}

			public override string GetArguments(KeyResolver resolver)
			{
				string s = String.Concat("[", this.Keys.Length.ToString(), "] {");
				if (this.Keys.Length > 0) s += resolver.Resolve(this.Keys[0]);
				if (this.Keys.Length > 1) s += " ... " + resolver.Resolve(this.Keys[this.Keys.Length - 1]);
				return s + " }";
			}

			public override string GetResult(KeyResolver resolver)
			{
				if (!this.Result.HasValue) return base.GetResult(resolver);
				var res = this.Result.Value;
				string s = String.Concat("[", res.Length.ToString(), "] {");
				if (res.Length > 0) s += res[0].ToAsciiOrHexaString();
				if (res.Length > 1) s += " ... " + res[res.Length - 1].ToAsciiOrHexaString();
				return s + " }";

			}

		}

		public sealed class GetKeysCommand : Command<Slice[]>
		{
			/// <summary>List of selectors looked up in the database</summary>
			public FdbKeySelector[] Selectors { get; private set; }

			public override Operation Op { get { return Operation.GetKeys; } }

			public GetKeysCommand(FdbKeySelector[] selectors)
			{
				this.Selectors = selectors;
			}

			public override int? ArgumentBytes
			{
				get
				{
					int sum = 0;
					for (int i = 0; i < this.Selectors.Length; i++) sum += this.Selectors[i].Key.Count;
					return sum;
				}
			}

			public override int? ResultBytes
			{
				get
				{
					if (!this.Result.HasValue || this.Result.Value == null) return null;
					var array = this.Result.Value;
					int sum = 0;
					for (int i = 0; i < array.Length; i++) sum += array[i].Count;
					return sum;
				}
			}

			public override string GetArguments(KeyResolver resolver)
			{
				string s = String.Concat("[", this.Selectors.Length.ToString(), "] {");
				//TODO: use resolver!
				if (this.Selectors.Length > 0) s += this.Selectors[0].ToString();
				if (this.Selectors.Length > 1) s += " ... " + this.Selectors[this.Selectors.Length - 1].ToString();
				return s + " }";
			}

		}

		public sealed class GetRangeCommand : Command<FdbRangeChunk>
		{
			/// <summary>Selector to the start of the range</summary>
			public FdbKeySelector Begin { get; private set; }
			/// <summary>Selector to the end of the range</summary>
			public FdbKeySelector End { get; private set; }
			/// <summary>Options of the range read</summary>
			public FdbRangeOptions Options { get; private set; }
			/// <summary>Iteration number</summary>
			public int Iteration { get; private set; }

			public override Operation Op { get { return Operation.GetRange; } }

			public GetRangeCommand(FdbKeySelector begin, FdbKeySelector end, FdbRangeOptions options, int iteration)
			{
				this.Begin = begin;
				this.End = end;
				this.Options = options;
				this.Iteration = iteration;
			}

			public override int? ArgumentBytes
			{
				get { return this.Begin.Key.Count + this.End.Key.Count; }
			}

			public override int? ResultBytes
			{
				get
				{
					if (!this.Result.HasValue) return null;
					int sum = 0;
					var chunk = this.Result.Value.Chunk;
					for (int i = 0; i < chunk.Length; i++)
					{
						sum += chunk[i].Key.Count + chunk[i].Value.Count;
					}
					return sum;
				}
			}

			public override string GetArguments(KeyResolver resolver)
			{
				//TODO: use resolver!
				string s = this.Begin.PrettyPrint(FdbKey.PrettyPrintMode.Begin) + " <= k < " + this.End.PrettyPrint(FdbKey.PrettyPrintMode.End);
				if (this.Iteration > 1) s += ", #" + this.Iteration.ToString();
				if (this.Options != null)
				{
					if ((this.Options.Limit ?? 0) > 0) s += ", limit(" + this.Options.Limit + ")";
					if (this.Options.Reverse == true) s += ", reverse";
					if (this.Options.Mode.HasValue) s += ", " + this.Options.Mode;
				}
				return s;
			}

			public override string GetResult(KeyResolver resolver)
			{
				if (this.Result.HasValue)
				{
					string s = String.Format("{0} result(s)", this.Result.Value.Count);
					if (this.Result.Value.HasMore) s += ", has_more";
					return s;
				}
				return base.GetResult(resolver);
			}

		}

		public sealed class GetReadVersionCommand : Command<long>
		{
			public override Operation Op { get { return Operation.GetReadVersion; } }

		}

		public sealed class CancelCommand : Command
		{
			public override Operation Op { get { return Operation.Cancel; } }

		}

		public sealed class ResetCommand : Command
		{
			public override Operation Op { get { return Operation.Reset; } }

		}

		public sealed class CommitCommand : Command
		{
			public override Operation Op { get { return Operation.Commit; } }

		}

		public sealed class OnErrorCommand : Command
		{
			public FdbError Code { get; private set; }

			public override Operation Op { get { return Operation.OnError; } }

			public OnErrorCommand(FdbError code)
			{
				this.Code = code;
			}

			public override string GetArguments(KeyResolver resolver)
			{
				return String.Format(CultureInfo.InvariantCulture, "{0} ({1})", this.Code, (int)this.Code);
			}
		}

		public sealed class WatchCommand : Command
		{
			public Slice Key { get; private set; }

			public override Operation Op
			{
				get { return Operation.Watch; }
			}

			public WatchCommand(Slice key)
			{
				this.Key = key;
			}

			public override int? ArgumentBytes
			{
				get { return this.Key.Count; }
			}

			public override string GetArguments(KeyResolver resolver)
			{
				return resolver.Resolve(this.Key);
			}

		}


	}

}
