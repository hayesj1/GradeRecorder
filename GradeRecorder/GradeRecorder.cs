using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

// ReSharper disable HeapView.ObjectAllocation.Evident
// ReSharper disable HeapView.ObjectAllocation.Possible
namespace GradeRecorder {
	public sealed class GradeRecorder {
		private Configuration config;

		private StreamReader gradesFile;
		private FileStream outputFile;

		public GradeRecorder(string oldPath) { config = new Configuration(oldPath); }

		private bool run() {
			var grades = new OpenFileDialog{
				                               Title = "Choose the File Containing the Grades:",
				                               InitialDirectory = $"C:\\Users\\{Environment.UserName}\\Documents\\Grade-Recorder\\Grades",
				                               CheckFileExists = true,
				                               CheckPathExists = true,
				                               Filter = "SpreadSheets (*.xlsx, *.cvs, *.txt)|*.xml, *.csv, *.txt",
				                               DereferenceLinks = true
				                               };

			DialogResult result = grades.ShowDialog();
			if (result != DialogResult.OK) { return false; }

			gradesFile = File.OpenText(grades.FileName);
			outputFile = File.Create(config.OutputPath);

			//readGrades();
			computeGPAs();
			//writeGPAs();

			gradesFile.Dispose();
			outputFile.Dispose();

			return true;
		}

		private void computeGPAs() { throw new NotImplementedException(); }

		public static void Main(string[] args) {
			var lastConfigPath = string.Empty;
			if (args.Length == 1) { lastConfigPath = args[0]; }

			var recorder = new GradeRecorder(lastConfigPath);
			var success = recorder.run();
			Console.WriteLine($"Successful? {(success ? "Yes" : "No")}");
		}
	}

	internal sealed class Configuration {
		public static string DEFAULT_CONFIG_PATH => $"C:\\Users\\{Environment.UserName}\\Documents\\Grade-Recorder";

		public static string DEFAULT_OUTPUT_FILENAME => "computed-grades";
		public static string DEFAULT_OUTPUT_FILEEXT => ".xlsx";
		public static string DEFAULT_OUTPUT_FILE => string.Concat(DEFAULT_OUTPUT_FILENAME, DEFAULT_OUTPUT_FILEEXT);

		public static string CONFIG_FILENAME => "configuration";
		public static string CONFIG_FILEEXT => ".xml";
		public static string CONFIG_FILE => string.Concat(CONFIG_FILENAME, CONFIG_FILEEXT);

		public static string[] VALID_OUTPUT_EXTS => new[] { ".xml", ".csv", ".txt"};

		private string configFile;
		private string configPath;
		private string outputFileExt;

		public string OutputFileName { get; set; }
		public string OutputFileExt {
			get { return outputFileExt; }
			set {
				if (!value.ElementAt(0).Equals('.')) { return; }

				if (!value.Equals(DEFAULT_OUTPUT_FILEEXT)) {
					var valid = false;
					foreach (var ext in VALID_OUTPUT_EXTS) {
						valid = value.Equals(ext);
						if (valid) { break; }
					}

					if (!valid) { return; }
				}

				outputFileExt = value;
			}
		}

		public string OutputFile => $"{OutputPath}\\{string.Concat(OutputFileName, OutputFileExt)}";
		public string ConfigFile => $"{configPath}\\{configFile}";

		public string GradesPath { get; private set; }
		public string OutputPath { get; private set; }

		internal Configuration(string oldPath) {
			configFile = CONFIG_FILE;
			configPath = DEFAULT_CONFIG_PATH;

			GetConfigPath(oldPath);

			var corrupted = !LoadConfig();
			if (corrupted) { throw new CorruptedConfigException(); }
		}

		private void GetConfigPath(string old) {
			if (!old.Equals(string.Empty)) {
				configPath = old;
				return;
			}

			var file = new OpenFileDialog
				           {
				           Title = "Choose a folder for the configuration file or the old configuration file itself:",
				           InitialDirectory = DEFAULT_CONFIG_PATH,
				           CheckFileExists = true,
				           CheckPathExists = true,
				           Filter = "XML files (*.xml)|*.xml",
				           DereferenceLinks = true
				           };
			DialogResult result = file.ShowDialog();
			if (result == DialogResult.OK) {
				if (!Directory.Exists(file.FileName) && !File.Exists(file.FileName)) {
					configFile = CONFIG_FILE;
					configPath = DEFAULT_CONFIG_PATH;
				} else if (Directory.Exists(file.FileName)) {
					configFile = CONFIG_FILE;
					configPath = file.FileName;
				} else if (File.Exists(file.FileName)) {
					Debug.Assert(file.SafeFileName != null, message : "file.SafeFileName != null");
					var ext = file.SafeFileName.Substring(file.SafeFileName.LastIndexOf('.'));

					configFile = ext.Equals(CONFIG_FILEEXT) ? file.SafeFileName : CONFIG_FILE;
					configPath = Path.GetDirectoryName(file.FileName);
				}
			} else {
				if (!Directory.Exists(configPath)) { Directory.CreateDirectory(configPath); }
				if (!File.Exists(ConfigFile)) { File.Create(ConfigFile); }
			}
		}

		internal bool LoadConfig() {
			var cfgFile = new ConfigXmlDocument();
			cfgFile.Load($"{configPath}\\{CONFIG_FILE}");

			var integrity = validateConfigIntegrity(cfgFile);
			if (!integrity) { regenConfig(); }

			XmlNodeList settingNodes = cfgFile.SelectNodes(xpath : "//Config/Setting");


			foreach (XmlNode node in settingNodes) {
				var setting = node.SelectSingleNode(xpath : "Name")?.InnerText;
				var value = node.SelectSingleNode(xpath : "Value")?.InnerText ?? string.Empty;
				switch (setting) {
					case "outputFileName":
						OutputFileName = value.Equals(string.Empty) ? DEFAULT_OUTPUT_FILENAME : value;
						break;
					case "outputFileExt":
						OutputFileExt = value.Equals(string.Empty) ? DEFAULT_OUTPUT_FILEEXT : value;
						break;

					case "gradesPath":
						GradesPath = value;
						break;
					case "outputPath":
						OutputPath = value;
						break;
					default:
						continue;
				}
			}

			return true;
		}

		private bool validateConfigIntegrity(ConfigXmlDocument cfgFile) {
			var valid = true;

			cfgFile.

			return valid;
		}

		internal bool SaveConfig() {
			var cfgFile = new ConfigXmlDocument();
			cfgFile.Load($"{configPath}\\{CONFIG_FILE}");
			XmlNodeList settingNodes = cfgFile.SelectNodes(xpath : "//Config/Setting");

			if (settingNodes == null) return false;

			foreach (XmlNode node in settingNodes) {
				var setting = node.SelectSingleNode(xpath : "Name")?.InnerText;
				string value;

				switch (setting) {
					case "outputFileName":
						value = OutputFileName.Equals(string.Empty) ? DEFAULT_OUTPUT_FILENAME : OutputFileName;
						break;
					case "outputFileExt":
						value = OutputFileExt.Equals(string.Empty) ? DEFAULT_OUTPUT_FILEEXT : OutputFileExt;
						break;

					case "gradesPath":
						value = GradesPath;
						break;
					case "outputPath":
						value = OutputPath;
						break;
					default:
						continue;
				}

				node.InnerText = value;
			}

			return true;
		}

		~Configuration() { SaveConfig(); }
	}

	internal class CorruptedConfigException : Exception {
		public override string Message => "Configuration file is corrupted! DO NOT TAMPER WITH THE CONFIG DIRECTLY!";
	}
}