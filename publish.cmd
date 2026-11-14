@echo off
rem Builds the standalone, self-contained single-file Windows executable into .\dist
rem (no .NET runtime or development environment is needed on the target PC).
dotnet publish BlochSphereSimulator\BlochSphereSimulator.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none -o dist
