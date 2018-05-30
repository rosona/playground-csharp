#!/bin/bash
set -ev

TAG=$1
DOCKER_USERNAME=$2
DOCKER_PASSWORD=$3

# Create publish artifact
dotnet publish -c Release console-test

# Build the Docker images
docker build -t rosona/test:$TAG console-test/bin/Release/netcoreapp2.0/publish/.
docker tag rosona/test:$TAG rosona/test:latest

# Login to Docker Hub and upload images
docker login -u="$DOCKER_USERNAME" -p="$DOCKER_PASSWORD"
docker push rosona/test:$TAG
docker push rosona/test:latest
