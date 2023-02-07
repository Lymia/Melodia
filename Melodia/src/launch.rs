use anyhow::{bail, Result};
use std::{
    ffi::OsString,
    fs,
    path::{Path, PathBuf},
    process::{Command, Stdio},
};
use tempfile::TempDir;

/// Get the directory the Melodia root launcher is installed in.
fn get_base_dir() -> Result<PathBuf> {
    let mut exe = std::env::current_exe()?;
    exe = exe.canonicalize()?;
    exe.pop();
    Ok(exe)
}

fn resolve(dir: &Path, name: &str) -> PathBuf {
    let mut dir = dir.to_path_buf();
    dir.push(name);
    dir
}
fn resolve_chk(dir: &Path, name: &str) -> Result<PathBuf> {
    let path = resolve(dir, name);
    if !path.exists() {
        bail!("'{}' does not exist!", path.display());
    }
    Ok(path)
}

fn symlink(src_dir: &Path, dest_dir: &Path, name: &str) -> Result<()> {
    let src = resolve_chk(src_dir, name)?;
    let dest = resolve(dest_dir, name);
    println!("Symlink '{}' -> '{}'", src.display(), dest.display());
    symlink::symlink_file(src, dest)?;
    Ok(())
}
#[allow(unused)] // not used on windows
fn symlink_dir(src_dir: &Path, dest_dir: &Path, name: &str) -> Result<()> {
    let src = resolve_chk(src_dir, name)?;
    let dest = resolve(dest_dir, name);
    println!("Symlink '{}' -> '{}'", src.display(), dest.display());
    symlink::symlink_dir(src, dest)?;
    Ok(())
}

fn winepath(temp_dir: &Path, path: &Path, launch_wine: bool) -> Result<OsString> {
    if launch_wine {
        let result = Command::new("winepath")
            .current_dir(temp_dir)
            .arg("-w")
            .arg(path)
            .env("WINEDEBUG", "-all")
            .output()?
            .stdout;

        let mut str = String::from_utf8_lossy(&result).trim().to_string();
        if path.is_dir() {
            str.push('\\');
        }
        Ok(OsString::from(str))
    } else {
        Ok(path.as_os_str().to_owned())
    }
}

fn launch_bin(
    game_dir: &Path,
    base_dir: &Path,
    temp_dir: &Path,
    patcher_dir: &Path,
    bin: &Path,
    launch_wine: bool,
) -> Result<()> {
    if launch_wine {
        Command::new("wineserver")
            .current_dir(temp_dir)
            .arg("-p=60")
            .stdin(Stdio::null())
            .stdout(Stdio::null())
            .stderr(Stdio::null())
            .spawn()?;
    }

    let mut args = Vec::new();
    // proton argument if needed
    if launch_wine {
        args.push(winepath(temp_dir, bin, launch_wine)?);
    }
    // arguments for MelodiaBootstrap
    args.push(winepath(temp_dir, game_dir, launch_wine)?);
    args.push(winepath(temp_dir, patcher_dir, launch_wine)?);
    args.push(OsString::from("MelodiaPatcher"));
    // arguments for MelodiaPatcher
    args.push(winepath(temp_dir, game_dir, launch_wine)?);
    args.push(winepath(temp_dir, base_dir, launch_wine)?);
    args.push(winepath(temp_dir, temp_dir, launch_wine)?);
    // user arguments
    args.extend(std::env::args_os().skip(1));

    let mut child = if launch_wine {
        Command::new("wine")
            .current_dir(temp_dir)
            .args(args)
            .env("WINEDEBUG", "-all")
            .spawn()?
    } else {
        Command::new(bin).current_dir(temp_dir).args(args).spawn()?
    };
    child.wait()?;
    Ok(())
}

fn symlink_libraries(src_dir: &Path, dest_dir: &Path) -> Result<()> {
    for file in fs::read_dir(src_dir)? {
        let file = file?;
        if let Some(name) = file.file_name().to_str() {
            if name.ends_with(".dll")
                && (name.starts_with("mscorlib")
                    || name.starts_with("Mono.")
                    || name.starts_with("FNA")
                    || name.starts_with("System."))
            {
                symlink(src_dir, dest_dir, name)?;
            }
        }
    }
    Ok(())
}

#[cfg(not(any(target_os = "windows", target_os = "linux")))]
fn launch_game_windows(_: &Path, _: &Path, _: &Path) -> Result<()> {
    bail!("Cannot launch a Windows version of a game your platform");
}

