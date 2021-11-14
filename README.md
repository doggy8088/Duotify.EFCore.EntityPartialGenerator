# Duotify.EFCore.EntityPartialGenerator

This .NET Global tool is a supplemental tool for generating EFCore Entity Model class's `ModelMetadataType` partial class. (a.k.a. Buddy Class).

## Installation

```sh
dotnet tool install -g Duotify.EFCore.EntityPartialGenerator
```

## Usage

1. Usage information

    ```sh
    efp
    ```

    > `efp` is stands for **Entity Framework Partial class generator**.

1. List all the DbContext class in the project

    ```sh
    efp list
    ```

2. Generating all the required "buddy class" for the entity model class.

    ```sh
    efp generate -c ContosoUniversityContext -o Models
    ```

    > This command will build existing project first. Only buildable project can generate partial classes.

    Show generating files 

    ```sh
    efp generate -c ContosoUniversityContext -v -o Models
    ```

    Overwrite existing partial class

    ```sh
    efp generate -c ContosoUniversityContext -v -o Models -f
    ```

## Build & Publish

1. Change `<PackageVersion>` and `<Version>` property in `*.csproj` file

2. Build & Pack & Publish

    ```sh
    dotnet build -c Release
    dotnet pack -c Release
    dotnet nuget push bin\Release\Duotify.EFCore.EntityPartialGenerator.1.2.0.nupkg --api-key YourApiKeyFromNuGetOrg --source https://api.nuget.org/v3/index.json
    ```

