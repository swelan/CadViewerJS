<%@ WebHandler Language="C#" Class="Handler" %>

using System.Web;
using CadViewer;

public class Handler : CadViewer.HttpHandler.Conversion
{
	public override void ProcessRequest(HttpContext Context)
	{
		base.ProcessRequest(Context);
	}
}