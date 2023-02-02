#!/bin/sh

rm -rfv bin || exit 1
mkdir -v bin || exit 1
mkdir -v bin/lib || exit 1

echo "Restoring projects..."
dotnet restore || exit 1

echo "Building MelodiaBootstrap..."
cd MelodiaBootstrap || exit 1
    dotnet msbuild -p:Configuration=Release || exit 1
    cp -v bin/Release/net4.5.2/MelodiaBootstrap.* ../bin/lib || exit 1
cd .. || exit 1


echo "Building MelodiaPatcher"
cd MelodiaPatcher || exit 1
    dotnet msbuild -p:Configuration=Release || exit 1
    cp -v bin/Release/net4.5.2/MelodiaPatcher.* bin/Release/net4.5.2/dnlib.dll ../bin/lib || exit 1
cd .. || exit 1
