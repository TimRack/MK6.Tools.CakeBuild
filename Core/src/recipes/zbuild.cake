///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var parameters = BuildParameters.GetParameters(Context, BuildSystem);//, repositoryOwner, repositoryName);
var publishingError = false;

var config = Config.BuildJsonConfig(parameters.BuildConfigFilePath.FullPath);
var sourceDirectoryPath = Context.MakeAbsolute((DirectoryPath)config.SourceDirectoryPath);
var title = config.Title;
var solutionFilePath = Context.MakeAbsolute((FilePath)config.SolutionFilePath);
var solutionDirectoryPath = Context.MakeAbsolute((DirectoryPath)config.SolutionDirectoryPath);
var octopusProjectName = config.OctopusProjectName;
var testAssemblySearchPattern = config.TestAssemblySearchPattern;

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
    
    // if(parameters.IsMasterBranch && (context.Log.Verbosity != Verbosity.Diagnostic)) {
    //     Information("Increasing verbosity to diagnostic.");
    //     context.Log.Verbosity = Verbosity.Diagnostic;
    // }

    parameters.SetBuildPaths(
        BuildPaths.GetPaths(sourceDirectoryPath,
            context: Context
        )
    );

    parameters.SetBuildVersion(
        BuildVersion.CalculatingSemanticVersion(
            context: Context,
            parameters: parameters
        )
    );

    Information("Building version {0} of " + title + " ({1}, {2}) using version {3} of Cake.)",
        parameters.Version.SemVersion,
        parameters.Configuration,
        parameters.Target,
        parameters.Version.CakeVersion);

    SetupDefaultEnvironment(context, parameters);
});

// Teardown(context =>
// {
//     if(context.Successful)
//     {
//     }
//     else
//     {
//     }

//     Information("Finished running tasks.");
// });

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("Show-Info")
    .Description("Shows information about the current build")
    .Does(() =>
{
    Information("Target: {0}", parameters.Target);
    Information("Configuration: {0}", parameters.Configuration);

    Information("Solution FilePath: {0}", MakeAbsolute((FilePath)solutionFilePath));
    Information("Solution DirectoryPath: {0}", MakeAbsolute((DirectoryPath)solutionDirectoryPath));
    Information("Source DirectoryPath: {0}", MakeAbsolute(parameters.Paths.Directories.Source));
    Information("Build DirectoryPath: {0}", MakeAbsolute(parameters.Paths.Directories.Build));
    Information("IsMasterBranch: {0}", parameters.IsMasterBranch);
    
});

private void SetupDefaultEnvironment(ICakeContext context, BuildParameters parameters)
{
    if(parameters.IsLocalBuild)
    {
        Information("Environment variables....");
        SetVariable(context, localNugetSourceVariable,localNugetSourceDefaultValue);
        SetVariable(context, nuGetSourcesVariable, nugetSourcesDefaultValue);
        SetVariable(context, nuGetSourceCIUrlVariable, klondikeCIUrlDefault);
        SetVariable(context, nuGetSymbolSourceUrlVariable, nugetEnableSymbolsDefaultValue);
        SetVariable(context, octopusUrlVariable, octopusUrlVariableDefaultValue);
        SetVariable(context, octopusApiKeyVariable, octopusApiKeyVariableDefaultValue);
    }
}
private void SetVariable(ICakeContext context, string variableName, string defaultValue)
{
    context.Information("{0} : {1}", variableName, Environment.GetEnvironmentVariable(variableName));
    
    if(!context.HasEnvironmentVariable(variableName))
    {
        context.Warning("Setting EnvrionmentVariable {0} to {1}",variableName, defaultValue);
        Environment.SetEnvironmentVariable(variableName, defaultValue);
    }

    context.Information("{0} : {1}", variableName, Environment.GetEnvironmentVariable(variableName));
    
}

Task("Clean")
    .Description("Cleans directories: Build, TempBuild, TestResults, NuGetPackages.")
    .Does(() =>
{
    Information("Cleaning...");

    CleanDirectories(parameters.Paths.Directories.ToClean);
});

Task("Restore")
    .Description("Executes a nuget restore using the specified solution file from the config")
    .Does(() =>
{
    Information("Restoring {0}...", solutionFilePath);
    var sourceString = EnvironmentVariable(nuGetSourcesVariable);
    
    // if(!parameters.IsMasterBranch)
    // {
    //     if(sourceString == null)
    //         sourceString = string.Empty;
        
    //     sourceString += ";" + EnvironmentVariable(nuGetSourceCIUrlVariable);
    // }

    if(parameters.IsLocalBuild)
    {
        if(sourceString == null)
            sourceString = string.Empty;
        
        sourceString += ";" + (HasEnvironmentVariable(localNugetSourceVariable) ? EnvironmentVariable(localNugetSourceVariable) : localNugetSourceDefaultValue);
    }

    Verbose("Using nuget source(s) for restore: {0}", sourceString);

    var source = sourceString.Split(';').ToList();
    NuGetRestore(solutionFilePath, new NuGetRestoreSettings { Source = source });
});

Task("Build")
    .Description("Executes msbuild using the specified solution file from the config. Depends on Show-Info, Clean and Restore tasks.")
    .IsDependentOn("Show-Info")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does(() =>
{
    Information("Building {0}", solutionFilePath);

    MSBuild(solutionFilePath, settings =>
        settings.SetPlatformTarget(PlatformTarget.MSIL)
            //.AddFileLogger()
            //.SetVerbosity(Verbosity.Verbose)
            .WithProperty("TreatWarningsAsErrors", "false")
            .WithProperty("OutDir", MakeAbsolute(parameters.Paths.Directories.TempBuild).FullPath)
            .WithTarget("Build")
            .SetConfiguration(parameters.Configuration));
});

Task("Package")
    .Description("Depends on Test and NugetPackage tasks.")
    .IsDependentOn("Test")
    .IsDependentOn("NugetPackage");

Task("Default")
    .Description("Default task that depends on Package task.")
    .IsDependentOn("Package");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(parameters.Target);