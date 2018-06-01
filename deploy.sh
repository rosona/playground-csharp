#!/bin/bash
set -ev

TAG=$1
DOCKER_USERNAME=$2
DOCKER_PASSWORD=$3

docker login -u="$DOCKER_USERNAME" -p="$DOCKER_PASSWORD"

# ConsoleAppTest docker
dotnet publish -c Release ConsoleAppTest
docker build -t rosona/console-app-test:$TAG ConsoleAppTest/bin/Release/netcoreapp2.0/publish/.
docker tag rosona/console-app-test:$TAG rosona/console-app-test:latest
docker push rosona/console-app-test:$TAG
docker push rosona/console-app-test:latest

# AkkaTest docker
dotnet publish -c Release AkkaTest
docker build -t rosona/akka-test:$TAG ConsoleAppTest/bin/Release/netcoreapp2.0/publish/.
docker tag rosona/akka-test:$TAG rosona/akka-test:latest
docker push rosona/akka-test:$TAG
docker push rosona/akka-test:latest
