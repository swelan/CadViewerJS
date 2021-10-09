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
		public static string ConverterLocation { get => GetLocalPath("CadViewer.ConverterLocation", UriKind.Absolute); }
		public static string Executable { get => Path.GetFileName(GetProperty("CadViewer.Executable")); }
		public static string ExecutablePath { get => Path.Combine(ConverterLocation, Executable ?? ""); }
		public static string TempFolder { get => GetLocalPath("CadViewer.TempFolder", UriKind.Absolute); }
		public static string LicenseLocation { get => GetLocalPath("CadViewer.LicenseLocation"); }
		public static string XPathLocation { get => GetLocalPath("CadViewer.XPathLocation"); }
		public static string FontLocation { get => GetLocalPath("CadViewer.FontLocation"); }

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


		public static string LibreOfficeProgramLocation { get => GetUri("CadViewer.LibreOffice.ProgramLocation", UriKind.Absolute).LocalPath; }
		public static string LibreOfficeExecutable { get => Path.Combine(LibreOfficeProgramLocation, "soffice.com"); }
		public static Uri LibreOfficeUserEnv { get => GetUri("CadViewer.LibreOffice.Env.UserInstallation", UriKind.Absolute); }
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
		public static string GetLocalPath(string Index, UriKind Kind = UriKind.RelativeOrAbsolute)
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

			return new Uri(GetUri("CadViewer.ConverterLocation", UriKind.Absolute), v ?? new Uri(".", UriKind.Relative)).LocalPath.TrimEnd(new char[] { '/', '\\' });
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
			if (String.IsNullOrEmpty(v)) return null;
			return new Uri(v + "/", Kind);
		}
		public static string GetRandomTemporaryFileName(string FileExtension = null)
		{
			return Util.GetRandomFileName(TempFolder, FileExtension);
		}
	}
}
