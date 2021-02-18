#tool nuget:?package=Cake.Bakery&version=0.5.1
#addin nuget:?package=Cake.FileHelpers&version=3.3.0

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var artifactsDir = MakeAbsolute(Directory("./artifacts"));
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
  .Does(() =>
{
  CleanDirectory(artifactsDir);
  DotNetCoreClean("./gelf4net.sln");
});

Task("Restore-NuGet-Packages")
  .IsDependentOn("Clean")
  .Does(() =>
{
  DotNetCoreRestore("./gelf4net.sln");
});

Task("Build")
  .IsDependentOn("Clean")
  .Does(() =>
{
  DotNetCoreBuild("./gelf4net.sln", new DotNetCoreBuildSettings
  {
    Configuration = configuration,
  });
});

Task("Run-Unit-Tests")
  .IsDependentOn("Build")
  .Does(() =>
{
  var settings = new DotNetCoreTestSettings
  {
    Framework = "net47",
    Configuration = configuration,
    NoBuild = true,
    NoRestore = true,
  };

  var projectFiles = GetFiles("./test/**/*.csproj");
  foreach(var file in projectFiles)
  {
    DotNetCoreTest(file.FullPath, settings);
  }
});


Task("BuildPackage")
  .IsDependentOn("Run-Unit-Tests")
  .Does(() =>
{
  var settings = new DotNetCorePackSettings
  {
    Configuration = configuration,
    OutputDirectory = "./artifacts/"
  };

  DotNetCorePack("gelf4net.sln", settings);
});

Task("PushToNuget")
  .IsDependentOn("BuildPackage")
  .Does(() =>
{
    var apiKey = FileReadLines(File("./private/nugetapikey.txt"))[0];

    var settings = new NuGetPushSettings
    {
        Source = "https://api.nuget.org/v3/index.json",
        ApiKey = apiKey,
        Verbosity = NuGetVerbosity.Detailed,
    };

    var files = GetFiles("./artifacts/*.nupkg");
    foreach(var file in files)
    {
        Information("File: {0}", file.FullPath);
        NuGetPush(file.FullPath, settings);
    }
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
  .IsDependentOn("Run-Unit-Tests");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

