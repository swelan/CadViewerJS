using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CadViewer
{
	public class TempFile
	{
		private TempFile()
		{
		}
		~TempFile()
		{
		}

		public string FileTag { get => Path.GetFileNameWithoutExtension(PhysicalFile?.Name); }
		public string FileExtension { get => Util.GetFileExtension(PhysicalFile?.Name); }
		public string FullName { get => PhysicalFile?.FullName; }
		public DirectoryInfo Directory { get => PhysicalFile?.Directory; }

		public FileInfo PhysicalFile { get; private set; } = null;

		public bool Exists(bool Refresh = true) 
		{ 
			if (Refresh) PhysicalFile?.Refresh(); 
			return PhysicalFile?.Exists ?? false; 
		}
		public bool Create(Stream Source)
		{
			using (var target = new FileStream(PhysicalFile?.FullName, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				Source?.CopyTo(target);
				return true;
			}	
		}
		public async Task<bool> CreateAsync(Stream Source)
		{
			using (var target = new FileStream(PhysicalFile?.FullName, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
			{
				await Source?.CopyToAsync(target);
				return true;
			}
		}
		public bool Append(Stream Source)
		{
			using (var target = new FileStream(PhysicalFile?.FullName, FileMode.Append, FileAccess.Write, FileShare.None))
			{
				Source?.CopyTo(target);
				return true;
			}
		}
		public async Task<bool> AppendAsync(Stream Source)
		{
			using (var target = new FileStream(PhysicalFile?.FullName, FileMode.Append, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
			{
				await Source?.CopyToAsync(target);
				return true;
			}
		}

		public bool Move(string destination)
		{
			try
			{
				File.Move(PhysicalFile?.FullName, destination);
				return true;
			}
			catch (Exception)
			{
			}
			return false;
		}
		public bool Delete()
		{
			try
			{
				PhysicalFile?.Delete();
				return true;
			}
			catch (Exception)
			{
			}
			return false;
		}

		public static string GetRandomFileName(string FileExtension = null)
		{
			// Delegate to appconfig utility
			return AppConfig.GetRandomTemporaryFileName(FileExtension);
		}

		[Obsolete("Used solely for legacy API functions SaveFile|AppendFile")]
		public static TempFile GetNamedTempFile(string Name)
		{
			var tag = $"{new FileInfo(Name).Name}.txt";
			return GetTempFile(tag, null);
		}
		[Obsolete("Used solely for legacy API functions SaveFile|AppendFile")]
		public static TempFile CreateNamedTempFile(string Content, string Name)
		{
			var info = new FileInfo(Name);
			var name = Path.GetFileNameWithoutExtension(info.Name);
			var ext = Util.GetFileExtension(info.Name);
			if (String.IsNullOrEmpty(ext)) ext = "txt";
			else ext = $"{ext}.txt";

			var file = new TempFile()
			{
				PhysicalFile = new FileInfo(Path.Combine(AppConfig.TempFolder, $"{name}.{ext}"))
			};
			File.WriteAllText(file.FullName, Content ?? "");
			return file.Exists(true) ? file : null;
		}

		public static TempFile Touch(string FileExtension = null)
		{
			return CreateTempFile(null, FileExtension);
		}
		/// <summary>
		/// Create a temp file from a source data stream
		/// </summary>
		/// <param name="Source"></param>
		/// <param name="FileExtension"></param>
		/// <returns></returns>
		public static TempFile CreateTempFile(Stream Source, string FileExtension = null)
		{
			var file = new TempFile()
			{
				PhysicalFile = new FileInfo(AppConfig.GetRandomTemporaryFileName(FileExtension))
			};
			if (file.Create(Source)) return file;
			return null;
		}
		public static async Task<TempFile> CreateTempFileAsync(Stream Source, string FileExtension = null)
		{
			var file = new TempFile()
			{
				PhysicalFile = new FileInfo(AppConfig.GetRandomTemporaryFileName(FileExtension))
			};
			if (await file.CreateAsync(Source)) return file;
			return null;
		}
		/// <summary>
		/// Get a TempFile object from the temp folder; throws FileNotFoundException if not there.
		/// </summary>
		/// <param name="Tag"></param>
		/// <param name="FileExtension"></param>
		/// <returns></returns>
		public static TempFile GetTempFile(string Tag, string FileExtension)
		{
			//
			// Make sure any path components are stripped from the beginning of 'Tag' as
			// we only allow a distinct file tag as input. The tag may have an optional file extension which
			// is overriden by the 'FileExtension' parameter
			//
			Tag = Path.GetFileName(new Uri(Tag?.Trim() ?? "", UriKind.RelativeOrAbsolute).ToString());

			//
			// If 'FileExtension' is supplied, use that as the file extension, otherwise assume that the 'Tag' denotes the full
			// file name.
			//
			string filename = String.IsNullOrWhiteSpace(FileExtension) ? Tag : $"{Path.GetFileNameWithoutExtension(Tag)}.{FileExtension}";
			var file = new FileInfo(Path.Combine(AppConfig.TempFolder, filename));

			if (!file.Exists) throw new FileNotFoundException($"The file '{filename}' was not found in the expected location", file.FullName);

			return new TempFile()
			{
				PhysicalFile = file
			};
		}

		private static bool ServerCertificateValidator (object sender, X509Certificate rawcert, X509Chain chain, SslPolicyErrors error)
		{
			//
			// If there is an SslPolicyError => terminate (unless we're in debug mode)
			// Otherwise, validate the DnsName against the configured whitelist
			//
			if (null != rawcert && (AppConfig.IsDebug || SslPolicyErrors.None == error))
			{
				using (var cert = new X509Certificate2(rawcert))
				{
					//string subject = cert.GetNameInfo(X509NameType.SimpleName, false);
					string dns_name = cert.GetNameInfo(X509NameType.DnsName, false);
					return Util.DomainMatchesWildcard(dns_name, AppConfig.DomainWhitelist);
				}
			}
			return false;
		}

		/// <summary>
		/// Internal skeleton to orchestrate authenticated requests using a specific verb and callback
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="Source"></param>
		/// <param name="AuthContext"></param>
		/// <param name="Method"></param>
		/// <param name="Callback"></param>
		/// <returns></returns>
		private static async Task<T> IssueWebRequest<T>(
			Uri Source, 
			AuthorizationContext AuthContext, 
			string Method, 
			Func<System.Net.WebClient, Task<T>> Callback
		)
		{
			using (var xhr = new Util.WebClientEx())
			{
				if (!String.IsNullOrWhiteSpace(Method)) xhr.Method = Method;

				if (null != AuthContext)
				{
					foreach (var header in AuthContext.ToHttpHeaders())
					{
						xhr.Headers.Add(header.Key, header.Value);
					}
				}

				try
				{
					System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
					System.Net.ServicePointManager.ServerCertificateValidationCallback += ServerCertificateValidator;
					return await Callback.Invoke(xhr);
				}
				catch (System.Net.WebException err)
				{
					//err.data
					System.Net.HttpStatusCode status = ((System.Net.HttpWebResponse)err.Response)?.StatusCode ?? System.Net.HttpStatusCode.Unused;
					if (System.Net.HttpStatusCode.Forbidden == status) throw new NotAuthorizedException(err.Message);
					if (System.Net.HttpStatusCode.Unauthorized == status) throw new NotAuthenticatedException(err.Message);
					if (System.Net.HttpStatusCode.NotFound == status) throw new NotFoundException(err.Message);
					throw err;
				}
				catch (Exception)
				{
				}
				finally
				{
					System.Net.ServicePointManager.ServerCertificateValidationCallback -= ServerCertificateValidator;
				}
			}
			return default;
		}
		/// <summary>
		/// Download a file from a whitelisted source url using the provided AuthorizationContext
		/// </summary>
		/// <param name="Source">Url to query</param>
		/// <param name="FileExtension">The file extension of the temp file</param>
		/// <param name="AuthContext">Relay authentication/authorization parameters supplied from the client</param>
		/// <param name="MaxFileSize">0 = don't care; otherwise, throw PayloadTooLargeException if the Content-Length of the resource exceeds this value</param>
		/// <returns></returns>
		public static async Task<TempFile> DownloadFileAsync(
			Uri Source, 
			string FileExtension = null, 
			AuthorizationContext AuthContext = null,
			long MaxFileSize = 0
		)
		{
			//
			// Require TLS connection, unless we're in debug mode
			//
			if (null == Source || !Source.IsAbsoluteUri) throw new ArgumentNullException("Source", $"The uri '{Source?.ToString()}' is not a valid absolute uri");
			if (!"https".Equals(Source.Scheme, StringComparison.OrdinalIgnoreCase) && !AppConfig.IsDebug) throw new ArgumentOutOfRangeException("Source", "TLS connection is required for relay fetching");

			//
			// Make sure that the requested url is in the whitelist
			//
			var whitelist = AppConfig.DomainWhitelist;
			if (!Source.DomainMatchesWildcard(whitelist)) throw new ArgumentOutOfRangeException("Source", $"The domain '{Source.DnsSafeHost}' is not whitelisted");

			if (String.IsNullOrEmpty(FileExtension))
			{
				FileExtension = Util.GetFileExtension(Source);
			}

			// inline preflight request helper
			async Task<bool> Preflight(Action<System.Net.WebClient> callback)
			{
				return await IssueWebRequest(Source, AuthContext, "HEAD", async (xhr) =>
					{
						await xhr.DownloadStringTaskAsync(Source);
						if (null != callback) callback.Invoke(xhr);
						return true;
					}
				);
			}
			
			// inline download file helper
			async Task<TempFile> Download(Action<System.Net.WebClient, TempFile> callback)
			{
				return await IssueWebRequest(Source, AuthContext, "GET", async (xhr) =>
					{
						var temp = new TempFile()
						{
							PhysicalFile = new FileInfo(TempFile.GetRandomFileName(FileExtension))
						};
						await xhr.DownloadFileTaskAsync(Source, temp.FullName);

						if (null != callback) callback.Invoke(xhr, temp);
						return temp.Exists() ? temp : null;
					}
				);
			}
			
			//
			// If the app is configured for a maximum file size, issue a preflight request to get at the headers
			// without downloading the entire payload
			//
			Int64 content_length = 0;
			if (MaxFileSize > 0)
			{
				//
				// Make a preflight request to check whether the Content-Length exceeds the max allowed file size
				//
				await Preflight(xhr => {
					Int64.TryParse(xhr.ResponseHeaders?.Get("Content-Length"), out content_length);
				});

				if (0 == content_length) return TempFile.Touch();
				else if (MaxFileSize < content_length) // in bytes
				{
					// fail
					throw new PayloadTooLargeException(content_length, MaxFileSize);
				}
			}

			// Proceed to download the payload and return the temp file
			return await Download((xhr, temp) => { });
		}

		/// <summary>
		/// Physically delete one or more TempFiles silently
		/// </summary>
		/// <param name="items"></param>
		/// <returns>Number of files actually deleted</returns>
		public static int Delete(params TempFile[] items)
		{
			int res = 0;
			items?.ToList().ForEach(x =>
			{
				try
				{
					if (null != x)
					{
						x.Delete();
						res++;
					}
				}
				catch (Exception)
				{
					// Ignore
				}
			});
			return res;
		}

		/// <summary>
		/// Auto-cleanup of the 'Temp' folder. TODO: possibly schedule a task on 'idle' or similar
		/// </summary>
		public static void PurgeColdFiles(int OlderThanMinutes = 15)
		{
			// Remove any files not accessed for default randomly 15 min
			System.IO.Directory.GetFiles(AppConfig.TempFolder)
				.Select(x => new FileInfo(x))
				.Where(x => x.LastWriteTimeUtc < DateTime.UtcNow.AddMinutes(-OlderThanMinutes))
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


	}
}
