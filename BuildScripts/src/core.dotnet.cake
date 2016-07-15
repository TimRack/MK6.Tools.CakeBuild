
var msBuildSettings = new MSBuildSettings()
                            .SetConfiguration(buildParams.Configuration)
                            .SetVerbosity(Verbosity.Minimal)
                            .UseToolVersion(MSBuildToolVersion.VS2015);

Task("CoreClean")
    .Does(() => 
    {
        using(TeamCity.BuildBlock("Executing CoreClean..."))
        {
            MSBuild(buildParams.SolutionPath, msBuildSettings.WithTarget("Clean"));
            DeleteFiles("./*.nupkg");
        }
    });

Task("CoreRestoreNuGetPackages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    if(HasEnvironmentVariable("NUGET_SOURCES")) 
    {
        var sources = new List<string>(EnvironmentVariable("NUGET_SOURCES").Split(';'));
        NuGetRestore(buildParams.SolutionPath, new NuGetRestoreSettings { Source = sources });
    }
    else
        NuGetRestore(buildParams.SolutionPath);
    
});

Task("CoreBuild")
    .IsDependentOn("RestoreNuGetPackages")
    .Does(() => MSBuild(buildParams.SolutionPath, msBuildSettings.WithTarget("Build")));

Task("CorePackage")
    .IsDependentOn("Build")
    .Does(() =>
{
    var parsedSolution = ParseSolution(buildParams.SolutionPath);
    var nuspecs = GetFiles("./**/*.nuspec", fi => parsedSolution.Projects.Any(p => fi.Path.FullPath.EndsWith(p.Name)));
    foreach(var nuspec in nuspecs)
    {
        NuGetPack(nuspec.FullPath, new NuGetPackSettings {
                                    Version = buildParams.Version,
                                    //ReleaseNotes = parameters.ReleaseNotes.Notes.ToArray(),
                                    //BasePath = parameters.Paths.Directories.ArtifactsBin,
                                    //OutputDirectory = parameters.Paths.Directories.NugetRoot,
                                    //Symbols = false,
                                    NoPackageAnalysis = true
        });
    }


});

Task("CorePublish")
    .IsDependentOn("Package")
    .Does(() =>
{
    var parsedSolution = ParseSolution(buildParams.SolutionPath);
    var nupkgs = GetFiles("./*.nupkg").Where(fi => parsedSolution.Projects.Any(p => string.Format("{0}.{1}.nupkg", p.Name, buildParams.Version) == fi.GetFilename().ToString()));
    foreach(var nupkg in nupkgs)
    {
        NuGetPush(nupkg.FullPath, new NuGetPushSettings {
            ApiKey = EnvironmentVariable("NUGET_PUSH_API_KEY"),
            Source = EnvironmentVariable("NUGET_PUSH_SOURCE")
        });
    }
    
});