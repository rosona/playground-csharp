#!/bin/bash
set -ev
dotnet restore -s "https://nuget.cdn.azure.cn/v3/index.json" -s "https://api.nuget.org/v3/index.json" "playground-csharp.sln"
dotnet build "ConsoleAppTest/ConsoleAppTest.csproj"
dotnet build "AkkaTest/AkkaTest.csproj"
