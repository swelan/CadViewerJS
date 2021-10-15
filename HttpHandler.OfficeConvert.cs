using System;
using System.IO;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CadViewer.HttpHandler
{

	public class OfficeConvert : HttpTaskAsyncHandler
	{
		private async Task<TempFile> GetInputFile(HttpRequest Request)
		{
			var source_url = Request?["url"]?.Trim().OrDefault(null);
			var source_fmt = Request?["format"]?.Trim().OrDefault(null);

			if (!String.IsNullOrEmpty(source_url))
			{

				//
				// AuthorizationContext information is transmitted using a (secure) cookie named "AuthSession" (Cookie: AuthSession=token=value)
				// Bearer/Basic are mutually exclusive, FormCookie is always relayed
				//
				var auth_cookie = Request.Cookies["AuthSession"];

				return await TempFile.DownloadFileAsync(
					Source: new Uri(source_url),
					FileExtension: source_fmt,
					AuthContext: new AuthorizationContext(
						BearerToken: HttpUtility.UrlDecode(auth_cookie?["token"]),
						Username: HttpUtility.UrlDecode(auth_cookie?["username"]),
						Password: HttpUtility.UrlDecode(auth_cookie?["password"]),
						FormCookie: HttpUtility.UrlDecode(auth_cookie?["cookie"])
					)
				);
			}

			//
			// Return tempfile from input file stream
			//
			var file = Request.Files["file"] ?? (Request.Files.Count > 0 ? Request.Files[0] : null);
			if (null != file)
			{
				long max_size = AppConfig.MaxFileSize;
				if (max_size > 0 && file.InputStream.Length > max_size)
				{
					throw new PayloadTooLargeException(file.InputStream.Length, max_size);
				}
				using (var stream = file.InputStream)
				{
					return await TempFile.CreateTempFileAsync(stream, source_fmt ?? Util.GetFileExtension(file.FileName));
				}
			}

			return null;
		}

		public override async Task ProcessRequestAsync(HttpContext Context)
		{
			var Request = Context.Request;
			var Response = Context.Response;

			object result = null;
			var outputFileName = Util.GetFileName(Request["filename"].OrDefault(null)?.Trim());
			var outputBaseName = Util.GetFileNameWithoutExtension(outputFileName);
			var outputExtension = Util.GetFileExtension(outputFileName).OrDefault(Request["output-format"]).OrDefault("pdf").Trim();
			Int32.TryParse(Request["attachment"], out int attachment);

			FileInfo output = null;
			TempFile source = null;
			int ExitCode = 0;
			Exception error = null; // Save this to produce informative HTTP status codes when compiling the response

			try
			{
				source = await GetInputFile(Request);
				if (!(source?.PhysicalFile?.Exists ?? false)) throw new FileNotFoundException("Invalid input file");

				var converter = new OfficeConverter()
				{
					Action = "convert",
					InputFileName = source.FullName,
					OutputFormat = outputExtension ?? "pdf"
				};
				
				if (await converter.Execute())
				{
					output = converter.Output;
					result = new
					{
						success = true,
						value = new
						{
							method = "pickup",
							tag = Util.GetFileNameWithoutExtension(output.Name),
							type = Util.GetFileExtension(output.Name),
							url = new Uri(Context.Server.MakeAppRelativePath("./", $"converters/files/{HttpUtility.UrlEncode(output.Name)}"), UriKind.Relative).ToString()
						},
						error = (object)null
					};
				}
				else
				{
					ExitCode = converter.ExitCode;
					throw converter.LastError ?? new Exception($"Unknown error");
				}
			}
			catch (Exception e)
			{
				error = e;
				result = new
				{
					success = false,
					value = (object)null,
					error = new
					{
						type = e.GetType().FullName,
						message = e.Message,
						exitcode = ExitCode
					}
				};
			}
			finally
			{
				TempFile.Delete(source);
			}

			//
			// Compile a response conditionally
			//
			if (!String.IsNullOrEmpty(outputBaseName))
			{
				if (null != output)
				{
					// Send as a mime stream
					Response.ContentType = MimeMapping.GetMimeMapping(output.FullName).OrDefault("application/octet-stream");
					Response.Charset = "UTF-8";

					//
					// Add content-disposition as 'attachment' if requested by the client; default='inline'
					//
					Response.AddHeader(
						"Content-Disposition", 
						new System.Net.Http.Headers.ContentDispositionHeaderValue(0 == attachment ? "inline" : "attachment")
						{
							Size = output.Length,
							ReadDate = output.LastAccessTime,
							CreationDate = output.CreationTime,
							ModificationDate = output.LastWriteTime,
							FileName = $"{outputBaseName}{output.Extension}",
							FileNameStar = $"{outputBaseName}{output.Extension}"
						}.ToString()
					);

					Response.TransmitFile(output.FullName);
					try
					{
						Response.Flush();
						output.Delete();
					}
					catch (Exception)
					{
					}
				}
				else
				{
					//
					// Report the error via HTTP 404 (="the output file could not be found, the body contains the reason").
					//
					Response.ContentType = "application/json";
					Response.Charset = "UTF-8";
					Response.Status = new Func<Exception, string>((err) => {
						if (err is PayloadTooLargeException)
						{
							Response.AddHeader("DCS-Max-Converter-FileSize", ((PayloadTooLargeException)err).MaxSize.ToString());
							Response.AddHeader("DCS-Converter-FileSize", ((PayloadTooLargeException)err).Size.ToString());
							return "413 Payload Too Large";
						}
						return "404 File Not Found";
					})(error);
					Util.ToJSON(result, Response.Output);
				}
			}
			else
			{
				//
				// If successful, the body contains the information neccessary for pickup
				// If unsuccessful, the body contains the error reason
				//
				Response.ContentType = "application/json";
				Response.Charset = "UTF-8";
				Util.ToJSON(result, Response.Output);
			}
		}
		public override bool IsReusable { get => false; }
	}
}
