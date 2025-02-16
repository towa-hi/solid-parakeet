#![no_std]
use soroban_sdk::{contract, contracttype, contracterror, contractimpl, vec, Env, Address, String, Vec, Map};
use soroban_sdk::storage::Persistent;

// global state defs
pub type AllUserIds = Map<Address, ()>;
pub type AllLobbyIds = Map<LobbyId, ()>;

// key defs

// user key is always the Address
pub type LobbyId = u32;

// other struct defs

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct User {
    // immutable
    pub user_id: Address,
    // mutable
    pub name: String,
    pub games_played: u32,
    pub current_lobby: Option<LobbyId>,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct UserState
{
    // immutable for now
    pub user_id: Address,
    pub team: u32,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Lobby {
    // immutable
    pub lobby_id: LobbyId,
    pub host: Address,
    pub board_def: BoardDef,
    pub must_fill_all_tiles: bool,
    pub max_pawns: Map<u32, u32>,
    pub is_secure: bool,
    // mutable
    pub user_states: Vec<UserState>, // contains user specific state, changes when another user joins or leaves
    // game state
    pub game_end_state: u32,
    pub pawns: Map<PawnId, Pawn>,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct SetupCommitment {
    pub user_id: Address,
    pub pawn_positions: Vec<PawnCommitment>,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PawnCommitment {
    pub user_id: Address,
    pub pawn_id: PawnId,
    pub pos: Vector2Int,
    pub def_hidden: String,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct BoardDef {
    pub name: String,
    pub size: Vector2Int,
    pub tiles: Map<Vector2Int, Tile>,
    pub is_hex: bool,
    pub default_max_pawns: Map<u32, u32>,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Tile {
    pub pos: Vector2Int,
    pub is_passable: bool,
    pub setup_team: u32,
    pub auto_setup_zone: u32,
}

pub type PawnId = u32;
#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Pawn {
    // immutable
    pub pawn_id: PawnId,
    pub user: Address,
    pub team: u32,
    pub def_hidden: String,
    // mutable
    pub def_key: String,
    pub def: Option<PawnDef>,
    pub pos: Vector2Int,
    pub is_alive: bool,
    pub is_moved: bool,
    pub is_revealed: bool,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]

pub struct PawnDef {

}
#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Vector2Int {
    pub x: i32,
    pub y: i32,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub enum DataKey {
    // global
    AllUserIds,
    AllLobbyIds,

    // user data
    User(Address),
    UserGamesPlayed(Address), // todo remove this
    UserLobbyId(Address), // todo remove this

    // lobby specific data
    Lobby(LobbyId),
    SetupCommitments(LobbyId),
}

// events

pub const EVENT_REGISTER: &str = "register user";
pub const EVENT_UPDATE: &str = "update user";

#[contracterror]
#[derive(Copy, Clone, Debug, Eq, PartialEq, PartialOrd, Ord)]
pub enum Error {
    UserNotFound = 1,
    InvalidUsername = 2,
}

#[contract]
pub struct Contract;

#[contractimpl]
impl Contract {
    pub fn hello(env: Env, to: String) -> Vec<String> {
        vec![&env, String::from_str(&env, "Hello"), to]
    }

    pub fn register(env: Env, user_id: Address, user_name: String) -> Result<User, Error> {
        user_id.require_auth();
        // validate username string
        if !Self::validate_username(user_name.clone()) {
            return Err(Error::InvalidUsername);
        }
        let storage = env.storage().persistent();
        let mut new_user_registered = false;
        let user = if storage.has(&DataKey::User(user_id.clone())) {
            storage.update(&DataKey::User(user_id.clone()), |existing: Option<User>| {
                let mut user = existing.unwrap();
                user.name = user_name;
                user
            })
        } else {
            // Create new user
            let new_user = User {
                user_id: user_id.clone(),
                name: user_name,
                games_played: 0,
                current_lobby: None,
            };
            storage.set(&DataKey::User(user_id.clone()), &new_user);
            let mut all_users: AllUserIds = storage.get(&DataKey::AllUserIds).unwrap_or_else(||Map::new(&env));
            all_users.set(user_id.clone(), ());
            storage.set(&DataKey::AllUserIds, &all_users);
            new_user_registered = true;
            new_user
        };
        if new_user_registered {
            env.events().publish((EVENT_REGISTER, user_id.clone()), user.clone());
        }
        else
        {
            env.events().publish((EVENT_UPDATE, user_id.clone()), user.clone());
        }
        Ok(user)
    }

    pub fn get_user_data(env: Env, user_id: Address) -> Result<User, Error> {
        let storage = env.storage().persistent();
        storage.get(&DataKey::User(user_id)).ok_or(Error::UserNotFound)
    }

    fn validate_username(username: String) -> bool {
        let username_length = username.len(); // This is u32
        if username_length == 0 || username_length > 16 {
            return false;
        }

        let mut buffer = [0u8; 16];
        // Convert username_length to usize for slice indexing
        username.copy_into_slice(&mut buffer[..username_length as usize]);

        // Also need to convert for the validation loop
        for &b in buffer[..username_length as usize].iter() {
            if !(b.is_ascii_alphanumeric() || b == b'_') {
                return false;
            }
        }
        true
    }
}

