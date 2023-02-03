#!/bin/sh

rm -rfv bin || exit 1
mkdir -v bin || exit 1
mkdir -v bin/lib || exit 1
mkdir -v bin/lib/bootstrap || exit 1
mkdir -v bin/lib/patcher || exit 1
mkdir -v bin/lib/modules_host || exit 1
mkdir -v bin/lib/modules_guest || exit 1

echo "Restoring projects..."
dotnet restore || exit 1

echo "[ Building Melodia.exe (main binary) ]"
cd Melodia || exit 1
    cargo build --release || exit 1
    cp target/release/melodia ../bin/Melodia || exit 1
cd .. || exit 1

echo "[ Building C# binaries... ]"
dotnet msbuild -p:Configuration=Release || exit 1
cp -v Melodia.Common/bin/x64/Release/net4.5.2/Melodia.Common.* bin/lib/patcher || exit 1
cp -v Melodia.CoreCallbacks/bin/x64/Release/net4.5.2/Melodia.CoreCallbacks.* bin/lib/modules_guest || exit 1
cp -v MelodiaBootstrap/bin/x64/Release/net4.5.2/MelodiaBootstrap.* bin/lib/bootstrap || exit 1
cp -v MelodiaPatcher/bin/x64/Release/net4.5.2/MelodiaPatcher.* bin/lib/patcher || exit 1
cp -v MelodiaPatcher/bin/x64/Release/net4.5.2/dnlib.dll bin/lib/patcher || exit 1
