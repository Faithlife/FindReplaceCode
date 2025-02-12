namespace FindReplaceCode;

internal static class ProgramSettings
{
	public static readonly IReadOnlyCollection<string> FindReplaceFileContentExtensions = new HashSet<string>
	{
		".asax",
		".cake",
		".config",
		".cs",
		".cshtml",
		".csproj",
		".css",
		".fsd",
		".html",
		".js",
		".json",
		".md",
		".mysql",
		".nuspec",
		".props",
		".proto",
		".ps1",
		".py",
		".razor",
		".settings",
		".sln",
		".sql",
		".ts",
		".xaml",
		".yaml",
		".yml",
	};
}
