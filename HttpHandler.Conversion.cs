using System;
using System.IO;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CadViewer.HttpHandler
{
	public class Conversion : IHttpHandler
	{
		/// <summary>
		/// Extract the input file from request parameters
		/// </summary>
		/// <param name="Request"></param>
		/// <param name="Parameters"></param>
		/// <returns></returns>
		private TempFile GetInputFile(HttpRequest Request, ConversionRequestParameters Parameters)
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

				return TempFile.DownloadFile(
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
			var file = Request.Files["file"];
			if (null != file)
			{
				using (var stream = file.InputStream)
				{
					return TempFile.CreateTempFile(stream, Request["format"]?.Trim() ?? Util.GetFileExtension(file.FileName));
				}
			}
			
			return null;
		}
		public virtual void ProcessRequest(HttpContext Context)
		{
			var Response = Context.Response;
			var Request = Context.Request;

			object result = null;

			//
			// TODO: The 'request' parameter seems to be ambiguously url-encoded?
			// The client should send a regular www-form-urlencoded body, or simply the json document as the body
			//
			var input = ConversionRequestParameters.FromJSON(HttpUtility.UrlDecode(Request["request"]));

			TempFile source = null;
			try
			{
				source = GetInputFile(Request, input);
			}
			catch (Exception e)
			{
				result = new
				{
					success = false,
					error = new
					{
						type = e.GetType().FullName,
						message = e.Message,
						source = input?.contentLocation ?? Request["url"] ?? Request.Files["file"]?.FileName,
						format = input?.contentFormat ?? Request["format"]
					}
				};
			}

			if (source?.PhysicalFile?.Exists ?? false)
			{
				//
				// Download successful, invoke conversion
				//
				var converter = new Converter()
				{
					InputFileName = source.FullName,
					OutputFormat = input.GetParameterValue("f")?.ToString().Trim().ToLowerInvariant() ?? "svg"
				};

				if (null != input.parameters)
				{
					foreach (var p in input.parameters.Where(v => !String.IsNullOrEmpty(v.paramName)))
					{
						//
						// The parameters will be validated upon execution, so their respective content
						// is unimportant from a security perspective at this point
						//
						converter.Parameters.Add(p.paramName, p.paramValue);
					}
				}

				if (converter.Execute())
				{
					//
					// TODO: the backend could simply stream the result directly to the client here and
					// any error information would be propagated via HTTP status messages. This would
					// yield a simpler and more intuitive interface
					//
					var output = converter.Output;
					result = new
					{
						success = true,
						completedAction = (converter.OutputFormat?.Equals("svg") ?? false) ? "svg_creation" : "pdf_creation",
						errorCode = $"E{converter.ExitCode}",
						converter = "AutoXchange AX2020",
						version = "V1.00",
						userLabel = "fromCADViewerJS",
						contentLocation = input?.contentLocation ?? Request["url"] ?? Request.Files["file"]?.FileName,
						contentFormat = input?.contentFormat ?? Request["format"] ?? Util.GetFileExtension(Request.Files["file"]?.FileName),
						contentResponse = "stream",
						contentStreamData = $"{Path.Combine(AppConfig.GetUri("CadViewer.HandlerRootUrl")?.ToString(), "getFileHandler.ashx")}?remainOnServer=0&fileTag={HttpUtility.UrlEncode(Path.GetFileNameWithoutExtension(output.Name))}&Type={HttpUtility.UrlEncode(converter.OutputFormat)}"
					};
				}
				else
				{
					result = new
					{
						success = false,
						error = new
						{
							type = converter.LastError?.GetType().FullName,
							message = converter.LastError?.Message ?? "Unknown error",
							input = source?.PhysicalFile?.Name,
							output = converter.Output?.Name
						}
					};
				}
			}
			result = result ?? new
			{
				success = false,
				error = new
				{
					message = $"Invalid input file '{source?.PhysicalFile?.Name ?? "null"}'",
					input = source?.PhysicalFile?.Name,
				}
			};
			Util.ToJSON(result, Response.Output);
		}
		public bool IsReusable { get => false; }

	}
}
