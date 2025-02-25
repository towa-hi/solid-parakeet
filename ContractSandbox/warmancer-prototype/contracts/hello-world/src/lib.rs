#![no_std]


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

    pub fn test_set_lobby(env: Env, user_id: Address, lobby: Lobby) -> Result<Lobby, Error> {
        let storage = env.storage().persistent();
        let mut user = storage.get(&DataKey::User(user_id.clone())).clone();
        storage.set(&user_id.clone(), &user);
        user.ok_or(Error::UserNotFound)
    }

    pub fn test_get_lobby(env: Env, user_id: Address) -> Result<Lobby, Error> {
        let mut max_pawns: Map<u32, u32> = Map::new(&env);
        max_pawns.set(1, 111);
        max_pawns.set(2, 111);
        max_pawns.set(3, 111);
        max_pawns.set(4, 111);
        let test_pawn: Pawn = Pawn {
            pawn_id: String::from_str(&env, "test pawn id"),
            user: user_id.clone(),
            team: 0,
            def_hidden: String::from_str(&env, "def_hidden string"),
            def_key: String::from_str(&env, "def_key string"),
            def: PawnDef {
                def_id: String::from_str(&env, "pawndef def_id string"),
                rank: 123,
                name: String::from_str(&env, "pawndef name string"),
                power: 456,
                movement_range: 789,
            },
            pos: Vector2Int { x:9, y:8},
            is_alive: true,
            is_moved: false,
            is_revealed: false,
        };
        let test_pawn_two: Pawn = Pawn {
            pawn_id: String::from_str(&env, "test pawn two id"),
            user: user_id.clone(),
            team: 0,
            def_hidden: String::from_str(&env, "def_hidden string"),
            def_key: String::from_str(&env, "def_key string"),
            def: PawnDef {
                def_id: String::from_str(&env, "pawnDef def_id string"),
                rank: 123,
                name: String::from_str(&env, "pawnDef name string"),
                power: 456,
                movement_range: 789,
            },
            pos: Vector2Int { x:5, y:6},
            is_alive: true,
            is_moved: false,
            is_revealed: false,
        };
        let mut pawns: Map<String, Pawn> = Map::new(&env);
        pawns.set(test_pawn.pawn_id.clone(), test_pawn);
        pawns.set(test_pawn_two.pawn_id.clone(), test_pawn_two);
        let mut new_lobby = Lobby {
            lobby_id: String::from_str(&env, "test lobby id"),
            host: user_id.clone(),
            board_def: BoardDef {
                name: String::from_str(&env, "board def name"),
                size: Vector2Int { x: 10, y: 10 },
                tiles: Map::new(&env),
                is_hex: false,
                default_max_pawns: Map::new(&env),
            },
            must_fill_all_tiles: false,
            max_pawns: max_pawns,
            is_secure: false,
            user_states: Vec::from_array(
                &env,
          [
                    UserState {
                        user_id: user_id.clone(),
                        team: 0,
                    },
                    UserState {
                        user_id: user_id.clone(),
                        team: 0,
                    },
                ],
            ),
            game_end_state: 0,
            pawns: pawns,
        };
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

    pub(crate) fn generate_uuid(env: &Env, salt_string: String, salt_int: u32) -> String {
        const DASH_POSITIONS: [usize; 4] = [8, 13, 18, 23];
        let mut combined = Bytes::new(env);
        combined.append(&salt_string.to_xdr(env));
        combined.append(&env.ledger().timestamp().to_xdr(env));
        combined.append(&env.ledger().sequence().to_xdr(env));
        combined.append(&salt_int.to_xdr(env));
        // TODO: add a time based salt
        // hash combined bytes
        let mut bytes = env.crypto().sha256(&combined).to_array();

        // force "version 4" in bytes[6]
        bytes[6] = (bytes[6] & 0x0f) | 0x40;
        // force "variant 1" in bytes[8]
        bytes[8] = (bytes[8] & 0x3f) | 0x80;

        // output is 32 hex digits + 4 dashes
        let mut output = [0u8; 36];

        let mut cursor = 0;
        // convert only the first 16 bytes to hex (the standard UUID size)
        for i in 0..16 {
            // insert dash if at a dash position
            if DASH_POSITIONS.contains(&cursor) {
                output[cursor] = b'-';
                cursor += 1;
            }

            // convert high nibble
            let high = (bytes[i] >> 4) & 0x0f;
            output[cursor] = if high < 10 {
                high + b'0'
            } else {
                (high - 10) + b'a'
            };
            cursor += 1;

            // insert dash if next position is a dash
            if DASH_POSITIONS.contains(&cursor) {
                output[cursor] = b'-';
                cursor += 1;
            }

            // convert low nibble
            let low = bytes[i] & 0x0f;
            output[cursor] = if low < 10 {
                low + b'0'
            } else {
                (low - 10) + b'a'
            };
            cursor += 1;
        }

        String::from_bytes(env, &output)
    }
}

// endregion

mod test;