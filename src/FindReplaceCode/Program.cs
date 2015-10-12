using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FindReplaceCode.Properties;

namespace FindReplaceCode
{
	public sealed class Program
	{
		public Program(string[] args)
		{
			if (args.Length == 0)
				throw new ProgramException(s_fullUsageMessage);
			if (args.Length % 2 != 1)
				throw new ProgramException("The last <search> is missing its <replace>.");

			m_folderPath = Path.Combine(Environment.CurrentDirectory, args[0]);

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

			var infos = new DirectoryInfo(m_folderPath).EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
				.Where(x => !ShouldIgnoreFileSystemInfo(x)).ToList();

			var oldGuids = new HashSet<Guid>();

			// find GUIDs that need replacing
			foreach (var slnFile in infos.OfType<FileInfo>().Where(x => x.Extension == ".sln"))
			{
				string slnFileText = File.ReadAllText(slnFile.FullName);
				var projectRegex = new Regex(@"^\s*Project\(.*""(" + c_guidPattern + @")""\r?$", RegexOptions.CultureInvariant | RegexOptions.Multiline);
				foreach (var oldGuid in projectRegex.Matches(slnFileText).Cast<Match>().Select(x => Guid.Parse(x.Groups[1].ToString())))
					oldGuids.Add(oldGuid);
			}
			foreach (var csprojFile in infos.OfType<FileInfo>().Where(x => x.Extension == ".csproj"))
			{
				string csprojFileText = File.ReadAllText(csprojFile.FullName);
				var projectRegex = new Regex(@"^\s*<ProjectGuid>(" + c_guidPattern + @")", RegexOptions.CultureInvariant | RegexOptions.Multiline);
				foreach (var oldGuid in projectRegex.Matches(csprojFileText).Cast<Match>().Select(x => Guid.Parse(x.Groups[1].ToString())))
					oldGuids.Add(oldGuid);
			}

			// enhance search/replace pairs
			m_searchReplacePairs = new List<KeyValuePair<string, string>>();
			foreach (var searchReplacePair in m_searchReplaceArgs)
				m_searchReplacePairs.AddRange(GetEnhancedFindReplacePairs(searchReplacePair));

			// calculate new GUIDs
			m_searchReplaceGuids = oldGuids.Select(x => new KeyValuePair<Regex, Guid>(CreateRegexForGuid(x), Guid.NewGuid())).ToList();

			FindReplace(infos, doIt: false);

			Console.WriteLine();
			Console.WriteLine("WARNING! This operation will EDIT {0} files and RENAME {1} files and directories.", m_editCount, m_renameCount);
			Console.Write("ARE YOU SURE you want to do this? (y/n) ");

			string confirmation = Console.ReadLine();
			if (confirmation == null || confirmation.ToLowerInvariant() != "y")
				return;
			Console.WriteLine();

			FindReplace(infos, doIt: true);
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

					string newContent = ReplaceGuids(ReplaceStrings(oldContent, m_searchReplacePairs), m_searchReplaceGuids);

					if (oldContent != newContent)
					{
						Console.WriteLine("Find and replace in {0}.", info.FullName);
						m_editCount++;

						if (doIt)
							File.WriteAllText(info.FullName, newContent, new UTF8Encoding(hasBOM));
					}
				}

				string oldName = info.Name;
				string newName = ReplaceStrings(oldName, m_searchReplacePairs);
				if (oldName != newName)
				{
					Console.WriteLine("Rename {0} to {1}.", info.FullName, newName);
					m_renameCount++;

					if (doIt)
						info.MoveTo(Path.Combine(Path.GetDirectoryName(info.FullName), newName));
				}
			}

			foreach (DirectoryInfo info in infos.OfType<DirectoryInfo>())
			{
				string oldName = info.Name;
				string newName = ReplaceStrings(oldName, m_searchReplacePairs);
				if (oldName != newName)
				{
					Console.WriteLine("Rename {0} to {1}.", info.FullName, newName);
					m_renameCount++;

					if (doIt)
						info.MoveTo(Path.Combine(Path.GetDirectoryName(info.FullName), newName));
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
			return s_findReplaceFileContentExtensions.Contains(extension);
		}

		private static string ReplaceStrings(string oldText, IEnumerable<KeyValuePair<string, string>> m_searchReplacePairs)
		{
			string newText = oldText;
			foreach (var searchReplacePair in m_searchReplacePairs)
				newText = newText.Replace(searchReplacePair.Key, searchReplacePair.Value);
			return newText;
		}

		private static string ReplaceGuids(string oldText, IEnumerable<KeyValuePair<Regex, Guid>> m_searchReplaceGuids)
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

		const string c_guidPattern = @"\{[0-9a-zA-Z]{8}-[0-9a-zA-Z]{4}-[0-9a-zA-Z]{4}-[0-9a-zA-Z]{4}-[0-9a-zA-Z]{12}\}";

		static readonly string s_fullUsageMessage = string.Join(Environment.NewLine, new[]
		{
			"Usage: FindReplaceCode.exe <folder-path> [<find> <replace> ...]"
		});

		static readonly HashSet<string> s_findReplaceFileContentExtensions =
			new HashSet<string>(Settings.Default.FindReplaceFileContentExtensions.Split(',').Select(x => "." + x.Trim()));

		static readonly Regex s_hiddenDirectoryRegex = new Regex(@"[\\/]\..*[\\/]", RegexOptions.CultureInvariant);

		readonly string m_folderPath;
		readonly ReadOnlyCollection<KeyValuePair<string, string>> m_searchReplaceArgs;
		List<KeyValuePair<string, string>> m_searchReplacePairs;
		List<KeyValuePair<Regex, Guid>> m_searchReplaceGuids;
		int m_editCount;
		int m_renameCount;
	}
}
