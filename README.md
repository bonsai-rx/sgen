# sgen
Tool for automatically generating YML serialization classes from schema files

## Getting Started

1. Navigate to the [Bonsai.Sgen toolbox NuGet package](https://www.nuget.org/packages/Bonsai.Sgen/)
2. Click `NET CLI (Local)` and copy the two suggested commands. E.g.:

    ```cmd
    dotnet new tool-manifest # if you are setting up this repo
    dotnet tool install --local Bonsai.Sgen --version 0.1.0
    ```

3. Run the suggested commands in your terminal, in the root folder where you want to run the tool from:

4. Run the tool:

    ```cmd
    dotnet bonsai.sgen
    ```

5. Copy the generated class file to your project `Extensions` folder.

6. Add the necessary package references to your `Extensions.csproj` file. For instance:

    ```xml
    <ItemGroup>
        <PackageReference Include="Bonsai.Core" Version="2.8.0" />
        <PackageReference Include="YamlDotNet" Version="12.0.2" />
    </ItemGroup>
    </Project>
    ```

7. To restore the tool at any point, run:

    ```cmd
    dotnet tool restore
    ```
