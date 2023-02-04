mod launch;
mod steam_apps;

const APP_ID: u64 = 1637730;

#[cfg(feature = "setup_tool")]
fn main() {
    use std::path::PathBuf;

    let mut game_path = steam_apps::find_game_by_id(APP_ID).unwrap().unwrap();
    let mut target_path = PathBuf::from("../contrib");

    for name in &["Crystal Project.exe", "FNA.dll", "Steamworks.NET.dll"] {
        game_path.push(name);
        target_path.push(name);

        println!("Copying '{}' -> '{}'", game_path.display(), target_path.display());
        std::fs::copy(&game_path, &target_path).unwrap();

        game_path.pop();
        target_path.pop();
    }
}

#[cfg(not(feature = "setup_tool"))]
fn main() {
    // TODO: Error handling
    println!("[ Preparing to load Melodia... ]");
    launch::do_launch(&steam_apps::find_game_by_id(APP_ID).unwrap().unwrap()).unwrap();
}
