# FindReplaceCode

Used to generate a new code project from a template.

**WARNING:** This tool is recursively destructive. Please be extremely careful with it.

This tool is installed as a dotnet tool: `dotnet tool install Faithlife.FindReplaceCode.Tool --global`.

## Usage

```
Usage: findreplacecode <folder-path> <find> <replace> [<find> <replace> ...]
```

For example:

```
> findreplacecode C:\Code\MyItemApi MyItem CoolThing
```

### Folder Path

The first argument is the folder path. This tool may edit and/or rename every file and every folder in that folder and all of its subfolders, so be very careful where you point it.

Normally you would point the tool at a folder where you have made a copy of a project or solution that you want to modify.

### Find and Replace

Each pair of arguments after the folder path indicate *text to find* and *text to replace* it with.

The find-and-replace is executed not only on the content of code files, but it is also used to rename files and folders.

This tool also creates new a Visual Studio project GUID for each renamed `.csproj` and automatically substitutes that project GUID in any `.sln` file that references the old one.

The find-and-replace is case-sensitive, but certain case variants are automatically replaced. For example, if you replace "MyItem" with "CoolThing", these replacements will also be executed: "myItem" for "coolThing", "myitem" for "coolthing", and "MYITEM" for "COOLTHING".

If the find text is not sufficiently unique in the existing files, this tool may not work well. For example, replacing `Project` would break Visual Studio project and solution files. This tool isn't *that* smart.

At least one find-and-replace pair is required. Any additional pairs are executed in the order they are provided.

Any subfolder whose name starts with a period (e.g. `.git`) is automatically excluded from modification, along with all of its files and subfolders.

## Issues

Only [certain file extensions](src/FindReplaceCode/ProgramSettings.cs) are currently considered for content replacement. Please let us know if there are others that should be added.

## Build Status

Ubuntu | Windows | NuGet
--- | --- | ---
[![Travis CI](https://img.shields.io/travis/Faithlife/FindReplaceCode/master.svg)](https://travis-ci.org/Faithlife/FindReplaceCode) | [![AppVeyor](https://img.shields.io/appveyor/ci/Faithlife/findreplacecode/master.svg)](https://ci.appveyor.com/project/Faithlife/findreplacecode) | [![NuGet](https://img.shields.io/nuget/v/FindReplaceCode.svg)](https://www.nuget.org/packages/FindReplaceCode)

## Documentation

* License: [MIT](LICENSE)
* [Version History](VersionHistory.md)
* [Contributing Guidelines](CONTRIBUTING.md)
