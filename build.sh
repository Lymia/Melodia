#!/bin/sh

rm -rfv bin || exit 1
mkdir -v bin || exit 1
mkdir -v bin/lib || exit 1

echo "Building CrystalBootstrap..."
cd CrystalBootstrap || exit 1
    dotnet msbuild -p:Configuration=Release || exit 1
    cp -v bin/Release/net4.5.2/CrystalBootstrap.* ../bin/lib || exit 1
cd .. || exit 1


echo "Building CrystalPatcher"
cd CrystalPatcher || exit 1
    dotnet msbuild -p:Configuration=Release || exit 1
    cp -v bin/Release/net4.5.2/CrystalPatcher.* bin/Release/net4.5.2/dnlib.dll ../bin/lib || exit 1
cd .. || exit 1
