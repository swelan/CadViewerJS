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

		private string _basename = null;
		/// <summary>
		/// Allow external modification of the BaseName part of the OutputFilename
		/// </summary>
		public string OutputBaseName { get => _basename; set => _basename = Util.GetFileNameWithoutExtension(value).OrDefault(null); }
		public string OutputFormat { get; set; } = null;

		private string _action = null;
		/// <summary>
		/// Can be used to indicate the type of operation to perform
		/// </summary>
		public string Action { get => _action; set { _action = value?.Trim().ToLowerInvariant(); } }

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
		/// Execute the converter, optionally with a callback
		/// </summary>
		/// <param name="Callback"></param>
		/// <returns></returns>
		public async Task<bool> Execute(Action<FileInfo, int, Exception> Callback = null)
		{
			try
			{
				return await ExecuteInternal();
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
		private async Task<bool> ExecuteInternal()
		{
			ExitCode = 0;
			LastError = null;
			
			TempFile.PurgeColdFiles();

			Input?.Refresh();
			if (!(Input?.Exists ?? false)) throw new FileNotFoundException("Invalid input file", Input?.FullName);


			OutputFileName = TempFile.GetRandomFileName(OutputFormat);// $"{OutputBaseName.OrDefault(TempFile.GetRandomFileName())}.{OutputFormat}";
			if (!String.IsNullOrWhiteSpace(OutputBaseName))
			{
				OutputFileName = Path.Combine(Output.DirectoryName, $"{OutputBaseName}{Output.Extension}");
			}

			//
			// Generate a list of command line arguments. Parameter names are validated and values are properly escaped
			//
			var parameters = GetExeParameters().Select(x => {
				if (null == x.Value) return $"-{x.Key}"; // -switch
				return $"-{x.Key}=\"{Util.EscapeCommandLineParameter(x.Value)}\""; // -key=value
			});

			var executable = new FileInfo(AppConfig.ExecutablePath);

			ExitCode = await Util.StartProcessAsync(executable, parameters);

			//
			// Refresh the output information to reflect its existence
			//
			Output?.Refresh();
			return (Output?.Exists ?? false);
		}
	}
}
