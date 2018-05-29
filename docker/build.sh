#!/bin/bash
set -ev
dotnet restore -s "https://nuget.cdn.azure.cn/v3/index.json" -s "https://api.nuget.org/v3/index.json" "playground-csharp.sln"
dotnet test
dotnet build "console-test/console-test.csproj"
