using System;
using System.IO;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CadViewer.HttpHandler
{
	public partial class CadViewerConvert : HttpTaskAsyncHandler
	{
		/// <summary>
		/// Extract the input file from request parameters
		/// </summary>
		/// <param name="Request"></param>
		/// <param name="Parameters"></param>
		/// <returns></returns>
		private async Task<TempFile> GetInputFile(HttpRequest Request, RequestParameters Parameters)
		{
			var source_url = (Parameters?.contentLocation ?? Request?["url"])?.Trim();
			var source_fmt = (Parameters?.contentFormat ?? Request["format"])?.Trim();
			if (!String.IsNullOrEmpty(source_url))
			{

				//
				// AuthorizationContext information is transmitted using a (secure) cookie name "AuthSession" (Cookie: AuthSession=token=value)
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
					),
					MaxFileSize: AppConfig.CVJS_MaxFileSize
				);
			}

			//
			// Return tempfile from input file stream
			//
			var file = Request.Files["file"];
			if (null != file)
			{
				long max_size = AppConfig.CVJS_MaxFileSize;
				if (max_size > 0 && file.InputStream.Length > max_size)
				{
					throw new PayloadTooLargeException(file.InputStream.Length, max_size);
				}
				using (var stream = file.InputStream)
				{
					return await TempFile.CreateTempFileAsync(stream, Request["format"]?.Trim() ?? Util.GetFileExtension(file.FileName));
				}
			}
			
			return null;
		}

		public override async Task ProcessRequestAsync(HttpContext Context)
		{
			var Response = Context.Response;
			var Request = Context.Request;

			object result = null;

			//
			// TODO: The 'request' parameter seems to be ambiguously url-encoded?
			// UPDATE: see below, comment for url escaping.
			// The client should send a regular www-form-urlencoded body, or simply the json document as the body
			//

			TempFile source = null;
			FileInfo output = null;
			try
			{
				var input = RequestParameters.FromJSON(Request.Form["request"].OrDefault(Request.QueryString["request"]).OrDefault("{}"));

				//
				// The 'CVJS' frontend doesn't handle url escaping properly.
				// if the 'converterLocation' parameter is on the form 'https%3A...', then the entire URL has been encoded as a single component (why??)
				// if not, the url is correctly encoded (assuming that a properly escaped URL has been passed to the frontend viewer).
				// The handling of this escaping problem is handled by the RequestParameters.contentLocation setter
				//

				source = await GetInputFile(Request, input);
				if (!(source?.PhysicalFile?.Exists ?? false)) throw new FileNotFoundException("Invalid input file");

				//
				// Download successful, invoke conversion
				//
				var converter = new CadViewerConverter()
				{
					Action = input?.action,
					InputFileName = source.FullName,
					OutputFormat = input?.GetParameterValue("f")?.ToString().Trim().ToLowerInvariant() ?? "svg"
				};

				if (null != input?.parameters)
				{
					foreach (var p in input.parameters.Where(v => !String.IsNullOrWhiteSpace(v.paramName)))
					{
						//
						// The parameters will be validated upon execution, so their respective content
						// is unimportant from a security perspective at this point
						//
						converter.AddParameter(p.paramName, p.paramValue);
					}
				}

				if (await converter.Execute())
				{
					//
					// TODO: the backend could simply stream the result directly to the client here and
					// any error information would be propagated via HTTP status messages. This would
					// yield a simpler and more intuitive interface
					//
					output = converter.Output;
					result = new
					{
						success = true,
						value = new
						{
							method = "pickup",
							tag = Util.GetFileNameWithoutExtension(output?.Name),
							type = Util.GetFileExtension(output?.Name),
							url = new Uri(Context.Server.MakeAppRelativePath("./", $"converters/files/{HttpUtility.UrlEncode(output.Name)}"), UriKind.Relative).ToString()
						},
						completedAction = converter.Action,
						errorCode = $"E{converter.ExitCode}",
						converter = "AutoXchange AX2020",
						version = "V1.00",
						userLabel = "fromCADViewerJS",
						contentLocation = input?.contentLocation ?? Request["url"] ?? Request.Files["file"]?.FileName,
						contentFormat = input?.contentFormat ?? Request["format"] ?? Util.GetFileExtension(Request.Files["file"]?.FileName),
						contentResponse = "stream",
						contentStreamData = $"{Context.Server.MakeAppRelativePath("./", "converters/files")}?remainOnServer=0&fileTag={HttpUtility.UrlEncode(Util.GetFileNameWithoutExtension(output.Name))}&Type={HttpUtility.UrlEncode(converter.OutputFormat)}"
					};
				}
				else
				{
					throw converter.LastError ?? new Exception("Unknown Error");
				}
			} 
			catch (Exception e)
			{
				result = new
				{
					success = false,
					error = new
					{
						type = e.GetType().FullName,
						message = e.Message ?? "Unknown error",
						input = source?.PhysicalFile?.Name,
						output = output?.Name
					}
				};

			}
			Util.ToJSON(result, Response.Output);
		}
		public override bool IsReusable { get => false; }

	}
}
