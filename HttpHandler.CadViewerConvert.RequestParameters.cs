using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace CadViewer.HttpHandler
{
	public partial class CadViewerConvert
	{
		/// <summary>
		/// Represents input parameters for the conversion method
		/// </summary>
		public sealed class RequestParameters
		{
			public sealed class Parameter
			{
				//
				// 'parameterName': only allow letter/digit
				//
				private string _paramName = null;
				public string paramName
				{
					get => _paramName;
					set
					{
						_paramName = value?.Normalize().Trim().ToLowerInvariant();
						//
						// only emit valid [A-Za-z0-9] characters from the input, no control, punctuation or fancy characters
						// Example:

						//_paramName = new string(value?.Normalize().Trim().ToLowerInvariant().Where(c => (c < 128) && Char.IsLetterOrDigit(c)).ToArray());

						/*
						//
						// Throw exception on malformed input
						//
						if (value?.Any(c => !Char.IsLetterOrDigit(c)) ?? false)
						{
							//throw new ArgumentOutOfRangeException("paramName", value, "The argument contains invalid characters outside the range [a-zA-Z0-9]");
							value = null;
						}
						_paramName = value?.ToLowerInvariant().Normalize();
						*/
					}
				}

				//
				// 'parameterValue': allow everything except control, '"' and vertical whitespace
				//
				private object _paramValue = null;
				public object paramValue
				{
					get => _paramValue;
					set
					{
						if ((null != value) && (value is string))
						{
							// Strip control and normalize whitespace
							string tmp = new string(value?.ToString().Normalize().Trim().Select(c => (Char.IsControl(c) || Char.IsSeparator(c)) ? ' ' : c).ToArray());
							value = Regex.Replace(tmp ?? "", @"\s+", " ");
						}
						_paramValue = value;
					}
				}
			}
			public string action { get; set; }
			public string contentType { get; set; }
			public string contentFormat { get; set; }
			public string contentLocation { get; set; }
			public IList<Parameter> parameters { get; set; } = new List<Parameter>();

			//
			// Methods
			//
			public Parameter SetParameter(string Index, object Value)
			{
				var v = GetParameter(Index);
				if (null != v)
				{
					v.paramValue = Value;
				}
				else if (!String.IsNullOrWhiteSpace(Index))
				{
					parameters.Add(v = new Parameter { paramName = Index, paramValue = Value });
				}
				return v;
			}
			public Parameter GetParameter(string Index)
			{
				return parameters?.Where(x => x.paramName?.Equals(Index, StringComparison.OrdinalIgnoreCase) ?? false).DefaultIfEmpty(null).First() ?? null;
			}
			public object GetParameterValue(string Index)
			{
				return GetParameter(Index)?.paramValue ?? null;
			}

			//
			// JSON convenience methods
			//
			public string ToJSON() => Util.ToJSON(this);
			public static RequestParameters FromJSON(string json) => Util.FromJSON<RequestParameters>(json);
			public static RequestParameters FromJSON(System.IO.TextReader Reader) => Util.FromJSON<RequestParameters>(Reader);
		}
	}
}