#![no_std]
use soroban_sdk::*;
use soroban_sdk::xdr::*;
// region global state defs
pub type UserAddress = String;
pub type LobbyGuid = String;
pub type PawnGuid = String;
pub type PawnGuidHash = String;
pub type PawnDefHash = String;
pub type PosHash = String;
pub type Rank = i32;
// endregion
// region enums errors
#[contracterror]
#[derive(Copy, Clone, Debug, Eq, PartialEq, PartialOrd, Ord)]
pub enum Error {
    UserNotFound = 1,
    InvalidUsername = 2,
    AlreadyInitialized = 3,
    InvalidAddress = 4,
    InvalidExpirationLedger = 5,
    InvalidArgs = 6,
    InviteNotFound = 7,
    LobbyNotFound = 8,
    WrongPhase = 9,
    HostAlreadyInLobby = 10,
    GuestAlreadyInLobby = 11,
    LobbyNotJoinable = 12,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum InviteStatus {
    None = 0,
    Sent = 1,
    Accepted = 2,
    Rejected = 3,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum Phase {
    Uninitialized = 0,
    Setup = 1,
    Movement = 2,
    Commitment = 3,
    Resolve = 4,
    Ending = 5,
    Aborted = 6,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum UserLobbyState {
    NotAccepted = 0,
    InLobby = 1,
    InGame = 2,
    Left= 3,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum Team {
    None = 0,
    Red = 1,
    Blue = 2,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum EndState {
    Tie = 0,
    Red = 1,
    Blue = 2,
    Playing = 3,
    Aborted = 4,
}

// endregion
// region level 0 structs
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct LobbyRecord {
    pub end_ledger: u32,
    pub guest_address: UserAddress,
    pub host_address: UserAddress,
    pub index: String,
    pub lobby_id: LobbyGuid,
    pub start_ledger: u32,
    pub winner: UserAddress,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct User {
    pub current_lobby: LobbyGuid,
    pub games_completed: i32,
    pub index: UserAddress,
    pub name: String,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Pos {
    pub x: i32,
    pub y: i32,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct UserState {
    pub lobby_state: UserLobbyState,
    pub setup_commitments: Vec<PawnCommitment>,
    pub team: Team,
    pub user_address: UserAddress,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PawnDef {
    pub id: i32,
    pub movement_range: i32,
    pub name: String,
    pub power: i32,
    pub rank: Rank,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MaxPawns {
    pub max: i32,
    pub rank: Rank,
}

// endregion
// region level 1 structs
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Tile {
    pub auto_setup_zone: i32,
    pub is_passable: bool,
    pub pos: Pos,
    pub setup_team: Team,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PawnCommitment {
    pub pawn_id: PawnGuid,
    pub starting_pos: Pos,
    pub pawn_def_hash: PawnDefHash, // hash of the def of that pawn in case there's a conflict
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Pawn {
    pub is_alive: bool,
    pub is_moved: bool,
    pub is_revealed: bool,
    pub pawn_def: PawnDef,
    pub pawn_id: PawnGuid,
    pub pos: Pos,
    pub team: Team,
    pub user_address: UserAddress,
}
// endregion
// region level 2 structs
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct BoardDef {
    pub default_max_pawns: Vec<MaxPawns>,
    pub is_hex: bool,
    pub name: String,
    pub size: Pos,
    pub tiles: Vec<Tile>,
}
// endregion
// region level 3 structs
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct LobbyParameters {
    pub board_def_name: String,
    pub dev_mode: bool,
    pub max_pawns: Vec<MaxPawns>,
    pub must_fill_all_tiles: bool,
    pub security_mode: bool,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct TurnMove {
    pub initialized: bool,
    pub pawn_id: PawnGuid,
    pub pos: Pos,
    pub turn: i32,
    pub user_address: UserAddress,
}
// endregion
// region level 4 structs

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Turn {
    pub guest_turn: TurnMove,
    pub host_turn: TurnMove,
    pub turn: i32,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Lobby {
    pub game_end_state: EndState,
    pub guest_address: UserAddress,
    pub guest_state: UserState,
    pub host_address: UserAddress,
    pub host_state: UserState,
    pub index: LobbyGuid,
    pub parameters: LobbyParameters,
    pub pawns: Vec<Pawn>,
    pub phase: Phase,
    pub turns: Vec<Turn>,
}

// endregion
// region events
pub const EVENT_UPDATE_USER: &str = "EVENT_UPDATE_USER";
pub const EVENT_INVITE_ACCEPT: &str = "EVENT_INVITE_ACCEPT";
pub const EVENT_SETUP_START: &str = "EVENT_SETUP_START";
pub const EVENT_SETUP_END: &str = "EVENT_SETUP_END";
pub const EVENT_USER_LEFT: &str = "EVENT_USER_LEFT";
//endregion

// region requests

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MakeLobbyReq {
    pub host_address: UserAddress,
    pub parameters: LobbyParameters,
    pub salt: u32,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct JoinLobbyReq {
    pub guest_address: UserAddress,
    pub lobby_id: LobbyGuid,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct LeaveLobbyReq {
    pub guest_address: UserAddress,
    pub host_address: UserAddress,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct SetupCommitReq {
    pub lobby_id: LobbyGuid,
    pub setup_commitments: Vec<PawnCommitment>
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MoveCommitReq {
    pub lobby: LobbyGuid,
    pub move_pos_hash: PosHash, // hash of the Pos it's moving to
    pub pawn_id_hash: PawnGuidHash,
    pub turn: i32,
    pub user_address: UserAddress,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MoveSubmitReq {
    pub lobby: LobbyGuid,
    pub move_pos: Pos,
    pub pawn_id: PawnGuid,
    pub pawn_def: PawnDef,
    pub turn: i32,
    pub user_address: UserAddress,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct FlatTestReq {
    pub number: i32,
    pub word: String,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct NestedTestReq {
    pub number: i32,
    pub word: String,
    pub flat: FlatTestReq
}

// endregion
// region responses

// endregion
// region keys

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum DataKey {
    Admin, // Address
    User(UserAddress),
    Record(String),
    Lobby(LobbyGuid), // lobby specific data
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum TempKey {
    PendingInvites(UserAddress), // guests (the recipient) address
}
// endregion
// region contract
#[contract]
pub struct Contract;
#[contractimpl]
impl Contract {
    pub fn init(env: Env, admin: Address) -> Result<(), Error> {
        if env.storage().instance().has(&DataKey::Admin) {
            return Err(Error::AlreadyInitialized);
        }
        env.storage().persistent().set(&DataKey::Admin, &admin);
        Ok(())
    }

    pub fn upgrade(env: Env, new_wasm_hash: BytesN<32>) {
        let admin: Address = env.storage().instance().get(&DataKey::Admin).unwrap();
        admin.require_auth();
        env.deployer().update_current_contract_wasm(new_wasm_hash);
    }

    pub fn flat_param_test(env: Env, address: Address, req: FlatTestReq) -> Result<FlatTestReq, Error> {
        Ok(req.clone())
    }

    pub fn nested_param_test(env: Env, address: Address, req: NestedTestReq) -> Result<NestedTestReq, Error> {
        Ok(req.clone())
    }

    pub fn make_lobby(env: Env, address: Address, req: MakeLobbyReq) -> Result<LobbyGuid, Error> {
        address.require_auth();
        if address.to_string() != req.host_address {
            return Err(Error::InvalidArgs)
        }
        let persistent = &env.storage().persistent();
        //let temporary = &env.storage().temporary();
        let mut host_user = Self::get_or_make_user(&env, &req.host_address);
        if !host_user.current_lobby.is_empty() {
            let old_lobby_key = DataKey::Lobby(host_user.current_lobby.clone());
            if env.storage().persistent().has(&old_lobby_key) {
                return Err(Error::HostAlreadyInLobby);
            }
            else {
                host_user.current_lobby = String::from_str(&env, "");
            }
        }
        let lobby_id:LobbyGuid = Self::generate_uuid(&env, req.salt);
        let lobby = Lobby {
            game_end_state: EndState::Playing,
            guest_address: String::from_str(&env, ""),
            guest_state: UserState {
                lobby_state: UserLobbyState::NotAccepted,
                setup_commitments: Vec::new(&env),
                team: Team::Blue,
                user_address: String::from_str(&env, ""),
            },
            host_address: host_user.index.clone(),
            host_state: UserState {
                lobby_state: UserLobbyState::InLobby,
                setup_commitments: Vec::new(&env),
                team: Team::Red,
                user_address: host_user.index.clone(),
            },
            index: lobby_id.clone(),
            parameters: req.parameters,
            pawns: Vec::new(&env),
            phase: Phase::Uninitialized,
            turns: Vec::new(&env),
        };
        let lobby_key = DataKey::Lobby(lobby_id.clone());
        persistent.set(&lobby_key, &lobby);
        //persistent.extend_ttl(&lobby_key, 0, 17279);
        host_user.current_lobby = lobby_id.clone();
        let host_key = DataKey::User(host_user.index.clone());
        persistent.set(&host_key, &host_user);
        Ok(lobby_id)
    }

    pub fn leave_lobby(env: Env, address: Address) -> Result<bool, Error> {
        let user_address: UserAddress = address.to_string();
        address.require_auth();
        //let temporary = &env.storage().temporary();
        let persistent = &env.storage().persistent();
        let user_key = DataKey::User(user_address.clone());
        let mut user: User = match persistent.get(&user_key) {
            Some(thing) => thing,
            None => return Err(Error::UserNotFound),
        };
        if user.current_lobby.is_empty()
        {
            return Err(Error::LobbyNotFound)
        }
        let lobby_key = DataKey::Lobby(user.current_lobby.clone());
        //decouple user
        user.current_lobby = String::from_str(&env, "");
        persistent.set(&user_key, &user);
        // do not mutate user after this
        // update lobby
        let mut lobby: Lobby = match persistent.get(&lobby_key) {
            Some(thing) => thing,
            None => {
                return Err(Error::LobbyNotFound)
            },
        };
        let user_is_host = user.index == lobby.host_address;
        let other_user_address = if user_is_host {lobby.guest_address.clone()} else {lobby.host_address.clone()};
        if user_is_host {
            lobby.host_state.lobby_state = UserLobbyState::Left;
        }
        else {
            lobby.guest_state.lobby_state = UserLobbyState::Left;
        }
        lobby.game_end_state = EndState::Aborted;
        persistent.set(&lobby_key, &lobby);
        if !other_user_address.is_empty() {
            env.events().publish((EVENT_USER_LEFT, lobby.index, user.index.clone(), other_user_address), user.index);
        }
        Ok(true)
    }

    pub fn join_lobby(env: Env, address: Address, req: JoinLobbyReq) -> Result<bool, Error> {
        address.require_auth();
        if address.to_string() != req.guest_address {
            return Err(Error::InvalidArgs)
        }
        let persistent = &env.storage().persistent();
        let lobby_key = DataKey::Lobby(req.lobby_id.clone());
        let mut user: User = Self::get_or_make_user(&env, &req.guest_address);
        let user_key = DataKey::User(req.guest_address.clone());
        if !user.current_lobby.is_empty() {
            let old_lobby_key = DataKey::Lobby(user.current_lobby.clone());
            if env.storage().persistent().has(&old_lobby_key) {
                return Err(Error::GuestAlreadyInLobby);
            }
            user.current_lobby = String::from_str(&env, "");
            persistent.set(&user_key, &user);
        }
        let mut lobby: Lobby = match persistent.get(&lobby_key) {
            Some(thing) => thing,
            None => return Err(Error::LobbyNotFound),
        };
        // check if lobby is joinable
        if lobby.phase != Phase::Uninitialized {
            return Err(Error::LobbyNotJoinable)
        }
        if !lobby.guest_address.is_empty() {
            return Err(Error::LobbyNotJoinable)
        }
        if !lobby.guest_state.user_address.is_empty() {
            return Err(Error::LobbyNotJoinable)
        }
        if lobby.host_state.lobby_state != UserLobbyState::InLobby {
            return Err(Error::LobbyNotJoinable)
        }
        if lobby.host_address == req.guest_address {
            return Err(Error::LobbyNotJoinable)
        }
        // write lobby
        lobby.guest_address = req.guest_address.clone();
        lobby.guest_state = UserState {
            lobby_state: UserLobbyState::InGame,
            setup_commitments: Vec::new(&env),
            team: Team::Blue,
            user_address: req.guest_address.clone(),
        };
        lobby.host_state.lobby_state = UserLobbyState::InGame;
        lobby.phase = Phase::Setup;
        persistent.set(&lobby_key, &lobby);
        // write guest user
        user.current_lobby = req.lobby_id.clone();
        persistent.set(&user_key, &user);
        Ok(true)
    }

    pub fn commit_setup(env: Env, address: Address, req: SetupCommitReq) -> Result<(), Error> {
        address.require_auth();
        let persistent = env.storage().persistent();
        let lobby_key = DataKey::Lobby(req.lobby_id.clone());
        let mut lobby: Lobby = match persistent.get(&lobby_key) {
            Some(v) => v,
            None => return Err(Error::LobbyNotFound),
        };
        if lobby.phase != Phase::Setup {
            return Err(Error::WrongPhase);
        }
        // get state
        let user_address = address.to_string();
        let (mut user_state, other_user_committed) = if user_address == lobby.host_address {
            (lobby.host_state.clone(), !lobby.guest_state.setup_commitments.clone().is_empty())
        } else if user_address == lobby.guest_address {
            (lobby.guest_state.clone(), !lobby.host_state.setup_commitments.clone().is_empty())
        } else {
            return Err(Error::InvalidArgs);
        };
        user_state.setup_commitments = req.setup_commitments.clone();
        if other_user_committed
        {
            let unknown_pawn_def = PawnDef {
                id: 99,
                name: String::from_str(&env, "UNKNOWN"),
                rank: 99,
                power: 0,
                movement_range: 0,
            };
            let mut sneed: u32 = 0;
            for commitment in lobby.host_state.setup_commitments.clone() {
                let pawn = Pawn {
                    pawn_id: Self::generate_uuid(&env, sneed),
                    user_address: lobby.host_state.user_address.clone(),
                    team: lobby.host_state.team.clone(),
                    pos: commitment.starting_pos.clone(),
                    is_alive: true,
                    is_moved: false,
                    is_revealed: false,
                    pawn_def: unknown_pawn_def.clone(),
                };
                lobby.pawns.push_back(pawn);
                sneed += 1;
            }
            for commitment in lobby.guest_state.setup_commitments.clone() {
                let pawn = Pawn {
                    pawn_id: Self::generate_uuid(&env, sneed),
                    user_address: lobby.guest_state.user_address.clone(),
                    team: lobby.guest_state.team.clone(),
                    pos: commitment.starting_pos.clone(),
                    is_alive: true,
                    is_moved: false,
                    is_revealed: false,
                    pawn_def: unknown_pawn_def.clone(),
                };
                lobby.pawns.push_back(pawn);
                sneed += 1;
            }
            lobby.phase = Phase::Movement;
            let empty_pawn_id = String::from_str(&env, "Empty pawn Id");
            let empty_pos = Pos {
                x: -666,
                y: -666,
            };
            let first_turn = Turn {
                guest_turn: TurnMove {
                    initialized: false,
                    pawn_id: empty_pawn_id.clone(),
                    pos: empty_pos.clone(),
                    turn: 1,
                    user_address: lobby.guest_address.clone(),
                },
                host_turn: TurnMove {
                    initialized: false,
                    pawn_id: empty_pawn_id.clone(),
                    pos: empty_pos.clone(),
                    turn: 1,
                    user_address: lobby.host_address.clone(),
                },
                turn: 1,
            };
            lobby.turns.push_back(first_turn);
        }
        persistent.set(&lobby_key, &lobby);
        Ok(())
    }

    pub(crate) fn get_or_make_user(e: &Env, user_address: &UserAddress) -> User {
        let storage = e.storage().persistent();
        let key = DataKey::User(user_address.clone());
        storage.get(&key).unwrap_or_else(|| {
            let new_user = User {
                current_lobby: String::from_str(e, ""),
                games_completed: 0,
                index: user_address.clone(),
                name: String::from_str(e, "default name"),
            };
            storage.set(&key, &new_user);
            new_user
        })
    }

    pub(crate) fn generate_uuid(e: &Env, salt_int: u32) -> String {
        //let random_number: u64 = e.prng().gen();
        const DASH_POSITIONS: [usize; 4] = [8, 13, 18, 23];
        let mut combined = Bytes::new(e);
        //combined.append(&random_number.to_xdr(e));
        //combined.append(&e.ledger().sequence().to_xdr(e));
        combined.append(&salt_int.to_xdr(e));
        // hash combined bytes
        let mut bytes = e.crypto().sha256(&combined).to_array();
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
        String::from_bytes(e, &output)
    }

    /*
    pub(crate) fn validate_username(username: String) -> bool {
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
     */
}
// endregion

mod test;// run tests