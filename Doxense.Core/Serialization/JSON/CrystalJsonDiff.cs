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
	using System.Collections.Generic;
	using System.Linq;

	public static class CrystalJsonDiff
	{

		internal static class Tokens
		{
			public const string Add = "add";
			public const string Append = "append";
			public const string Clear = "clear";
			public const string ClearMany = "clearMany";
			public const string Patch = "patch";
			public const string Remove = "remove";
			public const string RemoveAll = "removeAll";
			public const string RemoveMany = "removeMany";
			public const string Replace = "replace";
			public const string Set = "set";
			public const string Truncate = "truncate";
		}

		internal static class Commands
		{
			public static readonly JsonValue Add = Tokens.Add;
			public static readonly JsonValue Append = Tokens.Append;
			public static readonly JsonValue Clear = Tokens.Clear;
			public static readonly JsonValue ClearMany = Tokens.ClearMany;
			public static readonly JsonValue Patch = Tokens.Patch;
			public static readonly JsonValue Remove = Tokens.Remove;
			public static readonly JsonValue RemoveAll = Tokens.RemoveAll;
			public static readonly JsonValue RemoveMany = Tokens.RemoveMany;
			public static readonly JsonValue Replace = Tokens.Replace;
			public static readonly JsonValue Set = Tokens.Set;
			public static readonly JsonValue Truncate = Tokens.Truncate;
		}

		#region Structural Diff...

		// compare deux objets, en retournant un objet qui a plus ou moins la même forme, contenant pour chaque champs une description de la différence observée...

		public static JsonValue? Diff(JsonValue left, JsonValue right)
		{
			var comparer = JsonValueComparer.Default;
			if (left.Type != right.Type) ThrowHelper.ThrowArgumentException(nameof(right), "Both items must be of the same type");
			if (left is JsonObject lobj) return DiffMap(lobj, (JsonObject) right, comparer);
			if (left is JsonArray larr) return DiffArray(larr, (JsonArray) right, comparer);
			return !comparer.Equals(left, right) ? right : null;
		}

		private static JsonObject? DiffMap(JsonObject left, JsonObject right, IEqualityComparer<JsonValue> comparer)
		{
			// return la liste des champs de lefts qui ont été modifiés pour arriver jusqu'à right

			var diff = JsonObject.Create();

			var inRight = right.Copy(deep: false);

			foreach(var kvp in left)
			{
				if (!inRight.Remove(kvp.Key, out var val))
				{ // removed from right
					diff[kvp.Key] = JsonObject.Create("_remove", kvp.Value);
					continue;
				}

				if (kvp.Value.Type != val.Type)
				{ // type has changed!
					diff[kvp.Key] = JsonObject.Create("_replace", kvp.Value, "_by", val);
				}
				else if (val is JsonObject valObj)
				{
					var obj = DiffMap((JsonObject)kvp.Value, valObj, comparer);
					if (obj != null) diff[kvp.Key] = obj;
				}
				else if (val is JsonArray valArr)
				{
					var arr = DiffArray((JsonArray)kvp.Value, valArr, comparer);
					if (arr != null) diff[kvp.Key] = arr;
				}
				else if (!comparer.Equals(kvp.Value, val))
				{ // changed in right
					diff[kvp.Key] = JsonObject.Create("_update", kvp.Value, "_by", val);
				}
			}

			foreach(var kvp in inRight)
			{
				diff[kvp.Key] = JsonObject.Create("_add", kvp.Value);
			}

			return diff.Count > 0 ? diff : null;
		}

		private static JsonObject? DiffArray(JsonArray left, JsonArray right, IEqualityComparer<JsonValue> comparer)
		{
			int nl = left.Count;
			int nr = right.Count;

			if (nl == 0)
			{
				if (nr > 0) return JsonObject.Create("_set", right);
				return null;
			}
			if (nr == 0)
			{
				if (nl > 0) return JsonObject.Create("_truncate", 0);
				return null;
			}

			// find common prefix (items usually added/removed at the end)
			int n = Math.Min(nl, nr);
			int common = 0;
			while (common < n && comparer.Equals(left[common], right[common])) { ++common; }

			if (common == nl && common == nr)
			{ // both arrays are identical
				return null;
			}

			if (nr > nl)
			{ // more items than before
				if (common > 0)
				{
					var tail = right.GetRange(common);
					if (common == nl)
					{ // all items in left were preserved
						return JsonObject.Create("_append", tail);
					}
					else
					{ // at least one item was added/changed in the array...
						return JsonObject.Create("_truncate", common, "_append", tail);
					}
				}
				else
				{
					return JsonObject.Create("_replace", right);
				}
			}
			else
			{ // less or same number than before
				if (common > 0)
				{
					if (common == nr)
					{ // items were removed at the end
						return JsonObject.Create("_pop", nl - nr);
					}
					else
					{ // items were removed and less were added?
						var tail = right.GetRange(common);
						return JsonObject.Create("_truncate", common, "_append", tail);
					}
				}
				else
				{
					return JsonObject.Create("_replace", right);
				}
			}

		}

		#endregion

		#region Flat Diff...

		// compare deux objets, en retournant un objet qui a plus ou moins la même forme, contenant pour chaque champs une description de la différence observée...

		public static List<(string Path, string Action, JsonValue? Before, JsonValue? After)> FlatDiff(JsonValue left, JsonValue right)
		{
			var res = new List<(string Path, string Action, JsonValue? Before, JsonValue? After)>();
			var comparer = JsonValueComparer.Default;
			if (left.Type != right.Type) ThrowHelper.ThrowArgumentException(nameof(right), "Both items must be of the same type");
			if (left is JsonObject lobj)
			{
				FlatDiffMap(res, "", lobj, (JsonObject) right, comparer);
			}
			else if (left is JsonArray larr)
			{
				FlatDiffArray(res, "", larr, (JsonArray)right, comparer);
			}
			else if (!comparer.Equals(left, right))
			{
				res.Add((string.Empty, "update", left, right));
			}
			return res;
		}

		private static void FlatDiffMap(List<(string Path, string Action, JsonValue? Before, JsonValue? After)> res, string prefix, JsonObject left, JsonObject right, IEqualityComparer<JsonValue> comparer)
		{
			// return la liste des champs de lefts qui ont été modifiés pour arriver jusqu'à right

			var inRight = right.Copy(deep: false);

			foreach(var kvp in left)
			{
				if (!inRight.TryGetValue(kvp.Key, out var val))
				{ // removed from right
					res.Add((prefix + kvp.Key, "remove", kvp.Value, null));
					continue;
				}

				inRight.Remove(kvp.Key);
				if (kvp.Value.Type != val.Type)
				{ // type has changed!

					if (val is JsonArray valr)
					{
						var p = prefix + kvp.Key + "[";
						for (int i = 0; i < valr.Count; i++)
						{
							res.Add(($"{p}{i}]", "add", null, valr[i]));
						}
					}
					else
					{
						res.Add((prefix + kvp.Key, "update", kvp.Value, val));
					}
				}
				else if (val is JsonObject obj)
				{
					FlatDiffMap(res, prefix + kvp.Key + ".", (JsonObject) kvp.Value, obj, comparer);
				}
				else if (val is JsonArray arr)
				{
					FlatDiffArray(res, prefix + kvp.Key + ".", (JsonArray) kvp.Value, arr, comparer);
				}
				else if (!comparer.Equals(kvp.Value, val))
				{ // changed in right
					res.Add((prefix + kvp.Key, "update", kvp.Value, val));
				}
			}

			foreach(var kvp in inRight)
			{
				if (kvp.Value is JsonArray valr)
				{
					var p = prefix + kvp.Key + "[";
					for (int i = 0; i < valr.Count; i++)
					{
						res.Add(($"{p}{i}]", "add", null, valr[i]));
					}
				}
				else
				{
					res.Add((prefix + kvp.Key, "set", null, kvp.Value));
				}
			}

		}

		private static void FlatDiffArray(List<(string Path, string Action, JsonValue? Before, JsonValue? After)> res, string prefix, JsonArray left, JsonArray right, IEqualityComparer<JsonValue> comparer)
		{
			int nl = left.Count;
			int nr = right.Count;

			if (nl == 0)
			{
				if (nr > 0) { res.Add((prefix, "append", null, right)); }
				return;
			}
			if (nr == 0)
			{
				if (nl > 0) { res.Add((prefix, "clear", left, right)); }
				return;
			}

			// find common prefix (items usually added/removed at the end)
			int n = Math.Min(nl, nr);
			int common = 0;
			while (common < n && comparer.Equals(left[common], right[common])) { ++common; }

			if (common == nl && common == nr)
			{ // both arrays are identical
				return;
			}

			if (nr > nl)
			{ // more items than before
				if (common > 0)
				{
					if (common == nl)
					{ // all items in left were preserved
						for (int i = common; i < nr; i++)
						{
							res.Add((prefix + $"[{i}]", "append", null, right[i]));
						}
					}
					else
					{ // at least one item was added/changed in the array...
						for (int i = common; i < nr; i++)
						{
							if (i < nl)
							{
								res.Add((prefix + $"[{i}]", "update", left[i], right[i]));
							}
							else
							{
								res.Add((prefix + $"[{i}]", "append", null, right[i]));
							}
						}
					}
				}
				else
				{
					res.Add((prefix, "replace", left, right));
				}
			}
			else
			{ // less or same number than before
				if (common > 0)
				{
					if (common == nr)
					{ // items were removed at the end
						for (int i = common; i < nl; i++)
						{
							res.Add((prefix + $"[{i}]", "remove", left[i], null));
						}
					}
					else
					{ // items were removed and less were added?
						for (int i = common; i < nl; i++)
						{
							if (i < nl)
							{
								res.Add((prefix + $"[{i}]", "update", left[i], right[i]));
							}
							else
							{
								res.Add((prefix + $"[{i}]", "remove", left[i], null));
							}
						}
					}
				}
				else
				{
					res.Add((prefix, "replace", left, right));
				}
			}

		}

		#endregion

		#region Relative Diff....

		// liste de transactions pour passer d'une version d'un document à une autre

		/// <summary>Génère la liste d'opérations de transformations nécessaires pour, à partir d'un document initial, arriver vers un document final</summary>
		/// <param name="before">Version initiale du document</param>
		/// <param name="after">Version finale du document</param>
		/// <param name="comparer"></param>
		/// <returns>Liste de transformations qui, si appliquées dans l'ordre, produise <paramref name="after"/> en partant de <paramref name="before"/></returns>
		public static JsonArray ComputeDiffSet(JsonObject before, JsonObject after, IEqualityComparer<JsonValue>? comparer = null)
		{
			var ops = JsonArray.Create();
			comparer ??= JsonValueComparer.Default;
			AppendToDiffSet(ops, before, after, comparer);
			return ops;
		}

		internal static void AppendToDiffSet(JsonArray ops, JsonObject before, JsonObject after, IEqualityComparer<JsonValue> comparer)
		{
			Contract.Debug.Requires(ops != null && before != null && after != null && comparer != null);

			if (before.Count == 0)
			{
				if (after.Count > 0)
				{
					// ["replace", OBJ] : completely replace previous document with OBJ
					ops.Add(JsonArray.Create(Commands.Replace, after));
				}
				return;
			}

			if (after.Count == 0)
			{
				if (before.Count > 0)
				{
					// ["clearAll"] : empty all fields in a document
					ops.Add(JsonArray.Create(Commands.RemoveAll));
				}
				return;
			}

			var fromBefore = new HashSet<string>(before.Keys, before.Comparer);
			fromBefore.ExceptWith(after.Keys);
			if (fromBefore.Count > 0)
			{
				// ["remove", PATH] = remove single field from objet
				// ["remove", PATH[]] = remove multiple fields from object

				if (fromBefore.Count == 1)
				{
					ops.Add(JsonArray.Create(Commands.Remove, fromBefore.Single()));
				}
				else
				{
					ops.Add(JsonArray.Create(Commands.Remove, fromBefore.ToJsonArray()));
				}
			}

			foreach(var kvp in after)
			{
				if (!before.TryGetValue(kvp.Key, out var prev))
				{
					// ["add", PATH, VALUE] : add new field "PATH" with value VALUE
					ops.Add(JsonArray.Create(Commands.Add, kvp.Key, kvp.Value));
				}
				else if (prev.Type != kvp.Value.Type)
				{
					if (kvp.Value.IsNull)
					{ // from value null => clear
						// ["clear", "PATH"] = set field PATH to null
						ops.Add(JsonArray.Create(Commands.Clear, kvp.Key));
					}
					else
					{
						// ["set", "PATH", VALUE] = set field PATH to VALUE
						ops.Add(JsonArray.Create(Commands.Set, kvp.Key, kvp.Value));
					}
				}
				else if (prev is JsonObject prevMap)
				{
					var curMap = (JsonObject) kvp.Value;
					//TODO!
					if (prevMap.Count == 0)
					{
						if (curMap.Count > 0)
						{
							ops.Add(JsonArray.Create(Commands.Set, kvp.Key, curMap));
						}
					}
					else if (curMap.Count == 0)
					{
						ops.Add(JsonArray.Create(Commands.Set, kvp.Key, JsonObject.EmptyReadOnly));
					}
					else
					{
						var subOps = JsonArray.Create();
						AppendToDiffSet(subOps, prevMap, curMap, comparer);
						if (subOps.Count > 0)
						{
							ops.Add(JsonArray.Create(Commands.Patch, kvp.Key, subOps));
						}
					}
				}
				else if (prev is JsonArray prevArray)
				{
					var curArray = (JsonArray) kvp.Value;

					if (prevArray.Count == 0)
					{
						if (curArray.Count > 0)
						{
							ops.Add(JsonArray.Create(Commands.Set, kvp.Key, curArray));
						}
					}
					else if (curArray.Count == 0)
					{
						// ["truncate", "PATH", count] = truncate array in PATH to only keep first 'count' items
						ops.Add(JsonArray.Create(Commands.Truncate, kvp.Key, 0));
					}
					else
					{
						int common = 0;
						int n = Math.Min(prevArray.Count, curArray.Count);
						while (common < n && comparer.Equals(prevArray[common], curArray[common])) { ++common; }

						if (common == 0)
						{ // replace
							ops.Add(JsonArray.Create(Commands.Set, kvp.Key, curArray));
						}
						else
						{ // added/remove (TODO:maybe change of single item)
							if (common < prevArray.Count)
							{
								ops.Add(JsonArray.Create(Commands.Truncate, kvp.Key, common));
							}
							if (common < curArray.Count)
							{
								ops.Add(JsonArray.Create(Commands.Append, kvp.Key, curArray.GetRange(common)));
							}
						}
					}
				}
				else
				{ // regular value
					if (!comparer.Equals(prev, kvp.Value))
					{
						// ["set", "PATH", VALUE]
						ops.Add(JsonArray.Create(Commands.Set, kvp.Key, kvp.Value));
					}
				}
			}
		}

		private static string GetFieldPath(JsonArray cmd, int index)
		{
			var op = cmd[index];
			if (op.Type != JsonType.String) throw new FormatException($"Invalid field path '{op}' in '{cmd[0]}' command");
			return op.ToString();
		}

		private static IEnumerable<string> GetFieldsPath(JsonArray cmd, int index)
		{
			var op = cmd[index];
			if (op.Type != JsonType.Array) throw new FormatException($"Invalid fields path '{op}' in '{cmd[0]}' command");
			foreach(var item in (JsonArray)op)
			{
				if (item.Type != JsonType.String) throw new FormatException($"Invalid field path '{item}' in '{cmd[0]}' command");
				yield return item.ToString();
			}
		}

		private static void VerifyCommand(JsonArray cmd, int count)
		{
			Contract.Debug.Requires(cmd != null);
			if (cmd.Count != count) FailInvalidCommandArgumentsCount(cmd, count);
		}

		[ContractAnnotation("=> halt")]
		private static void FailInvalidCommandArgumentsCount(JsonArray cmd, int count)
		{
			throw new FormatException($"Invalid JSON Diff '{cmd[0]}' command: {count} argument(s) required, but found {cmd.Count}.");
		}

		/// <summary>Modifie un objet en lui appliquant une liste de transformations</summary>
		/// <param name="state">Objet correspond à l'état initial, et qui sera modifié pour arriver dans l'état final</param>
		/// <param name="ops">Liste des transformations à appliquer</param>
		public static void ApplyDiffSet(JsonObject state, JsonArray ops)
		{
			Contract.NotNull(state);
			Contract.NotNull(ops);

			for (int i = 0; i < ops.Count;i++)
			{
				if (ops[i] is not JsonArray op) throw new FormatException($"Malformed JSON Diff: array expected at position {i} but was {ops[i].Type}");
				if (op.Count == 0) throw new InvalidOperationException($"Malformed JSON Diff: empty array at position {i}");

				switch (op[0].ToString())
				{
					case Tokens.Patch:
					{
						VerifyCommand(op, 3);
						var path = GetFieldPath(op, 1);
						ApplyDiffSet(state.GetPath(path).AsObject(required: true)!, op.GetArray(2, required: true)!);
						break;
					}
					case Tokens.Replace:
					{
						VerifyCommand(op, 2);
						var obj = op[1].AsObject(required: true)!;
						state.Clear();
						foreach (var kvp in obj)
						{
							state.Add(kvp.Key, kvp.Value);
						}
						break;
					}
					case Tokens.Set:
					{
						VerifyCommand(op, 3);
						string path = GetFieldPath(op, 1);
						state.SetPath(path, op[2]);
						break;
					}
					case Tokens.Add:
					{
						VerifyCommand(op, 3);
						string path = GetFieldPath(op, 1);
						//BUGBUG: AddPath(..) ?
						state.Add(path, op[2]);
						break;
					}
					case Tokens.Clear:
					{
						VerifyCommand(op, 2);
						string path = GetFieldPath(op, 1);
						state.SetPath(path, JsonNull.Null);
						break;
					}
					case Tokens.ClearMany:
					{
						VerifyCommand(op, 2);
						var nil = JsonNull.Null;
						foreach (var path in GetFieldsPath(op, 1))
						{
							state.SetPath(path, nil);
						}
						break;
					}
					case Tokens.Remove:
					{
						VerifyCommand(op, 2);
						string path = GetFieldPath(op, 1);
						//BUGBUG: RemovePath()
						state.Remove(path);
						break;
					}
					case Tokens.RemoveMany:
					{
						VerifyCommand(op, 2);
						foreach (var path in GetFieldsPath(op, 1))
						{
							//BUGBUG: RemovePath()
							state.Remove(path);
						}
						break;
					}
					case Tokens.RemoveAll:
					{
						VerifyCommand(op, 1);
						state.Clear();
						break;
					}
					case Tokens.Truncate:
					{
						VerifyCommand(op, 3);
						string path = GetFieldPath(op, 1);
						int count = op[2].ToInt32();
						var arr = state.GetPath(path).AsArray(required: false);
						if (arr != null)
						{
							state[path] = arr.GetRange(0, count);
						}
						break;
					}
					case Tokens.Append:
					{
						VerifyCommand(op, 3);
						string path = GetFieldPath(op, 1);
						var items = op.GetArray(2, required: true)!;
						var prev = state.GetPath(path).AsArray(required: false);
						if (prev == null)
						{
							state.SetPath(path, items.Copy());
						}
						else
						{
							prev.AddRange(items);
						}
						break;
					}
					default:
					{
						throw new InvalidOperationException($"Invalid JSON Diff: Operation '{op[0]}' not supported at position {i}");
					}
				}

			}

		}

		#endregion

	}

}
