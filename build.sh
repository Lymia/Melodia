#!/bin/sh

rm -rfv bin || exit 1
mkdir -v bin || exit 1
mkdir -v bin/lib || exit 1
mkdir -v bin/lib/bootstrap || exit 1
mkdir -v bin/lib/patcher || exit 1

echo "Restoring projects..."
dotnet restore || exit 1

echo "[ Building Melodia.exe (main binary) ]"
cd Melodia || exit 1
    cargo build --release || exit 1
    cp target/release/melodia ../bin/Melodia || exit 1
cd .. || exit 1

echo "[ Building MelodiaBootstrap.exe ]"
cd MelodiaBootstrap || exit 1
    dotnet msbuild -p:Configuration=Release || exit 1
    cp -v bin/Release/net4.5.2/MelodiaBootstrap.* ../bin/lib/bootstrap || exit 1
cd .. || exit 1


echo "[ Building MelodiaPatcher.exe ]"
cd MelodiaPatcher || exit 1
    dotnet msbuild -p:Configuration=Release || exit 1
    cp -v bin/Release/net4.5.2/MelodiaPatcher.* bin/Release/net4.5.2/dnlib.dll ../bin/lib/patcher || exit 1
cd .. || exit 1
