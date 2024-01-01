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

		public static Task<FdbCliResult> RunFdbCliCommand(string command, string? options, string? clusterFile, TextWriter? log, CancellationToken ct)
		{
			log ??= Console.Out;

			string path = "fdbcli";
			string arguments = string.Format("-C \"{0}\" {2} --exec \"{1}\"", clusterFile, command, options);
			string workingDirectory = Environment.CurrentDirectory;

			var startInfo = new ProcessStartInfo
			{
				FileName = path,
				Arguments = arguments,
				WorkingDirectory = workingDirectory,
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				StandardOutputEncoding = Encoding.Default,
				CreateNoWindow = true
			};
			if (OperatingSystem.IsWindows())
			{
				startInfo.LoadUserProfile = false;
			}

			var proc = new Process()
			{
				StartInfo = startInfo,
			};

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
				var p = (cts.Task.AsyncState as Process)!;
				// on doit appeler WaitForExit() pour etre certain que les stdout et stderr soient bien lus en entier
				p.WaitForExit();
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
