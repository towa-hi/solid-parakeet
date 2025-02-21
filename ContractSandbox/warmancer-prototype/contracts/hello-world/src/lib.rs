#![no_std]

extern crate alloc;

use soroban_sdk::*;
use soroban_sdk::xdr::*;
// region global state defs

pub type AllUserIds = Map<Address, ()>;
pub type AllLobbyIds = Map<String, ()>;

// endregion
// region errors
#[contracterror]
#[derive(Copy, Clone, Debug, Eq, PartialEq, PartialOrd, Ord)]
pub enum Error {
    UserNotFound = 1,
    InvalidUsername = 2,
}

// endregion
// region level 0 structs

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct User {
    // immutable
    pub user_id: Address,
    // mutable
    pub name: String,
    pub games_played: u32,
    pub current_lobby: Option<String>,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Vector2Int {
    pub x: i32,
    pub y: i32,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct UserState {
    // immutable for now
    pub user_id: Address,
    pub team: u32,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PawnDef {
    pub def_id: String,
    pub rank: u32,
    pub name: String,
    pub power: u32,
    pub movement_range: u32,
}

// endregion
// region level 1 structs

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Tile {
    pub pos: Vector2Int,
    pub is_passable: bool,
    pub setup_team: u32,
    pub auto_setup_zone: u32,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PawnCommitment {
    pub user_id: Address,
    pub pawn_id: String,
    pub pos: Vector2Int,
    pub def_hidden: String,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Pawn {
    // immutable
    pub pawn_id: String,
    pub user: Address,
    pub team: u32,
    pub def_hidden: String,
    // mutable
    pub def_key: String,
    pub def: PawnDef,
    pub pos: Vector2Int,
    pub is_alive: bool,
    pub is_moved: bool,
    pub is_revealed: bool,
}

// endregion
// region level 2 structs

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
pub struct SetupCommitment {
    pub user_id: Address,
    pub pawn_commitments: Vec<PawnCommitment>,
}

// endregion
// region level 3 structs

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Lobby {
    // immutable
    pub lobby_id: String,
    pub host: Address,
    pub board_def: BoardDef,
    pub must_fill_all_tiles: bool,
    pub max_pawns: Map<u32, u32>,
    pub is_secure: bool,
    // mutable
    pub user_states: Vec<UserState>, // contains user specific state, changes when another user joins or leaves
    // game state
    pub game_end_state: u32,
    pub pawns: Map<String, Pawn>,
}

// endregion
// region keys
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
    Lobby(String),
    SetupCommitments(String),
}

// endregion
// region events

pub const EVENT_REGISTER: &str = "register user";
pub const EVENT_UPDATE: &str = "update user";

// endregion
// region contract

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

    pub fn create_lobby(_env: Env, user_id: Address, lobby: Lobby) -> Result<Lobby, Error> {
        let mut new_lobby = lobby.clone();
        user_id.require_auth();
        new_lobby.lobby_id = Self::generate_uuid(&_env, user_id.to_string(), 0);


        Ok(new_lobby)
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

    pub(crate) fn generate_uuid(env: &Env, invoker: String, salt: u32) -> String {
        let mut combined = Bytes::new(env);

        // Convert values into bytes and append them
        combined.append(&invoker.to_xdr(env));
        combined.append(&env.ledger().timestamp().to_xdr(env));
        combined.append(&env.ledger().sequence().to_xdr(env));
        combined.append(&salt.to_xdr(env));

        // Hash the combined bytes
        let hash: BytesN<32> = env.crypto().sha256(&combined).to_bytes();
        let mut bytes = hash.to_array();

        // Force "version 4" in byte[6] (the 7th byte)
        bytes[6] = (bytes[6] & 0x0f) | 0x40;
        // Force "variant 1" in byte[8] (the 9th byte)
        bytes[8] = (bytes[8] & 0x3f) | 0x80;

        // We'll produce a 36-char output: 32 hex digits + 4 dashes
        let mut output = [0u8; 36];
        // Insert dashes at these positions in the final string
        let dash_positions = [8, 13, 18, 23];

        let mut out_i = 0;
        // Convert only the first 16 bytes to hex (the standard UUID size)
        for i in 0..16 {
            // Insert dash if we're at a dash position
            if dash_positions.contains(&out_i) {
                output[out_i] = b'-';
                out_i += 1;
            }

            // Convert high nibble
            let high = (bytes[i] >> 4) & 0x0f;
            output[out_i] = if high < 10 {
                high + b'0'
            } else {
                (high - 10) + b'a'
            };
            out_i += 1;

            // Insert dash if next position is a dash
            if dash_positions.contains(&out_i) {
                output[out_i] = b'-';
                out_i += 1;
            }

            // Convert low nibble
            let low = bytes[i] & 0x0f;
            output[out_i] = if low < 10 {
                low + b'0'
            } else {
                (low - 10) + b'a'
            };
            out_i += 1;
        }

        // Convert the 36-byte array into a Soroban String
        String::from_bytes(env, &output)
    }
}

// endregion

mod test;