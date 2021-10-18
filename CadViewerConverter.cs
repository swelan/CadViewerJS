using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
namespace CadViewer
{
	// TODO: find out how to typedef across implementation files
	using ConverterCallback = Action<FileInfo, int, Exception>;
	using ConverterParameters = Dictionary<string, object>;
	using ConverterParameter = KeyValuePair<string, object>;

	/// <summary>
	/// Concrete CadViewerConverter; implements Converter and provides specialization for the
	/// CadViewer "AX2020" backend executable.
	/// </summary>
	public class CadViewerConverter : Converter
	{
		public CadViewerConverter()
		{
		}
		~CadViewerConverter()
		{
		}

		/// <summary>
		/// Reference: ax2020 executable help; the following parameters may not be directly supplied from the client
		/// </summary>
		private static readonly string[] forbidden_parameters = { "f", "i", "o", "id", "od", "sub", "xpath", "log", "lpath", "licpath", "licensepath", "fontpath", "fpath", "xp" };

		/// <summary>
		/// Validate parameter names for the CadViewer executable
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		private static bool IsValidParameter(string key)
		{
			if (String.IsNullOrWhiteSpace(key)) return false;
			if (key.Any(c => (c >= 128) || !Char.IsLetterOrDigit(c))) return false;
			if (forbidden_parameters.Contains(key, StringComparer.OrdinalIgnoreCase)) return false;
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
				.Append(new ConverterParameter("i", Input?.FullName ?? ""))
				.Append(new ConverterParameter("o", Output?.FullName ?? ""))
				.Append(new ConverterParameter("f", OutputFormat ?? "pdf"))
				.Append(new ConverterParameter("lpath", AppConfig.CVJS_LicenseLocation))
				.Append(new ConverterParameter("xpath", AppConfig.CVJS_XPathLocation))
				.Append(new ConverterParameter("fpath", AppConfig.CVJS_FontLocation))
				.Concat(Parameters.Where(x => IsValidParameter(x.Key)));
		}


		/// <summary>
		/// Orchestrate the conversion process by passing validated commandline-parameters to the configured backend service
		/// </summary>
		/// <param name="Callback"></param>
		/// <param name="writer"></param>
		/// <returns></returns>
		protected override async Task<bool> ExecuteInternal()
		{
			//
			// Generate a list of command line arguments. Parameter names are validated and values are properly escaped
			//
			var parameters = GetExeParameters().Select(x => {
				if (String.IsNullOrEmpty(x.Value?.ToString())) return $"{Util.EscapeCommandLineParameter($"-{x.Key}")}"; // -switch
				return $"{Util.EscapeCommandLineParameter($"-{x.Key}")}={Util.EscapeCommandLineParameter(x.Value)}"; // -key=value
			});

			var executable = new FileInfo(AppConfig.CVJS_ExecutablePath);
			var result = await Util.StartProcessAsync(
				Executable: executable, 
				Arguments: parameters, 
				TimeoutMs: AppConfig.ExecutableTimeoutMs,
				RedirectStandardOutput: AppConfig.IsDebug,
				RedirectStandardError: AppConfig.IsDebug
			);

			if (result.ExitCode.HasValue) ExitCode = result.ExitCode.Value;
			
			if (AppConfig.IsDebug)
			{
				File.WriteAllLines(
					Path.Combine(AppConfig.TempFolder, "cadviewer-output.txt"),
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
			}
			
			//
			// Refresh the output information to reflect its existence
			//
			Output?.Refresh();
			return (Output?.Exists ?? false);
		}
	}
}
