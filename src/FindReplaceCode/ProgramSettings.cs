using System.Collections.Generic;

namespace FindReplaceCode
{
	public sealed class ProgramSettings
	{
		public static readonly IReadOnlyCollection<string> FindReplaceFileContentExtensions = new HashSet<string>
		{
			".asax",
			".cake",
			".config",
			".cs",
			".csproj",
			".fsd",
			".html",
			".json",
			".md",
			".nuspec",
			".props",
			".proto",
			".ps1",
			".settings",
			".sln",
			".xaml",
			".yml",
		};
	}
}
