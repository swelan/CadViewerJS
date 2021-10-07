using System;
using System.IO;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CadViewer.HttpHandler.Legacy
{
	/// <summary>
	/// Port of the legacy "AppendFile" handler. This is a dangerous function, and should only be used
	/// if CadViewer.ClientAppPath is configured as a rewrite-route. See examples.
	/// </summary>
	[Obsolete("Legacy API port")]
	public class SaveOrAppendFile : IHttpHandler
	{
		public virtual void ProcessRequest(HttpContext Context)
		{
			//
			// Create a file with the client-provided filename in the temporary folder
			// If the file already exists, append to the file
			//
			var Request = Context.Request;
			var Response = Context.Response;

			var filename = Request["file"]?.Trim();
			var content = Request["file_content"] ?? "";
			object result = null;

			Response.ContentType = "application/json";
			Response.Charset = "UTF-8";

			try
			{
				TempFile file = null;
				try
				{
					// Attempt to append to existing
					file = TempFile.GetNamedTempFile(filename);
					if (!String.IsNullOrEmpty(content))
					{
						File.AppendAllText(file.FullName, content);
					}
				}
				catch (FileNotFoundException)
				{
					// Create a new file
					file = TempFile.CreateNamedTempFile(content, filename);
				}
				bool success = file?.Exists() ?? false;
				result = new
				{
					success = success,
					tag = Util.GetFileNameWithoutExtension(file?.PhysicalFile?.Name),
					type = Util.GetFileExtension(file?.PhysicalFile?.Name),
					length = file?.PhysicalFile?.Length
				};
			}
			catch (Exception e)
			{
				result = new
				{
					success = false,
					tag = Util.GetFileNameWithoutExtension(filename),
					type = Util.GetFileNameWithoutExtension(filename),
					error = new
					{
						type = e.GetType().FullName,
						message = e.Message
					}
				};
			}
			Util.ToJSON(result, Response.Output);
		}
		public bool IsReusable { get => false; }
	}
}
