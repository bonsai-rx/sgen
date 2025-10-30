[CmdletBinding()] param (
    [string[]]$Serializer=@("yaml"),
    [string]$SchemaPath=".\workflows\*.json",
    [string]$OutputFolder=".\workflows\Extensions",
    [string]$SgenPath="..\artifacts\bin\Bonsai.Sgen\release"
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if ($SchemaPath) {
    $SchemaPath = Join-Path (Get-Location) $SchemaPath
}

if ($OutputFolder) {
    $OutputFolder = Join-Path (Get-Location) $OutputFolder
}

$SgenPath = Join-Path (Get-Location) $SgenPath | Join-Path -ChildPath "Bonsai.Sgen"
Push-Location $PSScriptRoot
try {
    foreach ($schemaPath in Get-ChildItem -File -Recurse $SchemaPath) {
        &$SgenPath $schemaPath --output $OutputFolder --serializer @Serializer
    }
} finally {
    Pop-Location
}
