mod launch;
mod steam_apps;

fn main() {
    println!("Preparing to load Melodia...");
    launch::do_launch(&steam_apps::find_game_by_id(1637730).unwrap().unwrap()).unwrap();
}
