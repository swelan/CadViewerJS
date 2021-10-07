using System;
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
			if (null != v)
			{
				//
				// Replace any number of '\' that precedes '"' with 2x'\'
				// Replace trailing '\' with 2x'\'
				//
				return Regex.Replace(Regex.Replace(v.ToString(), @"(\\*)" + "\"", @"$1$1\" + "\""), @"(\\+)$", @"$1$1");
			}
			return null;
		}

		public static string GetFileExtension(Uri FileName)
		{
			return GetFileExtension(FileName.LocalPath);
		}
		public static string GetFileExtension(string FileName)
		{
			if (!String.IsNullOrEmpty(FileName))
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
			if (!String.IsNullOrEmpty(FileName))
			{
				return Path.GetFileNameWithoutExtension(FileName);
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
		public static async Task<int> StartProcessAsync(FileInfo Executable, IEnumerable<string> Arguments)
		{
			if (!(Executable?.Exists ?? false)) throw new FileNotFoundException($"Invalid executable '{Executable?.Name ?? ""}", Executable.FullName);

			var StartInfo = new ProcessStartInfo()
			{
				FileName = Executable.FullName,
				Arguments = String.Join(" ", Arguments ?? new List<string>()),
				CreateNoWindow = true,
				UseShellExecute = false,
				WorkingDirectory = Executable.DirectoryName
			};
			return await StartProcessAsync(StartInfo);
		}
		/// <summary>
		/// Convenience method to start a process asynchronously and return its ExitCode
		/// </summary>
		/// <param name="StartInfo"></param>
		/// <returns></returns>
		public static async Task<int> StartProcessAsync(ProcessStartInfo StartInfo)
		{
			//
			// TODO: allow output/error redirection by attaching listeners to Output/ErrorDataReceived
			//
			StartInfo.RedirectStandardError = false;
			StartInfo.RedirectStandardOutput = false;
			using (var process = new Process { StartInfo = StartInfo, EnableRaisingEvents = true })
			{
				return await StartProcessAsync(process).ConfigureAwait(false);
			}
		}
		/// <summary>
		/// Actually start a process and wait for its completion using an async-event model
		/// </summary>
		/// <param name="process"></param>
		/// <returns></returns>
		public static Task<int> StartProcessAsync(Process process)
		{
			process.EnableRaisingEvents = true;
			var tcs = new TaskCompletionSource<int>();
			process.Exited += (object state, EventArgs evt) => tcs.SetResult(process.ExitCode);
			if (!process.Start())
			{
				throw new InvalidOperationException($"Unable to start process {process}");
			}
			// If redirection is enabled:
			//process.BeginOutputReadLine();
			//process.BeginErrorReadLine();
			return tcs.Task;
		}
	}
}
