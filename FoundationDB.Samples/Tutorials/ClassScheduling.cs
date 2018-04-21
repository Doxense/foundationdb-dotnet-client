//TODO: License for samples/tutorials ???

namespace FoundationDB.Samples.Tutorials
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Linq;
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;

	public class ClassScheduling : IAsyncTest
	{

		public ClassScheduling()
		{
			// create a bunch of random classes
			var levels = new[] { "intro", "for dummies", "remedial", "101", "201", "301", "mastery", "lab", "seminar" };
			var types = new[] { "chem", "bio", "cs", "geometry", "calc", "alg", "film", "music", "art", "dance" };
			var times = Enumerable.Range(2, 20).Select(h => h.ToString() + ":00").ToArray();

			this.ClassNames = times
				.SelectMany((h) => types.Select(t => h + " " + t))
				.SelectMany((s) => levels.Select((l) => s + " " + l))
				.ToArray();
		}

		public string[] ClassNames { get; }

		public IDynamicKeySubspace Subspace { get; private set; }

		protected Slice ClassKey(string c)
		{
			return this.Subspace.Keys.Encode("class", c);
		}

		protected Slice AttendsKey(string s, string c)
		{
			return this.Subspace.Keys.Encode("attends", s, c);
		}

		protected KeyRange AttendsKeys(string s)
		{
			return this.Subspace.Keys.ToRange(STuple.Create("attends", s));
		}

		/// <summary>
		/// Setup the initial state of the database
		/// </summary>
		public async Task Init(IFdbDatabase db, CancellationToken ct)
		{
			// open the folder where we will store everything
			this.Subspace = await db.Directory.CreateOrOpenAsync(new [] { "Tutorials", "ClassScheduling" }, ct: ct);

			// clear all previous values
			await db.ClearRangeAsync(this.Subspace, ct);
			// insert all the classes
			await db.WriteAsync((tr) =>
			{
				foreach (var c in this.ClassNames)
				{
					tr.Set(ClassKey(c), Slice.FromAscii("100"));
				}
			}, ct);


		}

		/// <summary>
		/// Returns the list of names of all existing classes
		/// </summary>
		public Task<List<string>> AvailableClasses(IFdbReadOnlyTransaction tr)
		{
			return tr.GetRange(this.Subspace.Keys.ToRange(STuple.Create("class")))
				.Where(kvp => { int _; return Int32.TryParse(kvp.Value.ToAscii(), out _); }) // (step 3)
				.Select(kvp => this.Subspace.Keys.Decode<string>(kvp.Key))
				.ToListAsync();
		}

		/// <summary>
		/// Signup a student to a class
		/// </summary>
		public async Task Signup(IFdbTransaction tr, string s, string c)
		{
			var rec = AttendsKey(s, c);

			if ((await tr.GetAsync(rec)).IsPresent)
			{ // already signed up
				return;
			}
			int seatsLeft = Int32.Parse((await tr.GetAsync(ClassKey(c))).ToAscii());
			if (seatsLeft <= 0)
			{
				throw new InvalidOperationException("No remaining seats");
			}

			var classes = await tr.GetRange(AttendsKeys(s)).ToListAsync();
			if (classes.Count >= 5) throw new InvalidOperationException("Too many classes");

			tr.Set(ClassKey(c), Slice.FromAscii((seatsLeft - 1).ToString()));
			tr.Set(rec, Slice.Empty);
		}

		/// <summary>
		/// Drop a student from a class
		/// </summary>
		public async Task Drop(IFdbTransaction tr, string s, string c)
		{
			var rec = AttendsKey(s, c);
			if ((await tr.GetAsync(rec)).IsNullOrEmpty)
			{ // not taking this class
				return;
			}

			var students = Int32.Parse((await tr.GetAsync(ClassKey(c))).ToAscii());
			tr.Set(ClassKey(c), Slice.FromAscii((students + 1).ToString()));
			tr.Clear(rec);
		}

		/// <summary>
		/// Drop a student from a class, and sign him up to another class
		/// </summary>
		public async Task Switch(IFdbTransaction tr, string s, string oldC, string newC)
		{
			await Drop(tr, s, oldC);
			await Signup(tr, s, newC);
		}

		/// <summary>
		/// Simulate a student that is really indecisive
		/// </summary>
		public async Task IndecisiveStudent(IFdbDatabase db, int id, int ops, CancellationToken ct)
		{
			string student = "s" + id.ToString("D04");
			var allClasses = new List<string>(this.ClassNames);
			var myClasses = new List<string>();

			var rnd = new Random(id * 7);

			for (int i = 0; i < ops && !ct.IsCancellationRequested; i++)
			{
				int classCount = myClasses.Count;

				var moods = new List<string>();
				if (classCount > 0) moods.AddRange(new[] { "drop", "switch" });
				if (classCount < 5) moods.Add("add");
				string mood = moods[rnd.Next(moods.Count)];

				try
				{
					if (allClasses == null)
					{
						allClasses = await db.ReadAsync((tr) => AvailableClasses(tr), ct);
					}

					switch (mood)
					{
						case "add":
						{
							string @class = allClasses[rnd.Next(allClasses.Count)];
							await db.ReadWriteAsync((tr) => Signup(tr, student, @class), ct);
							myClasses.Add(@class);
							break;
						}
						case "drop":
						{
							string @class = allClasses[rnd.Next(allClasses.Count)];
							await db.ReadWriteAsync((tr) => Drop(tr, student, @class), ct);
							myClasses.Remove(@class);
							break;
						}
						case "switch":
						{
							string oldClass = allClasses[rnd.Next(allClasses.Count)];
							string newClass = allClasses[rnd.Next(allClasses.Count)];
							await db.ReadWriteAsync((tr) => Switch(tr, student, oldClass, newClass), ct);
							myClasses.Remove(oldClass);
							myClasses.Add(newClass);
							break;
						}
						default:
						{
							throw new InvalidOperationException("Ooops");
						}
					}
				}
				catch (Exception e)
				{
					if (e is TaskCanceledException || e is OperationCanceledException) throw;
					allClasses = null;
				}
			}

			ct.ThrowIfCancellationRequested();

		}

		#region IAsyncTest...

		public string Name => "ClassScheduling";

		public async Task Run(IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			const int STUDENTS = 10;
			const int OPS_PER_STUDENTS = 10;

			await Init(db, ct);
			log.WriteLine("# Class sheduling test initialized");

			// run multiple students
			var elapsed = await Program.RunConcurrentWorkersAsync(
				STUDENTS,
				(i, _ct) => IndecisiveStudent(db, i, OPS_PER_STUDENTS, _ct),
				ct
			);

			log.WriteLine("# Ran {0} transactions in {1:0.0##} sec", (STUDENTS * OPS_PER_STUDENTS), elapsed.TotalSeconds);
		}

		#endregion

	}
}
