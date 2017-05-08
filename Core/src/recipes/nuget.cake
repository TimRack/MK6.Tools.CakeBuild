Task("Create-NuGet-Packages")
    .IsDependentOn("Build")
    .WithCriteria(() => DirectoryExists(parameters.Paths.Directories.NugetNuspecDirectory))
    .Does(() =>
{
    Information("NugetNuspecDirectory: {0}", parameters.Paths.Directories.NugetNuspecDirectory);
    var nuspecFiles = GetFiles(parameters.Paths.Directories.NugetNuspecDirectory + "/" + parameters.BuildConfigFilePath.FullPath.Replace(".json", "") + "/*.nuspec");

    EnsureDirectoryExists(parameters.Paths.Directories.NuGetPackages);

    var enableSymbols = true;
    if(HasEnvironmentVariable(nugetEnableSymbols))
    {
        if(!bool.TryParse(EnvironmentVariable(nugetEnableSymbols), out enableSymbols))
            enableSymbols = true;
    }


    foreach(var nuspecFile in nuspecFiles)
    {
        if(enableSymbols)
        {
            var srcDir = parameters.Paths.Directories.TempBuild.Combine("src");
            EnsureDirectoryExists(srcDir);
        }

        // TODO: Addin the release notes
        // ReleaseNotes = parameters.ReleaseNotes.Notes.ToArray(),

        // Create packages.
        NuGetPack(nuspecFile, new NuGetPackSettings {
            BasePath = parameters.Paths.Directories.TempBuild,
            Version = parameters.Version.SemVersion,
            OutputDirectory = parameters.Paths.Directories.NuGetPackages,
            Symbols = enableSymbols,
            NoPackageAnalysis = true
        });
    }
});

Task("Publish-Nuget-Packages")
    .IsDependentOn("Package")
    .WithCriteria(() => DirectoryExists(parameters.Paths.Directories.NuGetPackages))
    .Does(() =>
{
    var nugetSourceUrl = parameters.NuGet.SourceUrl;
    var nugetApiKey = parameters.NuGet.ApiKey;

    if(parameters.IsLocalBuild)
    {
        if(HasEnvironmentVariable(localNugetSourceVariable))
        {
            nugetSourceUrl = EnvironmentVariable(localNugetSourceVariable);
            Verbose("Local Build: Using env variable {0} with value of {1}", localNugetSourceVariable, nugetSourceUrl);
            nugetApiKey = EnvironmentVariable(nuGetApiKeyCIUrlVariable);            
        }
        else
        {
            nugetSourceUrl = localNugetSourceDefaultValue;            
            Verbose("Local Build: env variable {0} not found. Using default value {1}", localNugetSourceVariable, nugetSourceUrl);
        }
    }
    else
    {
        if(!parameters.IsMasterBranch)
        {
            Information("Setting nuget CI variables...");
            nugetSourceUrl = EnvironmentVariable(nuGetSourceCIUrlVariable);
            nugetApiKey = EnvironmentVariable(nuGetApiKeyCIUrlVariable);
        }

        if(string.IsNullOrEmpty(nugetApiKey)) {
            throw new InvalidOperationException("Could not resolve NuGet API key.");
        }

        if(string.IsNullOrEmpty(nugetSourceUrl)) {
            throw new InvalidOperationException("Could not resolve NuGet API url.");
        }

    }
    var nupkgFiles = GetFiles(parameters.Paths.Directories.NuGetPackages + "/**/*.nupkg");

    foreach(var nupkgFile in nupkgFiles)
    {
        Information("Using source {0}", nugetSourceUrl);
        // Push the package.
        NuGetPush(nupkgFile, new NuGetPushSettings {
            Source = nugetSourceUrl,
            ApiKey = nugetApiKey
        });
    }
})
.OnError(exception =>
{
    Information("Publish-Nuget-Packages Task failed, but continuing with next Task...");
    Error(exception.ToString());
    publishingError = true;
});