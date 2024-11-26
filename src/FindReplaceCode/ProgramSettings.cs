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
			".cshtml",
			".csproj",
			".css",
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
			".razor",
			".settings",
			".sln",
			".ts",
			".xaml",
			".yaml",
			".yml",
		};
	}
}
