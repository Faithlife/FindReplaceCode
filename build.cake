#addin Cake.Git
#addin Cake.XmlDocMarkdown&version=1.4.1

using System.Text.RegularExpressions;

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var nugetApiKey = Argument("nugetApiKey", "");
var trigger = Argument("trigger", "");
var versionSuffix = Argument("versionSuffix", "");

var solutionFileName = "FindReplaceCode.sln";

var nugetSource = "https://api.nuget.org/v3/index.json";
var buildBotUserName = "faithlifebuildbot";
var buildBotPassword = EnvironmentVariable("BUILD_BOT_PASSWORD");

Task("Clean")
	.Does(() =>
	{
		CleanDirectories("src/**/bin");
		CleanDirectories("src/**/obj");
		CleanDirectories("release");
	});

Task("Restore")
	.Does(() =>
	{
		DotNetCoreRestore(solutionFileName);
	});

Task("Build")
	.IsDependentOn("Restore")
	.Does(() =>
	{
		DotNetCoreBuild(solutionFileName, new DotNetCoreBuildSettings { Configuration = configuration, NoRestore = true, ArgumentCustomization = args => args.Append("--verbosity normal") });
	});

Task("Rebuild")
	.IsDependentOn("Clean")
	.IsDependentOn("Build");

Task("NuGetPackage")
	.IsDependentOn("Rebuild")
	.Does(() =>
	{
		if (string.IsNullOrEmpty(versionSuffix) && !string.IsNullOrEmpty(trigger))
			versionSuffix = Regex.Match(trigger, @"^v[^\.]+\.[^\.]+\.[^\.]+-(.+)").Groups[1].ToString();
		foreach (var projectPath in GetFiles("src/**/*.csproj").Select(x => x.FullPath))
			DotNetCorePack(projectPath, new DotNetCorePackSettings { Configuration = configuration, NoBuild = true, NoRestore = true, OutputDirectory = "release", VersionSuffix = versionSuffix });
	});

Task("NuGetPublish")
	.IsDependentOn("NuGetPackage")
	.Does(() =>
	{
		var nupkgPaths = GetFiles("release/*.nupkg").Select(x => x.FullPath).ToList();

		string version = null;
		foreach (var nupkgPath in nupkgPaths)
		{
			string nupkgVersion = Regex.Match(nupkgPath, @"\.([^\.]+\.[^\.]+\.[^\.]+)\.nupkg$").Groups[1].ToString();
			if (version == null)
				version = nupkgVersion;
			else if (version != nupkgVersion)
				throw new InvalidOperationException($"Mismatched package versions '{version}' and '{nupkgVersion}'.");
		}

		if (!string.IsNullOrEmpty(nugetApiKey) && (trigger == null || Regex.IsMatch(trigger, "^v[0-9]")))
		{
			if (trigger != null && trigger != $"v{version}")
				throw new InvalidOperationException($"Trigger '{trigger}' doesn't match package version '{version}'.");

			var pushSettings = new NuGetPushSettings { ApiKey = nugetApiKey, Source = nugetSource };
			foreach (var nupkgPath in nupkgPaths)
				NuGetPush(nupkgPath, pushSettings);
		}
		else
		{
			Information("To publish this package, push this git tag: v" + version);
		}
	});

Task("Default")
	.IsDependentOn("Build");

void ExecuteProcess(string exePath, string arguments)
{
	if (IsRunningOnUnix())
	{
		arguments = exePath + " " + arguments;
		exePath = "mono";
	}
	int exitCode = StartProcess(exePath, arguments);
	if (exitCode != 0)
		throw new InvalidOperationException($"{exePath} failed with exit code {exitCode}.");
}

RunTarget(target);
