using System;
using System.Web;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CadViewer
{
	#region AppExceptions
	public class PayloadTooLargeException : System.Exception
	{
		public PayloadTooLargeException(string Message, Int64 Size, Int64 MaxSize, Exception InnerException = null)
			: base(Message, InnerException)
		{
			this.Size = Size;
			this.MaxSize = MaxSize;
		}
		public PayloadTooLargeException(Int64 Size, Int64 MaxSize, Exception InnerException = null)
			: this($"The file size exceeds the maximum allowed value ({((decimal)MaxSize / 1048576).ToString("0.##")} MB)", Size, MaxSize, InnerException)
		{
		}

		public Int64 Size { get; set; } = 0;
		public Int64 MaxSize { get; set; } = 0;
	}
	#endregion
	#region StringExtensions
	public static class StringExtensions
	{
		// "Handy empty-string coalescing function"
		public static string OrDefault(this string v, object @default)
		{
			return string.IsNullOrWhiteSpace(v) ? (null == @default ? null : Convert.ToString(@default)) : v;
		}
	}
	#endregion

	#region HttpServerUtlityExtensions
	public static class HttpServerUtilityExtensions
	{
		public static Uri MakeAppRelativeUri(this HttpServerUtility server, string from_relative = "./", string to_relative = "./")
		{
			from_relative = from_relative?.TrimStart(new char[] { '/', '\\' }).Replace('\\', '/');
			to_relative = to_relative?.TrimStart(new char[] { '/', '\\' }).Replace('\\', '/');

			if (Path.IsPathRooted(from_relative)) throw new ArgumentOutOfRangeException("from_relative", "Invalid relative path specification");
			if (Path.IsPathRooted(to_relative)) throw new ArgumentOutOfRangeException("to_relative","Invalid relative path specification");

			var app_root = new Uri(server.MapPath("~/"), UriKind.Absolute);
			var from_absolute = new Uri(server.MapPath(from_relative), UriKind.Absolute);
			var to_absolute = new Uri(from_absolute, to_relative);

			if (!from_absolute.IsBaseOf(to_absolute)) throw new ArgumentOutOfRangeException("to_relative", "The from_relative path must be a base of to_relative");

			return app_root.MakeRelativeUri(to_absolute);
		}
		public static string MakeAppRelativePath(this HttpServerUtility server, string from_relative = "./", string to_relative = "./")
		{
			return $"/{server.MakeAppRelativeUri(from_relative, to_relative).ToString()}";
		}
	}
	#endregion
	#region UriExtensions
	public static class UriExtensions
	{
		public static bool DomainMatchesWildcard(this Uri uri, IEnumerable<string> Wildcard)
		{
			if (uri?.IsAbsoluteUri ?? false)
			{
				return Util.DomainMatchesWildcard(uri?.DnsSafeHost, Wildcard);
			}
			return false;
		}
	}
	#endregion
	/*
	/// <summary>
	/// TODO: Jeff Atwood's technique for scheduling repeating, simple tasks
	/// </summary>
	public static class CacheMaintenance
	{
		private static System.Web.Caching.CacheItemRemovedCallback Callback = null;
		private static int IntervalInSeconds = 0;
		private static readonly string Key = "CadViewer.CacheMaintenance";
		public static void SetSchedule(int seconds = 300)
		{
			if (seconds <= 0)
			{
				// Turn off
				IntervalInSeconds = 0;
				System.Web.HttpRuntime.Cache.Remove(Key);
				return;
			}

			IntervalInSeconds = seconds;
			if (null == Callback) Callback = new System.Web.Caching.CacheItemRemovedCallback(CacheItemRemoved);
			if (null == System.Web.HttpRuntime.Cache.Remove(Key))
			{
				System.Web.HttpRuntime.Cache.Insert(
					key: Key,
					value: IntervalInSeconds,
					dependencies: null,
					absoluteExpiration: DateTime.Now.AddSeconds(IntervalInSeconds),
					slidingExpiration: System.Web.Caching.Cache.NoSlidingExpiration,
					priority: System.Web.Caching.CacheItemPriority.NotRemovable,
					onRemoveCallback: Callback
				);
			}
		}
		private static void CacheItemRemoved(string key, object value, System.Web.Caching.CacheItemRemovedReason reason)
		{
			if (IntervalInSeconds > 0)
			{
				TempFile.PurgeColdFiles();
				TempFile.CreateNamedTempFile($"The time is {DateTime.UtcNow}; interval={Convert.ToInt32(value)}s; reason: {reason}", "cache-maintenance.log");
				
				SetSchedule(IntervalInSeconds);
			}
		}
	}
	*/
	public static class Util
    {
		public static string ToJSON<T>(T Value)
		{
			return JsonConvert.SerializeObject(Value);
		}
		public static void ToJSON<T>(T Value, TextWriter Destination)
		{
			var serializer = new JsonSerializer();
			using (var writer = new JsonTextWriter(Destination))
			{
				serializer.Serialize(writer, Value);
				writer.Close();
			}
		}

		public static T FromJSON<T>(string json)
		{
			try
			{
				return JsonConvert.DeserializeObject<T>(json);
			}
			catch (JsonException)
			{
			}
			return default;
		}
		public static T FromJSON<T>(TextReader Reader)
		{
			using (var reader = new JsonTextReader(Reader))
			{
				var serializer = new JsonSerializer();
				try
				{
					return serializer.Deserialize<T>(reader);
				}
				catch (JsonException)
				{
				}
				return default;
			}
		}

		public static string EscapeCommandLineParameter(object v)
		{
			string argument = v?.ToString();
			if (null != argument)
			{
				// Short circuit if argument is clean and doesn't need escaping
				if (argument.Length != 0 && argument.All(c => !char.IsWhiteSpace(c) && c != '"'))
					return argument;

				var buffer = new StringBuilder();

				buffer.Append('"');

				for (var i = 0; i < argument.Length;)
				{
					var c = argument[i++];

					if (c == '\\')
					{
						var numBackSlash = 1;
						while (i < argument.Length && argument[i] == '\\')
						{
							numBackSlash++;
							i++;
						}

						if (i == argument.Length)
						{
							buffer.Append('\\', numBackSlash * 2);
						}
						else if (argument[i] == '"')
						{
							buffer.Append('\\', numBackSlash * 2 + 1);
							buffer.Append('"');
							i++;
						}
						else
						{
							buffer.Append('\\', numBackSlash);
						}
					}
					else if (c == '"')
					{
						buffer.Append('\\');
						buffer.Append('"');
					}
					else
					{
						buffer.Append(c);
					}
				}

				buffer.Append('"');

				return buffer.ToString();
			}
			return null;
			/*

			if (null != v)
			{
				//
				// Replace any number of '\' that precedes '"' with 2x'\'
				// Replace trailing '\' with 2x'\'
				//
				return Regex.Replace(Regex.Replace(v.ToString(), @"(\\*)" + "\"", @"$1$1\" + "\""), @"(\\+)$", @"$1$1");
			}
			return null;
			*/
		}

		/// <summary>
		/// Assuming a valid path with no invalid path chars, enclose components having whitespace in double-quotes
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		public static string EscapePathComponents(string path)
		{
			char[] quote = { '"' };
			return String.Join("\\", path?.Trim(quote).Split(new char[] { '\\', '/' })
				.Select(x =>
					x.Trim(quote)						// Normalize if it's already quoted
					.Any(c => Char.IsWhiteSpace(c)) ? 
						$"\"{x}\"" :					// If it contains whitespace, escape
						x								// otherwise return as-is
				)
			);
		}

		public static string GetFileName(Uri FileName)
		{
			return GetFileName(FileName.LocalPath);
		}
		public static string GetFileName(string FileName)
		{
			if (!String.IsNullOrWhiteSpace(FileName))
			{
				return Path.GetFileName(FileName.Trim());
			}
			return null;
		}
		public static string GetFileExtension(Uri FileName)
		{
			return GetFileExtension(FileName.LocalPath);
		}
		public static string GetFileExtension(string FileName)
		{
			if (!String.IsNullOrWhiteSpace(FileName))
			{
				return Path.GetExtension(FileName)?.Trim().Trim(new char[] { '.' });
			}
			return null;
		}
		public static string GetFileNameWithoutExtension(Uri FileName)
		{
			return GetFileNameWithoutExtension(FileName.LocalPath);
		}
		public static string GetFileNameWithoutExtension(string FileName)
		{
			if (!String.IsNullOrWhiteSpace(FileName))
			{
				return Path.GetFileNameWithoutExtension(FileName.Trim());
			}
			return null;
		}
		public static string GetRandomFileName(string BaseFolder, string FileExtension = null)
		{
			if (!String.IsNullOrEmpty(FileExtension = FileExtension?.Trim().Trim(new char[] { '.' })))
			{
				FileExtension = $".{FileExtension}";
			}
			string filename = $"F{new Random().Next().ToString("D10")}{FileExtension ?? ""}";
			if (null != BaseFolder) return Path.Combine(BaseFolder, filename);
			return filename;
		}

		public static bool DomainMatchesWildcard(string Domain, string Wildcard)
		{
			if (Wildcard?.StartsWith("*.") ?? false)
			{
				Wildcard = $"*{Wildcard.Substring(2)}";
			}
			return MatchesWildcard(Domain, Wildcard, true);
		}
		public static bool DomainMatchesWildcard(string Domain, IEnumerable<string> Wildcards)
		{
			if (null != Wildcards)
			{
				foreach (var wildcard in Wildcards)
				{
					if (DomainMatchesWildcard(Domain, wildcard)) return true;
				}
			}
			return false;
		}
		public static bool MatchesWildcard(string StringToMatch, string Wildcard, bool IgnoreCase = true)
		{
			StringComparison comparison = IgnoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;

			//
			// The empty string doesn't match anything, and nothing matches an empty wildcard
			//
			if (String.IsNullOrEmpty(StringToMatch) || String.IsNullOrEmpty(Wildcard)) return false;

			int pos = Wildcard.IndexOf("*");
			if (pos < 0)
			{
				// No wildcard match
				return StringToMatch.Equals(Wildcard, comparison);
			}

			if (pos > 0)
			{
				// Capture the case where the beginning of the strings don't match
				if (!StringToMatch.StartsWith(Wildcard.Substring(0, pos), comparison))
				{
					return false;
				}
			}

			pos = Wildcard.LastIndexOf('*');
			if (pos < (Wildcard.Length - 1))
			{
				// Capture the case where the end of the strings don't match
				int start = pos + 1;
				if (!StringToMatch.EndsWith(Wildcard.Substring(start, Wildcard.Length - start), comparison))
				{
					return false;
				}
			}

			// Perform regex match
			RegexOptions options = RegexOptions.CultureInvariant | (IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
			return Regex.IsMatch(StringToMatch, Wildcard.Replace(@".", @"\.").Replace(@"*", @".*"), options);
		}

		/// <summary>
		/// Convenience method to start a process asynchronously and return its ExitCode
		/// </summary>
		/// <param name="Executable"></param>
		/// <param name="Arguments"></param>
		/// <returns></returns>
		public static async Task<AsyncProcess.Result> StartProcessAsync(
			FileInfo Executable, 
			IEnumerable<string> Arguments, 
			int? TimeoutMs = null,
			bool RedirectStandardOutput = false, 
			bool RedirectStandardError = false
		)
		{
			if (!(Executable?.Exists ?? false)) throw new FileNotFoundException($"Invalid executable '{Executable?.Name ?? ""}", Executable.FullName);

			return await AsyncProcess.StartProcessAsync(
				new ProcessStartInfo()
				{	
					FileName = Executable.FullName,
					Arguments = String.Join(" ", Arguments ?? new List<string>()),
					CreateNoWindow = true,
					UseShellExecute = false,
					WorkingDirectory = Executable.DirectoryName,
					RedirectStandardOutput = RedirectStandardOutput,
					RedirectStandardError = RedirectStandardError
				},
				TimeoutMs
			);
		}
		/*
		/// <summary>
		/// Convenience method to start a process asynchronously and return its ExitCode
		/// </summary>
		/// <param name="StartInfo"></param>
		/// <returns></returns>
		public static async Task<int> StartProcessAsync(
			ProcessStartInfo StartInfo, 
			Action<object, DataReceivedEventArgs> OnOutput = null, 
			Action<object, DataReceivedEventArgs> OnError = null
		)
		{
			//
			// TODO: allow output/error redirection by attaching listeners to Output/ErrorDataReceived
			//
			StartInfo.RedirectStandardError =  (null != OnError);// false;
			StartInfo.RedirectStandardOutput = (null != OnOutput);//false;
			using (var process = new Process { StartInfo = StartInfo, EnableRaisingEvents = true })
			{
				return await StartProcessAsync(process, OnOutput, OnError).ConfigureAwait(false);
			}
		}
		/// <summary>
		/// Actually start a process and wait for its completion using an async-event model
		/// </summary>
		/// <param name="process"></param>
		/// <returns></returns>
		public static Task<int> StartProcessAsync(
			Process process, 
			Action<object, DataReceivedEventArgs> OnOutput = null, 
			Action<object, DataReceivedEventArgs> OnError = null
		)
		{
			process.EnableRaisingEvents = true;
			var tcs = new TaskCompletionSource<int>();
			process.Exited += (object state, EventArgs evt) => tcs.SetResult(process.ExitCode);

			if (null != OnOutput) process.OutputDataReceived += new DataReceivedEventHandler(OnOutput);
			if (null != OnError) process.ErrorDataReceived += new DataReceivedEventHandler(OnError);

			if (!process.Start())
			{
				throw new InvalidOperationException($"Unable to start process {process}");
			}
			// If redirection is enabled:
			if (process.StartInfo.RedirectStandardOutput) process.BeginOutputReadLine();
			if (process.StartInfo.RedirectStandardError) process.BeginErrorReadLine();
			return tcs.Task;
		}
		*/
		/// <summary>
		/// Open a Win32 registry key and yield the value from invoking the callback on that key.
		/// If the platform is not windows, null is always passed to the reviver
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="Path">Registry path</param>
		/// <param name="Callback">Reviver for the requested key</param>
		/// <param name="Hive">Requested hive or default LocalMachine</param>
		/// <param name="Bitness">Search a particluar hive view, or default view determined by runtime bitness of assembly</param>
		/// <returns></returns>
		public static T ReadRegistryKey<T>(
			string Path, 
			Func<Microsoft.Win32.RegistryKey, T> Callback,
			Microsoft.Win32.RegistryHive Hive = Microsoft.Win32.RegistryHive.LocalMachine,
			Microsoft.Win32.RegistryView Bitness = Microsoft.Win32.RegistryView.Default
		)
		{
			if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
			{
				using (var base_key = Microsoft.Win32.RegistryKey.OpenBaseKey(Hive, Bitness))
				using (var key = base_key?.OpenSubKey(Path))
				{
					return Callback.Invoke(key);
				}
			}
			return Callback.Invoke(null);
		}

		public class WebClientEx : System.Net.WebClient
		{
			public WebClientEx(string Method = null)
			{
				this.Method = Method;
			}
			public string Method { get; set; } = null;
			protected override System.Net.WebRequest GetWebRequest(Uri uri)
			{
				var request = base.GetWebRequest(uri);
				if (!String.IsNullOrWhiteSpace(Method))
				{
					request.Method = Method;
				}
				return request;
			}
		}
	}
}
