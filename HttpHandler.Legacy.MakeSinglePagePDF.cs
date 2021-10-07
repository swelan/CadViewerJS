using System;
using System.IO;
using System.Web;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CadViewer.HttpHandler.Legacy
{
	/// <summary>
	/// <para>Legacy handlers modified for security and robustness, but with maintained api-compatibility.</para>
	/// <para>TODO: create a better pipeline API, but this requires changes in the client software</para>
	/// <para>
	/// IMPORTANT: the SaveOrAppend handler is unneccessary if the API is changed so that the data is uploaded as multipart/mime content
	/// directly to the conversion processor. MakeSinglePagePDF can then be refactored to either stream the result directly to the client
	/// and/or indicate the pickup location. Currently the client makes assumptions about the pickup location, but this can in the short
	/// term be countered by routing the '/converters/files' path to the Download handler.
	/// </summary>
	[Obsolete("Legacy API port")]
	public class MakeSinglePagePDF : HttpTaskAsyncHandler
	{
		public override async Task ProcessRequestAsync(HttpContext Context)
		{
			var Request = Context.Request;
			var Response = Context.Response;

			object result = null;

			var tag = Util.GetFileNameWithoutExtension(Request["fileName_0"]);
			var rotation = Request["rotation_0"].OrDefault("landscape");
			var pageformat = Request["page_format_0"].OrDefault("A4");
			var original_filename = Util.GetFileNameWithoutExtension(Request["org_fileName_0"]).OrDefault(tag);

			//

			// Intermediate files
			TempFile base64_file = null;
			TempFile png_file = null;

			Response.ContentType = "text/plain";
			Response.Charset = "UTF-8";

			try
			{
				base64_file = TempFile.GetNamedTempFile($"{tag}_base64.png");
				// Convert the contents of the temporary file to binary (into another temp file...)
				var output_filename = Path.Combine(AppConfig.TempFolder, $"{tag}.png");

				// Purge the input stream on close
				using (var input = new FileStream(base64_file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose | FileOptions.SequentialScan))//, 4096, FileOptions.Asynchronous|FileOptions.SequentialScan))
				{
					// Move file pointer past the content-type header:
					input.Position = 22;// => "data:image/png;base64,".Length;

					using (var trx = new FromBase64Transform(FromBase64TransformMode.IgnoreWhiteSpaces))
					using (var decoder = new CryptoStream(input, trx, CryptoStreamMode.Read))
					{
						using (var output = new FileStream(output_filename, FileMode.Create, FileAccess.Write, FileShare.None))//, 4096, FileOptions.Asynchronous|FileOptions.SequentialScan))
						{
							// decode input as a stream into output
							//await decoder.CopyToAsync(output);
							decoder.CopyTo(output);
						}
					}
				}

				png_file = TempFile.GetTempFile(tag, "png");

				// Counter the '/converter/files' path prepended by the client software
				// Alternatively, use routing to map '/converter/files' to the handler path.
				//Response.Write($"../../getFileHandler.ashx?remainOnServer=1&attachment=0&fileTag={HttpUtility.UrlEncode(Util.GetFileNameWithoutExtension(file.PhysicalFile.Name))}&Type={HttpUtility.UrlEncode(Util.GetFileExtension(file.PhysicalFile.Name))}");

				// Invoke conversion to pdf
				var converter = new Converter()
				{
					Action = "makesinglepagepdf",
					InputFileName = png_file.FullName,
					OutputBaseName = tag.OrDefault(null),
					OutputFormat = "pdf"
				};

				converter.Parameters.Add("model", null);
				converter.Parameters.Add(rotation, null);
				converter.Parameters.Add(pageformat, null);
				converter.Parameters.Add("title", "A drawing");
				converter.Parameters.Add("author", "Lars");

				if (await converter.Execute())
				{
					var output = converter.Output;
					//
					// The response is the pickup filename, in plain text
					// A) Counter the '/converter/files' path prepended by the client software using back-relative path
					// B) Alternatively, use routing to map '/converter/files' to the handler path.
					//

					// Option A): use back-relative path
					//Response.Write($"../../getFileHandler.ashx?remainOnServer=1&attachment=0&fileTag={HttpUtility.UrlEncode(Util.GetFileNameWithoutExtension(output.Name))}&Type={HttpUtility.UrlEncode(Util.GetFileExtension(output.Name))}");

					// Option B): Configure a route that diverts the '/converter/files' path to the Download handler
					Response.Write(output.Name);
				}

				/*
				// TODO: deliver the result to the client in a structured format
				result = new
				{
					success = true,
					completedAction = "pdf_creation",
					errorCode = "E0",
					contentResponse = "stream",
					contentStreamData = $"{Path.Combine(AppConfig.GetUri("CadViewer.HandlerRootUrl")?.ToString(), "getFileHandler.ashx")}?remainOnServer=0&fileTag={HttpUtility.UrlEncode(Util.GetFileNameWithoutExtension(file.PhysicalFile.Name))}&Type={HttpUtility.UrlEncode(Util.GetFileExtension(file.PhysicalFile.Name))}"
				};
				*/

			}
			catch (Exception e)
			{
				//
				// TODO: error output
				// The legacy api doesn't handle errors, the response body is treated as a pickup file name.
				// For now, yield HTTP-500 (possibly separate HTTP-404) and emit a json error body
				//
				Response.Status = "500 Internal Server Error";
				Response.ContentType = "application/json";
				Response.Charset = "UTF-8";
				result = new
				{
					success = false,
					error = new
					{
						type = e.GetType().FullName,
						message = e.Message
					}
				};
				Util.ToJSON(result, Response.Output);
			}
			finally
			{
				// cleanup
				TempFile.Delete(base64_file, png_file);
			}
			//Util.ToJSON(result, Response.Output);
		}
		public override bool IsReusable { get => false; }
	}
}
