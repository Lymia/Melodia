use anyhow::{bail, Result};
use std::{
    fs,
    path::{Path, PathBuf},
};

/// A quick and dirty function to parse a line of Steam's vdf format.
fn parse_vdf_line<'a>(line: &'a str, target_name: &str) -> Result<Option<&'a str>> {
    let line = line.trim();
    if line.chars().next() != Some('"') {
        Ok(None)
    } else {
        let line = &line[1..];

        if line.len() > target_name.len() + 1
            && &line.as_bytes()[..target_name.len()] == target_name.as_bytes()
            && line.as_bytes()[target_name.len()] == b'"'
        {
            let line = line[target_name.len() + 1..].trim();
            if line.as_bytes().first() != Some(&b'"') && line.as_bytes().last() != Some(&b'"') {
                Ok(None)
            } else {
                return Ok(Some(&line[1..line.len() - 1]));
            }
        } else {
            Ok(None)
        }
    }
}

/// Parses an appmanifest to find the game directory
fn parse_app_manifest(library_root: &Path, path: &Path) -> Result<PathBuf> {
    let file = fs::read_to_string(path)?;

    for line in file.lines() {
        if let Some(path) = parse_vdf_line(line, "installdir")? {
            let mut out_path = library_root.to_path_buf();
            out_path.push("common");
            out_path.push(path);

            if out_path.exists() && out_path.is_dir() {
                return Ok(out_path);
            } else {
                bail!("Path '{}' does not exist or is not a directory?", out_path.display());
            }
        }
    }

    bail!("Could not find installdir field.")
}

/// Finds all available libraries for steam from a given manifest
fn find_libraries_from_manifest(manifest: &Path) -> Result<Vec<PathBuf>> {
    let file = fs::read_to_string(manifest)?;

    let mut dirs = Vec::new();
    for line in file.lines() {
        if let Some(path) = parse_vdf_line(line, "path")? {
            let mut buf = PathBuf::from(path);
            buf.push("steamapps");

            if buf.exists() && buf.is_dir() {
                dirs.push(buf);
            }
        }
    }

    if dirs.is_empty() {
        bail!("No steamapps directories found?")
    } else {
        Ok(dirs)
    }
}

/// Finds the steam root directory
fn steam_root() -> Result<PathBuf> {
    if cfg!(target_os = "linux") {
        match dirs::home_dir() {
            Some(x) => {
                let mut steam_path = x;
                steam_path.push(".steam");
                steam_path.push("steam");
                Ok(steam_path)
            }
            None => bail!("Could not find home directory???"),
        }
    } else {
        bail!("Unsupported platform!")
    }
}

/// Finds all steam libraries installed for the current user
fn find_steam_libraries() -> Result<Vec<PathBuf>> {
    let mut path = steam_root()?;
    path.push("steamapps");
    path.push("libraryfolders.vdf");

    if path.exists() && path.is_file() {
        find_libraries_from_manifest(&path)
    } else {
        bail!("Library manifest not found at '{}'", path.display());
    }
}

/// Check if a game exists in a specific library
fn check_game_in_library(library: &Path, filename: &str) -> Result<Option<PathBuf>> {
    let mut buf = library.to_path_buf();
    buf.push(filename);

    if buf.exists() && buf.is_file() {
        Ok(Some(parse_app_manifest(library, &buf)?))
    } else {
        Ok(None)
    }
}

/// Finds a steam game by its id, if it exists
pub fn find_game_by_id(id: u64) -> Result<Option<PathBuf>> {
    let name = format!("appmanifest_{id}.acf");
    for library in find_steam_libraries()? {
        if let Some(location) = check_game_in_library(&library, &name)? {
            return Ok(Some(location));
        }
    }
    Ok(None)
}
