using System;
using System.IO;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CadViewer.HttpHandler.Legacy
{
	public class Load : IHttpHandler
	{
		public virtual void ProcessRequest(HttpContext Context)
		{
			var Server = Context.Server;
			var Request = Context.Request;
			var Response = Context.Response;

			var filename = Request.QueryString["file"].OrDefault(Request.Form["file"]).OrDefault(null)?.Trim();
			try
			{
				//
				// Allow retrieval of documents located in descendant folders only
				// MakeAppRelativePath throws ArgumentOutOfRange if "to_relative" refers to anything above "./"
				//
				var path = Server.MapPath("~" + Server.MakeAppRelativePath("./", filename));
				Response.ContentType = "text/plain";
				Response.Charset = "UTF-8";
				Response.TransmitFile(path);//.LocalPath);

			}
			catch (Exception e)
			{
				Response.Status = "404 File Not Found";
				Response.ContentType = "application/json";
				Response.Charset = "UTF-8";
				Util.ToJSON(
					new
					{
						success = false,
						error = new
						{
							type = e.GetType().FullName,
							message = e.Message
						}
					}, 
					Response.Output
				);
			}
		}
		public bool IsReusable { get => false; }
	}
}
