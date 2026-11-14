@echo off
rem Builds the standalone, self-contained, compressed single-file Windows executable into .\dist
rem (no .NET runtime or development environment is needed on the target PC).
rem All publish settings live in BlochSphereSimulator\BlochSphereSimulator.csproj (Release + RuntimeIdentifier).
dotnet publish BlochSphereSimulator\BlochSphereSimulator.csproj -c Release -r win-x64 -o dist
