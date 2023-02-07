#!/bin/sh

rm -rfv bin || exit 1
mkdir -v bin || exit 1
mkdir -pv bin/Melodia/lib/bootstrap || exit 1
mkdir -pv bin/Melodia/lib/patcher || exit 1
mkdir -pv bin/Melodia/lib/modules_host || exit 1
mkdir -pv bin/Melodia/lib/modules_guest || exit 1

echo "[ Restoring projects... ]"
dotnet restore || exit 1

echo "[ Building Melodia.exe (main binary) ]"
cd Melodia || exit 1
    cargo build --release --target x86_64-pc-windows-gnu || exit 1
    cargo build --release --target x86_64-unknown-linux-gnu || exit 1
    cp -v target/x86_64-pc-windows-gnu/release/melodia.exe ../bin/Melodia/Melodia.win32.exe || exit 1
    cp -v target/x86_64-unknown-linux-gnu/release/melodia ../bin/Melodia/Melodia.linux || exit 1
cd .. || exit 1

echo "[ Building C# binaries... ]"
dotnet msbuild -p:Configuration=Release || exit 1

echo "[ Building Melodia distribution... ]"
cp -vn Melodia.Common/bin/x64/Release/net4.7.2/* bin/Melodia/lib/patcher || exit 1
cp -vn MelodiaBootstrap/bin/x64/Release/net4.7.2/* bin/Melodia/lib/bootstrap || exit 1
cp -vn MelodiaPatcher/bin/x64/Release/net4.7.2/* bin/Melodia/lib/patcher || exit 1

cp -vn Melodia.CoreCallbacks/bin/x64/Release/net4.7.2/Melodia.CoreCallbacks.* bin/Melodia/lib/modules_guest || exit 1

rm -v bin/Melodia/lib/*/FNA* || exit 1
chmod -vR -x bin/Melodia/lib/*/*.{dll,exe,pdb} bin/Melodia/Melodia.win32.exe || exit 1
