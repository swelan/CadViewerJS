using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CadViewer
{
	// TODO: find out how to typedef across implementation files
	using ConverterCallback = Action<FileInfo, int, Exception>;
	using ConverterParameters = Dictionary<string, object>;
	using ConverterParameter = KeyValuePair<string, object>;

	public interface IConverter
	{

		FileInfo Input { get; set; }
		string InputFileName { get; set; }
		FileInfo Output { get; }
		string OutputFileName { get; set; }
		string OutputBaseName { get; set; }
		string OutputFormat { get; set; }
		string Action { get; set; }

		ConverterParameters Parameters { get; }
		int ExitCode { get; }
		Exception LastError { get; }

		Task<bool> Execute(ConverterCallback Callback);

	}

	/// <summary>
	/// Abstract Converter; implements IConverter and provides plumbing for generic conversion processes
	/// </summary>
	public abstract class Converter : IConverter
	{
		public Converter()
		{
		}
		~Converter()
		{
		}
		public FileInfo Input { get; set; } = null;
		public string InputFileName
		{
			get => Input?.FullName;
			set => Input = String.IsNullOrEmpty(value) ? null : new FileInfo(value);
		}
		public FileInfo Output { get; protected set; } = null;
		public string OutputFileName
		{
			get => Output?.FullName;
			set => Output = String.IsNullOrEmpty(value) ? null : new FileInfo(value);
		}

		private string _basename = null;
		/// <summary>
		/// Allow external modification of the BaseName part of the OutputFilename
		/// </summary>
		public string OutputBaseName { get => _basename; set => _basename = Util.GetFileNameWithoutExtension(value).OrDefault(null); }

		private string _outputformat = null;
		public string OutputFormat { get => _outputformat; set => _outputformat = value.OrDefault(null); }

		private string _action = null;
		/// <summary>
		/// Can be used to indicate the type of operation to perform
		/// </summary>
		public string Action { get => _action; set { _action = value?.Trim().ToLowerInvariant(); } }

		/// <summary>
		/// Case-insensitive collection for optional command parameters
		/// </summary>
		public ConverterParameters Parameters { get; private set; } = new ConverterParameters(StringComparer.OrdinalIgnoreCase);

		public int ExitCode { get; protected set; } = 0;
		public Exception LastError { get; protected set; } = null;
		protected abstract Task<bool> ExecuteInternal();

		/// <summary>
		/// Execute the converter, optionally with a callback
		/// </summary>
		/// <param name="Callback"></param>
		/// <returns></returns>
		public async Task<bool> Execute(ConverterCallback Callback = null)
		{
			ExitCode = 0;
			LastError = null;

			try
			{
				TempFile.PurgeColdFiles();

				Input?.Refresh();
				if (!(Input?.Exists ?? false)) throw new FileNotFoundException("Invalid input file", Input?.FullName);

				OutputFileName = TempFile.GetRandomFileName(OutputFormat);
				if (!String.IsNullOrWhiteSpace(OutputBaseName))
				{
					OutputFileName = Path.Combine(Output.DirectoryName, $"{OutputBaseName}{Output.Extension}");
				}

				return await ExecuteInternal();
			}
			catch (Exception e)
			{
				LastError = e;
			}
			finally
			{
				//
				// If provided invoke the callback with the result
				//
				try
				{
					Callback?.Invoke(Output, ExitCode, LastError);
				}
				catch (Exception)
				{
					// Ignore, handled by callback
				}
			}
			return false;
		}
	}
}
