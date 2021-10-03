using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CadViewer
{
	/// <summary>
	/// Holder of HTTP authentication parameters to be relayed to an origin server.
	/// </summary>
	public class AuthorizationContext
	{
		public AuthorizationContext(string BearerToken = null, string Username = null, string Password = null, string FormCookie = null)
		{
			this.BearerToken = BearerToken;
			this.Username = Username;
			this.Password = Password;
			this.FormCookie = FormCookie;
		}
		public string BearerToken { get; set; } = null;
		public string Username { get; set; } = null;
		public string Password { get; set; } = null;
		public string FormCookie { get; set; } = null;

		/// <summary>
		/// Generate suitable Authorization headers for use with a HTTP request.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<KeyValuePair<string, string>> ToHttpHeaders()
		{
			var result = new List<KeyValuePair<string, string>>();
			if (!String.IsNullOrEmpty(BearerToken))
			{
				//
				// NOTE: the 'Bearer' scheme does not define what a 'BearerToken' contains; it is determined by the implementation.
				// It could, for instance, consist of a base64-encoded signed JWT.
				// This implementation assumes that the user of this library provides a pre-encoded token in accordance with RFC2616.
				//
				result.Add(new KeyValuePair<string, string>("Authorization", $"Bearer {BearerToken}"));
			}
			else if (!String.IsNullOrEmpty(Username))
			{
				//
				// Assume UTF8-encoding. This implementation assumes that the server accepts the Basic scheme in UTF8-encoding as indicated 
				// by the user of this library.
				//
				// General case: the request should be submitted and upon receiving '401 Not Authorized', the Authorization header should
				// be constructed based on the WWW-Authenticate-header's 'charset' parameter returned by the server. The request should then
				// be replayed using the constructed 'Authorization' header.
				//
				result.Add(new KeyValuePair<string, string>("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Password ?? ""}"))}"));
			}
			if (!String.IsNullOrEmpty(FormCookie))
			{
				result.Add(new KeyValuePair<string, string>("Cookie", FormCookie));
			}
			return result;
		}
	}
}
