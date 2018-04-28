
namespace FdbShell
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	public static class FdbCliCommands
	{

		public class FdbCliResult
		{
			public int ExitCode { get; set; }
			public string StdOut { get; set; }
			public string StdErr { get; set; }
		}

		public static Task<FdbCliResult> RunFdbCliCommand(string command, string options, string clusterFile, TextWriter log, CancellationToken ct)
		{
			if (log == null) log = Console.Out;

			string path = "fdbcli";
			string arguments = String.Format("-C \"{0}\" {2} --exec \"{1}\"", clusterFile, command, options);
			string workingDirectory = Environment.CurrentDirectory;

			var startInfo = new ProcessStartInfo()
			{
				FileName = path,
				Arguments = arguments,
				WorkingDirectory = workingDirectory,
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
			};
			startInfo.StandardOutputEncoding = Encoding.Default;
			startInfo.LoadUserProfile = false;
			startInfo.CreateNoWindow = true;

			var proc = new Process();
			proc.StartInfo = startInfo;

			var stdOut = new StringBuilder();
			var stdErr = new StringBuilder();

			// récupère le StdOutput
			proc.OutputDataReceived += (sender, e) =>
			{
				if (e.Data != null)
				{
					stdOut.AppendLine(e.Data);
				}
			};
			// récupère le StdError
			startInfo.StandardOutputEncoding = Encoding.Default;
			proc.ErrorDataReceived += (sender, e) =>
			{
				if (e.Data != null)
				{
					stdErr.AppendLine(e.Data);
				}
			};

			var cts = new TaskCompletionSource<FdbCliResult>(proc);

			// Termine la Task lorsque le process se termine
			proc.EnableRaisingEvents = true;
			proc.Exited += (sender, e) =>
			{
				var p = (cts.Task.AsyncState as Process);
				// on doit appeler WaitForExit() pour etre certain que les stdout et stderr soient bien lus en entier
				p.WaitForExit();
				int code = p.ExitCode;
				cts.TrySetResult(new FdbCliResult
				{
					ExitCode = p.ExitCode,
					StdOut = stdOut.ToString(),
					StdErr = stdErr.ToString()
				});
			};

			proc.Start();
			proc.BeginOutputReadLine();
			proc.BeginErrorReadLine();
			return cts.Task;
		}

	}
}
