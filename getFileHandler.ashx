<%@ WebHandler Language="C#" Class="Handler" %>
using CadViewer;
using System.Web;

public class Handler : CadViewer.HttpHandler.Download
{
	public override void ProcessRequest(HttpContext Context)
	{
		base.ProcessRequest(Context);
	}
}
