using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CadViewer
{
	// TODO: find out how to typedef across implementation files
	using ConverterCallback = Action<FileInfo, int, Exception>;
	using ConverterParameters = Dictionary<string, object>;
	using ConverterParameter = KeyValuePair<string, object>;

	public class OfficeConverter : Converter
	{

		/// <summary>
		/// Validate parameter names for the CadViewer executable
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		private static bool IsValidParameter(string key)
		{
			if (String.IsNullOrWhiteSpace(key)) return false;
			if (key.Any(c => (c >= 128) || !Char.IsLetterOrDigit(c) || !Char.Equals(c, '-'))) return false;
			return true;
		}

		/// <summary>
		/// Return a list of server-supplied parameters and filtered safe parameters from the client
		/// </summary>
		/// <returns></returns>
		private IEnumerable<ConverterParameter> GetExeParameters()
		{
			//
			// Return a virtual, enumerable list of valid parameters, with appended server-provided values
			// Order: -i -o -f -lpath -xpath -fpath <additional parameters in no particular order>
			//
			return new List<ConverterParameter>()
				.Append(new ConverterParameter("headless", null))
				.Append(new ConverterParameter("convert-to", "pdf"))
				.Append(new ConverterParameter("outdir", Output?.DirectoryName))
				.Append(new ConverterParameter(null, Input?.FullName));
		}
		protected override async Task<bool> ExecuteInternal()
		{
			OutputFileName = Path.Combine(Output.DirectoryName, $"{Util.GetFileNameWithoutExtension(Input.Name)}.pdf");
			//
			// Generate a list of command line arguments. Parameter names are validated and values are properly escaped
			// NOTE: user_env must be an accessible, local folder. It must not exist, but must be creatable.
			// On first launch, soffice will create a user profile in this folder. This is necessary for the invocation
			// to function properly. It must be given to the executable as a file uri.
			//
			var user_env = AppConfig.GetUri("CadViewer.LibreOffice.Env.UserInstallation", UriKind.Absolute);

			//var home = new DirectoryInfo(user_env.To);
			//if (!home.Exists) throw new FileNotFoundException("")
			var parameters = GetExeParameters().Select(x =>
			{
				if (null == x.Value) return $"--{x.Key}";
				if (null == x.Key) return $"\"{Util.EscapeCommandLineParameter(x.Value)}\"";
				return $"--{x.Key} \"{Util.EscapeCommandLineParameter(x.Value)}\"";
			}).Append($"-env:UserInstallation={user_env.AbsoluteUri}");

			var executable = new FileInfo(Path.Combine(AppConfig.GetLocalPath("CadViewer.LibreOffice.ProgramLocation", UriKind.Absolute), "soffice.com"));
			var result = await Util.StartProcessAsync(
				Executable: executable,
				Arguments: parameters, 
				TimeoutMs: 8000,
				RedirectStandardOutput: true, 
				RedirectStandardError: true
			);

			ExitCode = result.ExitCode.HasValue ? result.ExitCode.Value : 0;

			File.WriteAllLines(
				@"C:\temp\CadViewer\office-output.txt",
				new string[] {
					$"[{DateTime.UtcNow}]:",
					$"{Util.EscapePathComponents(executable.FullName)}",
					String.Join("\n", parameters),
					"\nEXITCODE: " + (result.ExitCode.HasValue ? $"{result.ExitCode.Value}" : "null -- the process timed out"),
					"\n===== STDOUT =====",
					$"{result.StdOut}",
					"\n=== STDERR ===",
					$"{result.StdErr}"
				}
			);

			/*
			File.WriteAllText(@"C:\temp\CadViewer\output.txt", $"[{DateTime.UtcNow}]:\n{Util.EscapePathComponents(executable.FullName)}\n{String.Join("\n", parameters)}\n===============================================\n");
			void OnOutput(object sender, System.Diagnostics.DataReceivedEventArgs args)
			{
				//if (null != args && null != args.Data) sb.AppendLine(args.Data);
				File.AppendAllText(@"C:\temp\cadviewer\output.txt", $"{args.Data}\n");
			}
			File.WriteAllText(@"C:\temp\CadViewer\error.txt", $"[{DateTime.UtcNow}]:\n{Util.EscapePathComponents(executable.FullName)}\n{String.Join("\n", parameters)}\n===============================================\n");
			void OnError(object sender, System.Diagnostics.DataReceivedEventArgs args)
			{
				File.AppendAllText(@"C:\temp\cadviewer\error.txt", $"{args.Data}\n");
			}

			ExitCode = await Util.StartProcessAsync(executable, parameters, OnOutput, OnError);
			*/

			//
			// Refresh the output information to reflect its existence
			//
			Output?.Refresh();
			return (Output?.Exists ?? false);
		}

	}
}
