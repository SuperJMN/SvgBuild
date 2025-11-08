# SvgBuild

SvgBuild is an MSBuild task that transforms SVG assets into raster images such as **PNG** or **ICO**. It ships as a NuGet package so any project can consume it by adding a single package reference.

## Features

- Convert SVG files to PNG or multi-size ICO outputs during the build.
- Generate icon-friendly ICO files that include the common sizes (16, 24, 32, 48, 64, 128 and 256).
- Functional error handling powered by [CSharpFunctionalExtensions](https://github.com/vkhorikov/CSharpFunctionalExtensions).
- Simple MSBuild integration with support for declarative conversion items.

## Getting started

Install the NuGet package in the project that needs the raster assets:

```xml
<ItemGroup>
  <PackageReference Include="SvgBuild" Version="1.0.0" />
</ItemGroup>
```

> **Tip:** when the package is referenced from multiple projects within a solution, place the `PackageReference` in a shared `Directory.Build.props` file so every project sees the `ConvertSvg` task.

## Converting assets during the build

SvgBuild exposes an item called `SvgBuildConversion`. Each item represents a single conversion that will run before the `BeforeBuild` phase. The `Include` attribute is the source SVG, and the `OutputPath` metadata determines the destination file.

```xml
<ItemGroup>
  <SvgBuildConversion Include="App/Assets/icon.svg">
    <OutputFormat>ico</OutputFormat>
    <OutputPath>$(ProjectDir)App.Desktop/Assets/icon.ico</OutputPath>
  </SvgBuildConversion>
</ItemGroup>
```

### Avalonia Win32 icon example

In an Avalonia solution with a desktop head project (`App.Desktop`), place the SVG in the shared project (`App/Assets/icon.svg`) and add the `SvgBuildConversion` item to the desktop head `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="SvgBuild" Version="1.0.0" />
  </ItemGroup>

  <ItemGroup>
    <SvgBuildConversion Include="..\App\Assets\icon.svg">
      <OutputFormat>ico</OutputFormat>
      <OutputPath>$(ProjectDir)Assets\icon.ico</OutputPath>
    </SvgBuildConversion>
  </ItemGroup>

  <ItemGroup>
    <None Include="Assets\icon.ico" />
  </ItemGroup>
</Project>
```

When the desktop project builds, SvgBuild will create `Assets/icon.ico` with multiple resolutions so the Win32 window icon looks sharp at every scale.

### Converting to PNG

Use `OutputFormat` set to `png` to obtain a raster PNG version of an SVG. The generated file preserves the original aspect ratio and is encoded at maximum quality.

```xml
<ItemGroup>
  <SvgBuildConversion Include="App/Assets/splash.svg">
    <OutputFormat>png</OutputFormat>
    <OutputPath>$(ProjectDir)Assets/splash.png</OutputPath>
  </SvgBuildConversion>
</ItemGroup>
```

### Using the task directly

For advanced scenarios you can invoke the `ConvertSvg` task explicitly inside a target:

```xml
<Target Name="GenerateSplashAssets" BeforeTargets="BeforeBuild">
  <ConvertSvg
    InputPath="$(ProjectDir)App/Assets/splash.svg"
    OutputFormat="png"
    OutputPath="$(IntermediateOutputPath)splash.png" />
</Target>
```

## Azure Pipelines

The repository includes an `azure-pipelines.yml` definition that installs the DotnetDeployer global tool and uses it to build and publish the NuGet package. Publication happens automatically on pushes to `main` or `master`, while other branches perform a dry run.

```yaml
- script: dotnet tool install --global DotnetDeployer.Tool
  displayName: Install DotnetDeployer

- pwsh: |
    $branch = '$(Build.SourceBranch)'
    $isMain = $branch -eq 'refs/heads/main'
    $isMaster = $branch -eq 'refs/heads/master'
    $shouldPush = $isMain -or $isMaster
    Write-Host "Branch: $branch. Will push: $shouldPush"

    $arguments = @(
      'nuget',
      '--api-key', '$(NuGetApiKey)',
      '--configuration', 'Release',
      '--solution', 'SvgBuild.sln'
    )

    if (-not $shouldPush) {
      $arguments += '--no-push'
    }

    dotnetdeployer @arguments
  displayName: Package and publish with DotnetDeployer
```

Configure a secret variable called `NuGetApiKey` in Azure Pipelines (for example through a Variable Group) so the pipeline can authenticate against NuGet.org or your internal feed.

## License

SvgBuild is distributed under the MIT License.
