using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FindReplaceCode
{
	public sealed class Program
	{
		public Program(string[] args)
		{
			if (args.Length < 3)
				throw new ProgramException(s_fullUsageMessage);
			if (args.Length % 2 != 1)
				throw new ProgramException("Missing <folder-path>, or the last <search> is missing its <replace>.");

			m_folderPath = Path.GetFullPath(args[0]);

			var searchReplaceArgs = new List<KeyValuePair<string, string>>();
			for (int index = 1; index < args.Length; index += 2)
				searchReplaceArgs.Add(new KeyValuePair<string, string>(args[index], args[index + 1]));
			m_searchReplaceArgs = searchReplaceArgs.AsReadOnly();
		}

		public void Run()
		{
			if (!Directory.Exists(m_folderPath))
				throw new ProgramException("<folder-path> does not exist: " + m_folderPath);

			if (m_searchReplaceArgs.Any(x => string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Value)))
				throw new ProgramException("Neither <search> nor <replace> can be blank.");

			DirectoryInfo folderInfo = new DirectoryInfo(m_folderPath);
			var infos = folderInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
				.Where(x => !ShouldIgnoreFileSystemInfo(x)).ToList();

			// enhance search/replace pairs
			m_searchReplacePairs = new List<KeyValuePair<string, string>>();
			foreach (var searchReplacePair in m_searchReplaceArgs)
				m_searchReplacePairs.AddRange(GetEnhancedFindReplacePairs(searchReplacePair));

			var oldGuids = new HashSet<Guid>();

			// create new GUIDs for csproj files that will be renamed
			foreach (var csprojFile in infos.OfType<FileInfo>().Where(x => x.Extension == ".csproj"))
			{
				string oldName = csprojFile.Name;
				if (oldName != ReplaceStrings(oldName))
				{
					string csprojFileText = File.ReadAllText(csprojFile.FullName);
					var projectRegex = new Regex(@"^\s*<ProjectGuid>(" + c_guidPattern + @")", RegexOptions.CultureInvariant | RegexOptions.Multiline);
					foreach (var oldGuid in projectRegex.Matches(csprojFileText).Cast<Match>().Select(x => Guid.Parse(x.Groups[1].ToString())))
						oldGuids.Add(oldGuid);
				}
			}

			// calculate new GUIDs
			m_searchReplaceGuids = oldGuids.Select(x => new KeyValuePair<Regex, Guid>(CreateRegexForGuid(x), Guid.NewGuid())).ToList();

			FindReplace(infos, doIt: false);
			Console.WriteLine();

			if (m_editCount != 0 || m_renameCount != 0)
			{
				string backupFolderPath;
				int backupFolderSuffix = 0;
				while (true)
				{
					backupFolderPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(m_folderPath) + (backupFolderSuffix == 0 ? "" : ("-" + backupFolderSuffix)));
					if (!Directory.Exists(backupFolderPath) && !File.Exists(backupFolderPath))
						break;
					backupFolderSuffix++;
				}

				Console.WriteLine("WARNING! This operation will EDIT {0} files and RENAME {1} files and folders.", m_editCount, m_renameCount);
				Console.WriteLine("The folder will first be backed up to: {0}", backupFolderPath);
				Console.Write("ARE YOU SURE you want to do this? Type yes if you dare: ");

				string confirmation = Console.ReadLine();
				Console.WriteLine();

				if (confirmation == "yes")
				{
					Console.WriteLine("Backing up files to: {0}", backupFolderPath);
					Console.WriteLine();

					var backupFolderInfo = new DirectoryInfo(backupFolderPath);
					backupFolderInfo.Create();
					CopyFilesRecursively(folderInfo, backupFolderInfo);

					bool success = false;
					try
					{
						FindReplace(infos, doIt: true);
						success = true;
					}
					finally
					{
						Console.WriteLine();
						Console.WriteLine("{0}! Backup location: {1}", success ? "DONE" : "FAILED", backupFolderPath);
					}
				}
				else
				{
					Console.WriteLine("Aborted. No files or directories were edited or renamed.");
				}
			}
			else
			{
				Console.WriteLine("Nothing to do. The search string(s) were not found.");
			}
		}

		public static void CopyFilesRecursively(DirectoryInfo sourceDirectory, DirectoryInfo targetDirectory)
		{
			foreach (DirectoryInfo directory in sourceDirectory.GetDirectories())
				CopyFilesRecursively(directory, targetDirectory.CreateSubdirectory(directory.Name));
			foreach (FileInfo file in sourceDirectory.GetFiles())
				file.CopyTo(Path.Combine(targetDirectory.FullName, file.Name));
		}

		private IEnumerable<KeyValuePair<string, string>> GetEnhancedFindReplacePairs(KeyValuePair<string, string> searchReplacePair)
		{
			yield return searchReplacePair;

			KeyValuePair<string, string>? enhancedKeyValuePair;

			enhancedKeyValuePair = TryGetEnhancedFindReplacePairs(searchReplacePair, Capitalize);
			if (enhancedKeyValuePair != null)
				yield return enhancedKeyValuePair.Value;

			enhancedKeyValuePair = TryGetEnhancedFindReplacePairs(searchReplacePair, Uncapitalize);
			if (enhancedKeyValuePair != null)
				yield return enhancedKeyValuePair.Value;

			enhancedKeyValuePair = TryGetEnhancedFindReplacePairs(searchReplacePair, x => x.ToLowerInvariant());
			if (enhancedKeyValuePair != null)
				yield return enhancedKeyValuePair.Value;

			enhancedKeyValuePair = TryGetEnhancedFindReplacePairs(searchReplacePair, x => x.ToUpperInvariant());
			if (enhancedKeyValuePair != null)
				yield return enhancedKeyValuePair.Value;

			enhancedKeyValuePair = TryGetEnhancedFindReplacePairs(searchReplacePair, x => ToSnakeCase(x, '_'));
			if (enhancedKeyValuePair != null)
				yield return enhancedKeyValuePair.Value;

			enhancedKeyValuePair = TryGetEnhancedFindReplacePairs(searchReplacePair, x => ToSnakeCase(x, '-'));
			if (enhancedKeyValuePair != null)
				yield return enhancedKeyValuePair.Value;
		}

		private static KeyValuePair<string, string>? TryGetEnhancedFindReplacePairs(KeyValuePair<string, string> searchReplacePair, Func<string, string> transform)
		{
			string newKey = transform(searchReplacePair.Key);
			return newKey != searchReplacePair.Key ? new KeyValuePair<string, string>(newKey, transform(searchReplacePair.Value)) : default(KeyValuePair<string, string>?);
		}

		private static string Capitalize(string text)
		{
			return text.Substring(0, 1).ToUpperInvariant() + text.Substring(1);
		}

		private static string Uncapitalize(string text)
		{
			return text.Substring(0, 1).ToLowerInvariant() + text.Substring(1);
		}

		public static void Main(string[] args)
		{
			try
			{
				new Program(args).Run();
			}
			catch (ProgramException exception)
			{
				Console.Error.WriteLine(exception.Message);
			}
			catch (Exception exception)
			{
				Console.Error.WriteLine(exception.ToString());
			}
		}

		private void FindReplace(IReadOnlyCollection<FileSystemInfo> infos, bool doIt)
		{
			m_editCount = 0;
			m_renameCount = 0;

			foreach (FileInfo info in infos.OfType<FileInfo>())
			{
				if (ShouldFindReplaceFileContent(info))
				{
					bool hasBOM;
					using (var stream = File.OpenRead(info.FullName))
						hasBOM = stream.ReadByte() == 0xEF && stream.ReadByte() == 0xBB && stream.ReadByte() == 0xBF;

					string oldContent = File.ReadAllText(info.FullName);

					string newContent = ReplaceGuids(ReplaceStrings(oldContent));

					if (oldContent != newContent)
					{
						Console.WriteLine("{0} [edit]", info.FullName);
						m_editCount++;

						if (doIt)
							File.WriteAllText(info.FullName, newContent, new UTF8Encoding(hasBOM));
					}
				}
			}

			foreach (FileInfo info in infos.OfType<FileInfo>())
			{
				string oldName = info.Name;
				string newName = ReplaceStrings(oldName);
				if (oldName != newName)
				{
					Console.WriteLine("{0} => {1}", info.FullName, Path.GetFileName(newName));
					m_renameCount++;

					string newPath = Path.Combine(Path.GetDirectoryName(info.FullName), newName);
					if (File.Exists(newPath) || Directory.Exists(newPath))
						throw new ProgramException(string.Format(CultureInfo.InvariantCulture, "{0} already exists!", newPath));

					if (doIt)
						info.MoveTo(newPath);
				}
			}

			foreach (DirectoryInfo info in infos.OfType<DirectoryInfo>())
			{
				string oldName = info.Name;
				string newName = ReplaceStrings(oldName);
				if (oldName != newName)
				{
					Console.WriteLine("{0} => {1}", info.FullName, Path.GetFileName(newName));
					m_renameCount++;

					string newPath = Path.Combine(Path.GetDirectoryName(info.FullName), newName);
					if (File.Exists(newPath) || Directory.Exists(newPath))
						throw new ProgramException(string.Format(CultureInfo.InvariantCulture, "{0} already exists!", newPath));

					if (doIt)
						info.MoveTo(newPath);
				}
			}
		}

		private static bool ShouldIgnoreFileSystemInfo(FileSystemInfo info)
		{
			// omit directories that start with a period
			return s_hiddenDirectoryRegex.IsMatch(info.FullName);
		}

		private static bool ShouldFindReplaceFileContent(FileSystemInfo info)
		{
			if (!(info is FileInfo))
				return false;

			string extension = Path.GetExtension(info.Name).ToLowerInvariant();
			return ProgramSettings.FindReplaceFileContentExtensions.Contains(extension);
		}

		private string ReplaceStrings(string oldText)
		{
			string newText = oldText;
			foreach (var searchReplacePair in m_searchReplacePairs)
				newText = newText.Replace(searchReplacePair.Key, searchReplacePair.Value);
			return newText;
		}

		private string ReplaceGuids(string oldText)
		{
			string newText = oldText;
			foreach (var projectGuid in m_searchReplaceGuids)
				newText = projectGuid.Key.Replace(newText, x => RenderMatchingGuid(x, projectGuid.Value));
			return newText;
		}

		private static Regex CreateRegexForGuid(Guid guid)
		{
			return new Regex(Regex.Escape(guid.ToString()), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
		}

		private static string RenderMatchingGuid(Match match, Guid value)
		{
			string oldText = match.ToString();
			string newText = value.ToString();
			return oldText != oldText.ToUpperInvariant() ? newText : newText.ToUpperInvariant();
		}

		private static string ToSnakeCase(string value, char separator)
		{
			if (value == null)
				return null;

			var words = GetWords(value);
			return string.Join(separator.ToString(), words.Select(x => x.ToLowerInvariant()));
		}

		private static string[] GetWords(string value) => s_word.Matches(value).Select(x => x.ToString()).ToArray();

		private const string c_guidPattern = @"\{[0-9a-zA-Z]{8}-[0-9a-zA-Z]{4}-[0-9a-zA-Z]{4}-[0-9a-zA-Z]{4}-[0-9a-zA-Z]{12}\}";

		private static readonly string s_fullUsageMessage = string.Join(Environment.NewLine, new[]
		{
			"Usage: FindReplaceCode.exe <folder-path> <find> <replace> [<find> <replace> ...]"
		});

		private static readonly Regex s_hiddenDirectoryRegex = new Regex(@"[\\/]\..*[\\/]", RegexOptions.CultureInvariant);
		private static readonly Regex s_word = new Regex("[A-Z]([A-Z]*(?![a-z])|[a-z]*)|[a-z]+|[0-9]+", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

		private readonly string m_folderPath;
		private readonly ReadOnlyCollection<KeyValuePair<string, string>> m_searchReplaceArgs;
		private List<KeyValuePair<string, string>> m_searchReplacePairs;
		private List<KeyValuePair<Regex, Guid>> m_searchReplaceGuids;
		private int m_editCount;
		private int m_renameCount;
	}
}