#[cfg(any(target_os = "windows", target_os = "linux"))]
fn launch_game_windows(temp_dir: &Path, base_dir: &Path, game_dir: &Path) -> Result<()> {
    // Create patcher directory with dependencies installed
    let patcher_dir = resolve(temp_dir, "patcher");
    fs::create_dir_all(&patcher_dir)?;

    symlink(game_dir, &patcher_dir, "FAudio.dll")?;
    symlink(game_dir, &patcher_dir, "FNA.dll")?;
    symlink(game_dir, &patcher_dir, "FNA3D.dll")?;
    symlink(game_dir, &patcher_dir, "libtheorafile.dll")?;
    symlink(game_dir, &patcher_dir, "SDL2.dll")?;

    let src_dir = resolve_chk(base_dir, "lib/patcher")?;
    for file in fs::read_dir(&src_dir)? {
        let file = file?;
        if let Some(name) = file.file_name().to_str() {
            symlink(&src_dir, &patcher_dir, name)?;
        }
    }

    // Symlink important core libraries
    symlink_libraries(game_dir, temp_dir)?;

    // Symlink native libraries
    symlink(game_dir, temp_dir, "steam_api64.dll")?;

    // Symlink MelodiaBootstrap files
    let bootstrap_dir = resolve_chk(base_dir, "lib/bootstrap")?;
    symlink(&bootstrap_dir, temp_dir, "MelodiaBootstrap.exe")?;
    symlink(&bootstrap_dir, temp_dir, "MelodiaBootstrap.pdb")?;
    symlink(&bootstrap_dir, temp_dir, "MelodiaBootstrap.exe.config")?;

    // Write steam_appid.txt
    fs::write(resolve(temp_dir, "steam_appid.txt"), crate::APP_ID.to_string())?;

    // Execute MelodiaBootstrap binary
    println!("[ Launching bootstrap binary... ]");
    launch_bin(
        game_dir,
        base_dir,
        temp_dir,
        &patcher_dir,
        &resolve_chk(temp_dir, "MelodiaBootstrap.exe")?,
        cfg!(target_os = "linux"),
    )?;

    Ok(())
}

#[cfg(not(target_os = "linux"))]
fn launch_game_linux(_: &Path, _: &Path, _: &Path) -> Result<()> {
    bail!("Cannot launch a Linux version of a game on non-Linux platforms");
}

#[cfg(target_os = "linux")]
fn launch_game_linux(temp_dir: &Path, base_dir: &Path, game_dir: &Path) -> Result<()> {
    // Copy the main mono executable
    fs::copy(
        resolve_chk(game_dir, "Crystal Project.bin.x86_64")?,
        resolve(temp_dir, "MelodiaBootstrap.bin.x86_64"),
    )?;

    // Symlink Mono files
    symlink_dir(game_dir, temp_dir, "lib64")?;
    symlink(game_dir, temp_dir, "monoconfig")?;
    symlink(game_dir, temp_dir, "monomachineconfig")?;

    // Symlink important core libraries
    symlink_libraries(game_dir, temp_dir)?;

    // Symlink MelodiaBootstrap files
    let bootstrap_dir = resolve_chk(base_dir, "lib/bootstrap")?;
    symlink(&bootstrap_dir, temp_dir, "MelodiaBootstrap.exe")?;
    symlink(&bootstrap_dir, temp_dir, "MelodiaBootstrap.pdb")?;
    symlink(&bootstrap_dir, temp_dir, "MelodiaBootstrap.exe.config")?;

    // Write steam_appid.txt
    fs::write(resolve(temp_dir, "steam_appid.txt"), crate::APP_ID.to_string())?;

    // Execute MelodiaBootstrap binary
    println!("[ Launching bootstrap binary... ]");
    launch_bin(
        game_dir,
        base_dir,
        temp_dir,
        &resolve_chk(base_dir, "lib/patcher")?,
        &resolve_chk(temp_dir, "MelodiaBootstrap.bin.x86_64")?,
        false,
    )?;

    Ok(())
}

fn launch_game(temp_dir: &Path, base_dir: &Path, game_dir: &Path) -> Result<()> {
    let mut game_buf = game_dir.to_path_buf();

    game_buf.push("Crystal Project.bin.x86_64");
    if game_buf.exists() {
        return launch_game_linux(temp_dir, base_dir, game_dir);
    }
    game_buf.pop();

    game_buf.push("steam_api64.dll");
    if game_buf.exists() {
        return launch_game_windows(temp_dir, base_dir, game_dir);
    }
    game_buf.pop();

    bail!("Installation was not recognized")
}

pub fn do_launch(game_dir: &Path) -> Result<()> {
    let base_dir = get_base_dir()?;

    println!("Game directory: {}", game_dir.display());
    println!("Melodia directory: {}", base_dir.display());

    let temp_dir = TempDir::new()?;
    println!("Temp directory: {}", temp_dir.path().display());

    launch_game(temp_dir.path(), &base_dir, game_dir)?;

    temp_dir.close()?;

    Ok(())
}
