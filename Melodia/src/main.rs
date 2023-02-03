mod launch;
mod steam_apps;

const APP_ID: u64 = 1637730;

fn main() {
    // TODO: Error handling
    println!("[ Preparing to load Melodia... ]");
    launch::do_launch(&steam_apps::find_game_by_id(APP_ID).unwrap().unwrap()).unwrap();
}
