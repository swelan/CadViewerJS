using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CadViewer
{
	/// <summary>
	/// Proxy interface for backend conversion
	/// </summary>
	public class Converter
	{
		public Converter()
		{
		}
		~Converter()
		{
		}

		public FileInfo Input { get; set; } = null;
		public string InputFileName
		{
			get => Input?.FullName;
			set => Input = String.IsNullOrEmpty(value) ? null : new FileInfo(value);
		}
		public FileInfo Output { get; private set; } = null;
		public string OutputFileName
		{
			get => Output?.FullName;
			set => Output = String.IsNullOrEmpty(value) ? null : new FileInfo(value);
		}

		public string OutputFormat { get; set; } = null;

		/// <summary>
		/// Case-insensitive collection for optional command parameters
		/// </summary>
		public Dictionary<string, object> Parameters { get; private set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		public int ExitCode { get; private set; } = 0;
		public Exception LastError { get; private set; } = null;
		/// <summary>
		/// Reference: ax2020 executable help; the following parameters may not be directly supplied from the client
		/// </summary>
		private static readonly string[] forbidden_parameters = { "f", "i", "o", "id", "od", "sub", "xpath", "log", "lpath", "licpath", "licensepath", "fontpath", "fpath", "xp" };
		
		/// <summary>
		/// Validate parameter names
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
		private IEnumerable<KeyValuePair<string, object>> GetExeParameters()
		{
			//
			// Return a virtual, enumerable list of valid parameters, with appended server-provided values
			// Order: -i -o -f -lpath -xpath -fpath <additional parameters in no particular order>
			//
			return new List<KeyValuePair<string, object>>()
				.Append(new KeyValuePair<string, object>("i", Input?.FullName ?? ""))
				.Append(new KeyValuePair<string, object>("o", Output?.FullName ?? ""))
				.Append(new KeyValuePair<string, object>("f", OutputFormat ?? "pdf"))
				.Append(new KeyValuePair<string, object>("lpath", AppConfig.LicenseLocation))
				.Append(new KeyValuePair<string, object>("xpath", AppConfig.XPathLocation))
				.Append(new KeyValuePair<string, object>("fpath", AppConfig.FontLocation))
				.Concat(Parameters.Where(x => IsValidParameter(x.Key)));
		}

		/// <summary>
		/// Auto-cleanup of the 'Temp' folder. TODO: possibly schedule a task on 'idle' or similar
		/// </summary>
		private void CleanupDumpFolder()
		{
			// Remove any files not accessed for randomly 15 min
			Directory.GetFiles(AppConfig.TempFolder)
				.Select(x => new FileInfo(x))
				.Where(x => x.LastAccessTimeUtc < DateTime.UtcNow.AddMinutes(-15))
				.ToList()
				.ForEach(x => {
					try
					{
						x.Delete();
					} 
					catch (Exception)
					{
						// Don't care
					}
				});
		}

		public bool Execute(Action<FileInfo, int, Exception> Callback = null)
		{
			try
			{
				return ExecuteInternal();
			}
			catch (Exception e)
			{
				LastError = e;
			}
			finally
			{
				//
				// If provided invoke the callback with the result
				//
				try
				{
					Callback?.Invoke(Output, ExitCode, LastError);
				}
				catch (Exception)
				{
					// Ignore, handled by callback
				}
			}
			return false;
		}

		/// <summary>
		/// Orchestrate the conversion process by passing validated commandline-parameters to the configured backend service
		/// </summary>
		/// <param name="Callback"></param>
		/// <param name="writer"></param>
		/// <returns></returns>
		private bool ExecuteInternal()
		{
			ExitCode = 0;
			LastError = null;
			CleanupDumpFolder();

			Input?.Refresh();
			if (!(Input?.Exists ?? false)) throw new FileNotFoundException("Invalid input file", Input?.FullName);

			OutputFileName = TempFile.GetRandomFileName(OutputFormat);

			//
			// Generate a list of command line arguments. Parameter names are validated and values are properly escaped
			//
			var parameters = GetExeParameters().Select(x => {
				if (null == x.Value) return $"-{x.Key}"; // -switch
				return $"-{x.Key}=\"{Util.EscapeCommandLineParameter(x.Value)}\""; // -key=value
			});

			var executable = new FileInfo(AppConfig.ExecutablePath);
			if (!executable.Exists) throw new FileNotFoundException("Invalid executable path", executable.FullName);

			var processInfo = new ProcessStartInfo()
			{
				FileName = executable.FullName,
				Arguments = String.Join(" ", parameters),
				CreateNoWindow = true,
				UseShellExecute = false,
				WorkingDirectory = executable.DirectoryName,
				RedirectStandardError = false,
				RedirectStandardOutput = false,
			};

			using (var process = Process.Start(processInfo))
			{
				process.WaitForExit();
				ExitCode = process.ExitCode;
			}

			//
			// Refresh the output information to reflect its existence
			//
			Output?.Refresh();
			return (Output?.Exists ?? false);
		}
	}
}
