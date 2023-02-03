use anyhow::{bail, Result};
use std::{fs, path::{Path, PathBuf}, process::Command};
use std::ffi::OsString;
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
fn symlink_dir(src_dir: &Path, dest_dir: &Path, name: &str) -> Result<()> {
    let src = resolve_chk(src_dir, name)?;
    let dest = resolve(dest_dir, name);
    println!("Symlink '{}' -> '{}'", src.display(), dest.display());
    symlink::symlink_dir(src, dest)?;
    Ok(())
}

fn launch_bin(game_dir: &Path, base_dir: &Path, temp_dir: &Path, bin: &Path) -> Result<()> {
    let patcher_dir = resolve_chk(base_dir, "lib/patcher")?;

    let mut args = Vec::new();
    args.push(game_dir.as_os_str().to_owned());
    args.push(patcher_dir.as_os_str().to_owned());
    args.push(OsString::from("MelodiaPatcher"));
    args.push(game_dir.as_os_str().to_owned());
    args.extend(std::env::args_os().skip(1));

    let mut child = Command::new(bin).current_dir(temp_dir).args(args).spawn()?;
    child.wait()?;
    Ok(())
}

#[cfg(target_os = "linux")]
fn launch_game(temp_dir: &Path, base_dir: &Path, game_dir: &Path) -> Result<()> {
    // Symlink Mono files
    fs::copy(
        resolve_chk(game_dir, "Crystal Project.bin.x86_64")?,
        resolve(temp_dir, "MelodiaBootstrap.bin.x86_64"),
    )?;
    symlink_dir(game_dir, temp_dir, "lib64")?;
    symlink(game_dir, temp_dir, "monoconfig")?;
    symlink(game_dir, temp_dir, "monomachineconfig")?;
    symlink(game_dir, temp_dir, "Steamworks.NET.dll")?; // TODO: Figure out how to avoid this!!
    for file in fs::read_dir(game_dir)? {
        let file = file?;
        if let Some(name) = file.file_name().to_str() {
            if name.ends_with(".dll")
                && (name.starts_with("mscorlib")
                    || name.starts_with("Mono.")
                    || name.starts_with("FNA")
                    || name.starts_with("System."))
            {
                symlink(game_dir, temp_dir, name)?;
            }
        }
    }

    // Symlink MelodiaBootstrap files
    let bootstrap_dir = resolve_chk(base_dir, "lib/bootstrap")?;
    symlink(&bootstrap_dir, temp_dir, "MelodiaBootstrap.exe")?;
    symlink(&bootstrap_dir, temp_dir, "MelodiaBootstrap.pdb")?;
    symlink(&bootstrap_dir, temp_dir, "MelodiaBootstrap.exe.config")?;

    // Write steam_appid.txt
    fs::write(resolve(temp_dir, "steam_appid.txt"), "1637730")?;

    // Execute MelodiaBootstrap binary
    println!("Launching bootstrap binary...");
    launch_bin(
        game_dir,
        base_dir,
        temp_dir,
        &resolve_chk(temp_dir, "MelodiaBootstrap.bin.x86_64")?,
    )?;

    Ok(())
}

#[cfg(not(any(target_os = "linux")))]
fn launch_game(_: &Path) -> Result<()> {
    bail!("Operating system is not supported. :(")
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
