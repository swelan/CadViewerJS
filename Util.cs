using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CadViewer
{
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
	}
}
