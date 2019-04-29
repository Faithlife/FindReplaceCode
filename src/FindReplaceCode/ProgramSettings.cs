using System.Collections.Generic;

namespace FindReplaceCode
{
	public static class ProgramSettings
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
			".js",
			".json",
			".md",
			".nuspec",
			".props",
			".proto",
			".ps1",
			".py",
			".settings",
			".sln",
			".ts",
			".xaml",
			".yml",
		};
	}
}
