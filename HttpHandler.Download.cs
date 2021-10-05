using System;
using System.IO;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CadViewer.HttpHandler
{
	public class Download : IHttpHandler
	{
		public virtual void ProcessRequest(HttpContext Context)
		{
			var Response = Context.Response;
			var Request = Context.Request;

			//
			// Set reasonable default headers
			// Content-Type is potentially modified below, application/octet-stream is the default for binary stream output
			//
			Response.AddHeader("Access-Control-Allow-Origin", "*");
			Response.Charset = "UTF-8";
			Response.ContentType = "application/octet-stream";

			TempFile file = null;
			var tag = Request["fileTag"]?.Trim() ?? "";
			var typ = Request["Type"]?.Trim();
			Int32.TryParse(Request["remainOnServer"], out int remainOnServer);

			try
			{
				//
				// GetTempFile will throw FileNotFoundException if the file isn't there
				//
				file = TempFile.GetTempFile(tag, typ);

				//
				// Attempt to indicate a reasonable content-type header for the transfer
				// NOTE: Unsure if 'GetMimeMapping' actually looks at the file header, or if it is based on extension only.
				// The Win32-api has a separate method 'GetMimeTypeFromData(...)'
				// Only mime types actually registered with the server are returned correctly, however.
				//
				var mime = MimeMapping.GetMimeMapping(file.FullName);
				if (!String.IsNullOrWhiteSpace(mime)) Response.ContentType = mime;

				//
				// Indicate file name
				// TODO: supply the name part via parameters
				//
				Response.AddHeader("Content-Disposition", new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
				{
					Size = file.PhysicalFile.Length,
					CreationDate = file.PhysicalFile.CreationTime,
					ModificationDate = file.PhysicalFile.LastWriteTime,
					FileName = file.PhysicalFile.Name,
					FileNameStar = file.PhysicalFile.Name
				}.ToString());

				//
				// TODO:
				// It should be possible to attach a listener to the IO-completion event (that's how we do it in C++/ISAPI)
				// but I'm unsure how it's done in c#/.net at this point.
				// In this implementation: If the file is requested to be purged, the 'finally'-block below will flush the Response stream, 
				// which I guess forces the file to be written immediately in sync mode.?
				//
				Response.TransmitFile(file.FullName);
			}
			catch (FileNotFoundException e)
			{
				Response.Status = "404 File Not Found";
				Response.ContentType = "application/json";
				Util.ToJSON(new { error = new { message = e.Message, filename = Path.GetFileName(e.FileName) } }, Response.Output);
			}
			catch (Exception e)
			{
				//
				// System.Security.SecurityException, UnauthorizedAccessException, etc.
				//
				// TODO: distinguish different scenarios here
				// It's most likely an authN/authZ problem, but since we do not have enough information to
				// create a WWW-Authenticate header, simply return 403
				//
				Response.Status = "403 Forbidden";
				Response.ContentType = "application/json";
				Util.ToJSON(new { error = new { message = e.Message } }, Response.Output);
			}
			finally
			{
				if (0 == remainOnServer)
				{
					// NOTE: the file is purged regardless of any errors
					try
					{
						if (file?.Exists() ?? false)
						{
							Response.Flush(); // Abort async transfer and switch to sequential mode
							file.Delete();
						}
					}
					catch (Exception)
					{
						// Ignore
					}
				}
			}
		}
		public bool IsReusable { get => false; }
	}

}
