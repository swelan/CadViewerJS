using System;
using System.IO;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CadViewer
{
	/// <summary>
	/// Proxy interface for the application configuration
	/// </summary>
	public class AppConfig
	{
		public static bool IsDebug
		{
			get
			{
				switch (GetProperty("CadViewer.Debug")?.Trim().ToLowerInvariant())
				{
					case "true":
					case "on":
					case "1":
						return true;
					default:
						return false;
				}
			}
		}
		public static string TempFolder { get => GetLocalPath("CadViewer.TempFolder", UriKind.Absolute); }
		public static string ConverterLocation { get => GetLocalPath("CadViewer.ConverterLocation", UriKind.Absolute); }


		public static string CVJS_ProgramLocation { get => GetLocalPath("CadViewer.CVJS.ProgramLocation"); }
		public static string CVJS_Executable { get => Path.GetFileName(GetProperty("CadViewer.CVJS.Executable")); }
		public static string CVJS_ExecutablePath { get => Path.Combine(CVJS_ProgramLocation, CVJS_Executable ?? ""); }
		public static string CVJS_LicenseLocation { get => GetLocalPath("CadViewer.CVJS.LicenseLocation", UriKind.RelativeOrAbsolute, CVJS_ProgramLocation); }
		public static string CVJS_XPathLocation { get => GetLocalPath("CadViewer.CVJS.XPathLocation", UriKind.RelativeOrAbsolute, CVJS_ProgramLocation); }
		public static string CVJS_FontLocation { get => GetLocalPath("CadViewer.CVJS.FontLocation", UriKind.RelativeOrAbsolute, CVJS_ProgramLocation); }

		/// <summary>
		/// Make exe timeout configurable; -1 => null, 0 => default
		/// </summary>
		public static int? ExecutableTimeoutMs 
		{ 
			get
			{
				int result = 0;
				Int32.TryParse(GetProperty("CadViewer.ExecutableTimeoutMs"), out result);
				if (result < 0) return null;
				if (result == 0) return 10000;
				return result;
			} 
		}
		/// <summary>
		/// <para>A white-list of domains to allow relay-fetching of content from. To allow fetch from any domain, include '*' in the whitelist configuration.</para>
		/// <para>A restrictive policy is recommended</para>
		/// </summary>
		public static List<string> DomainWhitelist
		{
			get
			{
				return GetProperty("CadViewer.DomainWhitelist")?.Split(',').Select(v => v?.Trim().ToLowerInvariant()).Where(v => !String.IsNullOrEmpty(v)).ToList() ?? new List<string>();
			}
		}
		/// <summary>
		/// Maximum file size allowed as input to conversion, to avoid ridiculously large files
		/// </summary>
		public static Int64 MaxFileSize
		{
			get
			{
				Int64.TryParse(GetProperty("CadViewer.MaxFileSize"), out Int64 max_size);
				return max_size > 0 ? max_size : 0;
			}
		}

		public static string LibreOfficeProgramLocation
		{
			get
			{
				var res = GetUri("CadViewer.LibreOffice.ProgramLocation", UriKind.Absolute)?.LocalPath;
				if (String.IsNullOrWhiteSpace(res) || !Path.IsPathRooted(res) || !Directory.Exists(res))
				{
					//
					// Make sure the correct hive is selected (if the app is running in 32-bit mode)
					// 
					res = Util.ReadRegistryKey(
						Path: "Software\\LibreOffice\\UNO\\InstallPath",
						Hive: Microsoft.Win32.RegistryHive.LocalMachine,
						Bitness: Microsoft.Win32.RegistryView.Registry64,
						Callback: key =>
						{
							//
							// value names are on the form "LibreOffice 7.2"
							// Select the value of the highest version
							//
							var value_name = key?.GetValueNames()
								.Where(x => x.StartsWith("LibreOffice", StringComparison.OrdinalIgnoreCase))
								.OrderBy(x =>
									{
										Version.TryParse(x.Substring("LibreOffice".Length).Trim(), out Version ver);
										return ver;
									}
								)
								.LastOrDefault();

							return key?.GetValue(value_name)?.ToString();
						}
					);
				}
				return Directory.Exists(res) && Path.IsPathRooted(res) ? res : null;
			}
		}

		//public static string LibreOfficeExecutable { get => Path.Combine(LibreOfficeProgramLocation, "soffice.com"); }
		//public static Uri LibreOfficeUserEnv { get => GetUri("CadViewer.LibreOffice.Env.UserInstallation", UriKind.Absolute); }

		public static string LibreOfficePythonExecutable { get => Path.Combine(LibreOfficeProgramLocation, "python.exe"); }
		public static string LibreOfficeUnoconvLocation { get => GetLocalPath("CadViewer.LibreOffice.UnoconvLocation");  }
		public static string LibreOfficeUnoconvExecutable { get => Path.Combine(LibreOfficeUnoconvLocation, "unoconv"); }

		/// <summary>
		/// Get a configuration property as string
		/// </summary>
		/// <param name="Index"></param>
		/// <returns></returns>
		public static string GetProperty(string Index)
		{
			return ConfigurationManager.AppSettings.Get(Index) ?? null;
		}

		/// <summary>
		/// <para>Retrieve a configuration property as a normalized local path.</para>
		/// <para>If the property is expressed as a relative path, it is evaluated relative to the ConverterLocation property</para>
		/// </summary>
		/// <param name="Index">Configuration property</param>
		/// <param name="Kind">Determines whether the returned path may be ConverterLocation-relative or not</param>
		/// <returns></returns>
		public static string GetLocalPath(string Index, UriKind Kind = UriKind.RelativeOrAbsolute, string RelativeTo = null)
		{
			var v = GetUri(Index, Kind);
			if (v?.IsAbsoluteUri ?? false)
			{
				return v?.LocalPath;
			}

			if (null == v && UriKind.Absolute == Kind)
			{
				throw new Exception($"AppConfig:'{Index}' must be a valid, absolute path");
			}

			Uri base_uri = (
				String.IsNullOrEmpty(RelativeTo) ? 
					GetUri("CadViewer.ConverterLocation", UriKind.Absolute) : 
					new Uri(RelativeTo + "/", UriKind.Absolute)
			);
			return new Uri(base_uri, v ?? new Uri(".", UriKind.Relative)).LocalPath.TrimEnd(new char[] { '/', '\\' });
		}

		/// <summary>
		/// Retrieve a configuration property interpreted as an uri
		/// </summary>
		/// <param name="Index"></param>
		/// <param name="Kind">Determines whether the returned uri may be interpreted as a relative uri or not</param>
		/// <returns></returns>
		public static Uri GetUri(string Index, UriKind Kind = UriKind.RelativeOrAbsolute)
		{
			var v = GetProperty(Index)?.TrimEnd(new char[] { '\\', '/' });
			if (UriKind.Relative == Kind || UriKind.RelativeOrAbsolute == Kind)
			{
				v = v?.TrimStart(new char[] { '\\', '/' });
			}
			if (String.IsNullOrEmpty(v)) return null;

			return new Uri(v + "/", Kind);
		}
		public static string GetRandomTemporaryFileName(string FileExtension = null)
		{
			return Util.GetRandomFileName(TempFolder, FileExtension);
		}
	}
}
