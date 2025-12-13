# Generates C# protobuf stubs for Unity from the shared schema.
param(
    [string]$SchemaPath = "proto/tienlen.proto",
    [string]$OutDir = "Client/Assets/Scripts/GeneratedProto"
)

if (-not (Test-Path $SchemaPath)) {
    Write-Error "Schema not found at $SchemaPath"
    exit 1
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

protoc --csharp_out=$OutDir $SchemaPath
if ($LASTEXITCODE -ne 0) {
    Write-Error "protoc failed"
    exit $LASTEXITCODE
}

Write-Host "C# stubs generated to $OutDir"
