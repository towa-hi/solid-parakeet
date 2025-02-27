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
pub type Team = u32;
pub type Rank = u32;
// endregion
// region errors
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
}

// endregion
// region enums

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub enum Phase {
    Uninitialized = 0,
    Setup = 1,
    Movement = 2,
    Commitment = 3,
    Resolve = 4,
    Ending = 5,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub enum UserLobbyState {
    NotAccepted = 0,
    InLobby = 1,
    Ready = 2,
    InGame = 3,
}

// endregion
// region level 0 structs

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MoveIndex {
    pub lobby_id: LobbyGuid,
    pub user_address: UserAddress,
    pub turn: u32,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct User {
    // immutable
    pub index: UserAddress,
    // mutable
    pub name: String,
    pub games_completed: u32,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Pos {
    pub x: i32,
    pub y: i32,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PawnDef {
    pub name: String,
    pub rank: Rank,
    pub power: u32,
    pub movement_range: u32,
}

// endregion
// region level 1 structs

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Tile {
    pub pos: Pos,
    pub is_passable: bool,
    pub setup_team: Team,
    pub auto_setup_zone: u32,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PawnCommitment {
    pub pawn_id: PawnGuid,
    pub starting_pos: Pos,
    pub pawn_def_hash: PawnDefHash, // hash of the def of that pawn in case there's a conflict
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Pawn {
    // immutable
    pub pawn_id: PawnGuid,
    pub user_address: UserAddress,
    pub team: Team,
    // mutable
    pub pos: Pos,
    pub is_alive: bool,
    pub is_moved: bool,
    pub is_revealed: bool,
    // unknown until revealed
    pub pawn_def: PawnDef,
}

// endregion
// region level 2 structs

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct BoardDef {
    pub name: String,
    pub size: Pos,
    pub tiles: Map<Pos, Tile>,
    pub is_hex: bool,
    pub default_max_pawns: Map<Rank, u32>,
}

// endregion
// region level 3 structs

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct LobbyParameters {
    pub board_def: BoardDef,
    pub must_fill_all_tiles: bool,
    pub max_pawns: Map<Rank, u32>,
    pub dev_mode: bool,
    pub security_mode: bool,
}


// endregion
// region level 4 structs
#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Invite {
    pub host_address: UserAddress,
    pub guest_address: UserAddress,
    pub sent_ledger: u32,
    pub ledgers_until_expiration: u32,
    pub expiration_ledger: u32,
    pub parameters: LobbyParameters,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Lobby {
    // immutable
    pub index: LobbyGuid,
    pub host_address: UserAddress,
    pub guest_address: UserAddress,
    pub parameters: LobbyParameters,
    // mutable
    pub user_lobby_states: Map<UserAddress, UserLobbyState>, // contains user specific state, changes when another user joins or leaves
    // game state
    pub game_end_state: u32,
    pub setup_commitments: Map<UserAddress, Map<PawnCommitment, ()>>,
    pub turn: u32,
    pub phase: Phase,
}

// endregion
// region events

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct InviteEvent {
    pub invite: Invite,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct InviteAcceptedEvent {
    pub lobby: Lobby,
}
//endregion

// region requests

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct SendInviteReq {
    pub host_address: UserAddress,
    pub guest_address: UserAddress,
    pub ledgers_until_expiration: u32,
    pub parameters: LobbyParameters,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct AcceptInviteReq {
    pub host_address: UserAddress,
}

// NOTE: DenyInviteReq is not in here as it is pointless and costs gas until we have a sponsorship model

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct CancelInviteReq {
    pub host_address: UserAddress,
    pub guest_address: UserAddress,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct LeaveLobbyReq {
    pub host_address: UserAddress,
    pub guest_address: UserAddress,
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct SetupCommitReq {
    pub lobby_id: LobbyGuid,
    pub setup_commitments: Map<PawnCommitment, ()>
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MoveCommitReq {
    pub lobby: LobbyGuid,
    pub user_address: UserAddress,
    pub turn: u32,
    pub pawn_id_hash: PawnGuidHash,
    pub move_pos_hash: PosHash, // hash of the Pos it's moving to
}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MoveSubmitReq {
    pub lobby: LobbyGuid,
    pub user_address: UserAddress,
    pub turn: u32,
    pub pawn_id: PawnGuid,
    pub move_pos: Pos,
    pub pawn_def: PawnDef,
}

// endregion
// region responses


// endregion


// region keys

pub type PendingInvites = Map<UserAddress, InviteEvent>;
pub type AllInvites = Map<UserAddress, u32>;

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub enum DataKey {
    // global
    Admin,

    // user data
    User(UserAddress),

}

#[contracttype]
#[derive(Clone, Debug, Eq, PartialEq)]
pub enum TempKey {
    PendingInvites(UserAddress), // guests (the recipient) address

    // lobby specific data
    Lobby(LobbyGuid),
}

// endregion
// region events

pub const EVENT_REGISTER: Symbol = symbol_short!("USERREG");
pub const EVENT_UPDATE: Symbol = symbol_short!("USERUPD");
pub const EVENT_INVITE: Symbol = symbol_short!("INVITE");
pub const EVENT_START: Symbol = symbol_short!("START");

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
        env.storage().instance().set(&DataKey::Admin, &admin);
        Ok(())
    }

    pub fn upgrade(env: Env, new_wasm_hash: BytesN<32>) {
        let admin: Address = env.storage().instance().get(&DataKey::Admin).unwrap();
        admin.require_auth();
        env.deployer().update_current_contract_wasm(new_wasm_hash);
    }

    pub fn send_invite(env: Env, address: Address, req: SendInviteReq) -> Result<(), Error> {
        const MAX_EXPIRATION_LEDGERS: u32 = 1000;
        address.require_auth();
        if req.host_address != address.to_string() {
            return Err(Error::InvalidAddress)
        }
        // TODO: validate addresses
        if (req.host_address == req.guest_address) || (req.ledgers_until_expiration > MAX_EXPIRATION_LEDGERS) {
            return Err(Error::InvalidArgs)
        }
        // if user is already registered, get their user struct or create a new one for them
        let _host_user = Self::get_or_make_user(&env, &req.host_address);
        let _guest_user = Self::get_or_make_user(&env, &req.guest_address);
        if req.ledgers_until_expiration > MAX_EXPIRATION_LEDGERS {
            return Err(Error::InvalidExpirationLedger)
        }
        // TODO: validate parameters
        let current_ledger = env.ledger().sequence();
        let invite_event = InviteEvent {
            invite: Invite {
                host_address: req.host_address.clone(),
                guest_address: req.guest_address.clone(),
                sent_ledger: current_ledger,
                ledgers_until_expiration: req.ledgers_until_expiration,
                expiration_ledger: current_ledger + req.ledgers_until_expiration,
                parameters: req.parameters,
            },
        };
        Self::update_invites(&env, &invite_event);
        env.events().publish((EVENT_INVITE, req.host_address, req.guest_address), invite_event);
        Ok(())
    }

    pub fn accept_invite(env: Env, address: Address, req: AcceptInviteReq) -> Result<(), Error> {
        address.require_auth();
        let temp = env.storage().temporary();
        let pending_invites_key = TempKey::PendingInvites(address.to_string());
        // see if invite from this user exists in pending invites
        let mut pending_invites: PendingInvites = temp.get(&pending_invites_key).unwrap_or_else(|| PendingInvites::new(&env));
        let invite_event = match pending_invites.get(req.host_address.clone()) {
            Some(v) => v,
            None => return Err(Error::InviteNotFound),
        };
        let invite = invite_event.invite;
        let current_ledger = env.ledger().sequence();
        if invite.expiration_ledger < current_ledger {
            return Err(Error::InvalidExpirationLedger)
        }
        // remove invite
        pending_invites.remove(req.host_address.clone());
        temp.set(&pending_invites_key, &pending_invites);
        // make a lobby from the invite
        let lobby_id = Self::generate_uuid(&env, invite.host_address.clone(), current_ledger);
        let mut user_lobby_states: Map<UserAddress, UserLobbyState> = Map::new(&env);
        user_lobby_states.set(invite.host_address.clone(), UserLobbyState::InLobby);
        user_lobby_states.set(invite.guest_address.clone(), UserLobbyState::InLobby);
        let setup_commitments: Map<UserAddress, Map<PawnCommitment, ()>> = Map::new(&env); // intentionally empty
        let lobby = Lobby {
            index: lobby_id.clone(),
            host_address: invite.host_address.clone(),
            guest_address: invite.guest_address.clone(),
            parameters: invite.parameters,
            user_lobby_states: user_lobby_states,
            game_end_state: 0,
            setup_commitments: setup_commitments,
            turn: 0,
            phase: Phase::Setup,
        };
        let lobby_key = TempKey::Lobby(lobby_id.clone());
        temp.set(&lobby_key, &lobby.clone());
        temp.extend_ttl(&lobby_key, 0, 999);
        let invite_accepted_event = InviteAcceptedEvent {
            lobby: lobby.clone(),
        };
        env.events().publish((EVENT_START, invite.host_address, invite.guest_address, lobby_id), invite_accepted_event);
        Ok(())
    }

    pub fn commit_setup(env: Env, address: Address, req: SetupCommitReq) -> Result<(), Error> {
        let temp = env.storage().temporary();
        let lobby_key = TempKey::Lobby(req.lobby_id.clone());
        let mut lobby: Lobby = match temp.get(&lobby_key) {
            Some(v) => v,
            None => return Err(Error::LobbyNotFound),
        };

        // validate commitment
        if !Self::validate_setup_commitment(&env, &req.setup_commitments, &lobby.parameters) {
            return Err(Error::InvalidArgs);
        }
        lobby.setup_commitments.set(address.to_string(), req.setup_commitments);
        if (lobby.setup_commitments.keys().len() == 2)
        {
            // TODO: start game
        }



        Ok(())
    }

    pub(crate) fn get_or_make_user(e: &Env, user_address: &UserAddress) -> User {
        let storage = e.storage().persistent();
        let key = DataKey::User(user_address.clone());
        let user = if storage.has(&key) {
            storage.get(&DataKey::User(user_address.clone())).unwrap()
        } else {
            let new_user = User {
                index: user_address.clone(),
                name: String::from_str(e, "default name"),
                games_completed: 0,
            };
            storage.set(&key, &new_user);
            new_user
        };
        user
    }

    // TODO: make this function take in pending invites instead of calling storage
    pub(crate) fn update_invites(e: &Env, new_invite_event: &InviteEvent) {
        let temp = e.storage().temporary();
        let current_ledger = e.ledger().sequence();
        let new_invite = new_invite_event.invite.clone();
        let key = TempKey::PendingInvites(new_invite.guest_address.clone());
        let mut pending_invites: PendingInvites = temp.get(&key).unwrap_or_else(|| PendingInvites::new(e));
        // prune pending invites
        let mut changed = false;
        for (invite_address, pending_invite_event) in pending_invites.iter() {
            if pending_invite_event.invite.expiration_ledger < current_ledger {
                pending_invites.remove(invite_address.clone());
                changed = true;
            }
        }
        // if new_invite isn't already expired, add it to pending
        if new_invite.expiration_ledger >= current_ledger {
            pending_invites.set(new_invite.host_address.clone(), new_invite_event.clone());
            changed = true;
        }
        if changed {
            temp.set(&key, &pending_invites);
            temp.extend_ttl(&key, 0, new_invite.ledgers_until_expiration);
        }
    }

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

    pub(crate) fn generate_uuid(e: &Env, salt_string: String, salt_int: u32) -> String {
        const DASH_POSITIONS: [usize; 4] = [8, 13, 18, 23];
        let mut combined = Bytes::new(e);
        combined.append(&salt_string.to_xdr(e));
        combined.append(&e.ledger().timestamp().to_xdr(e));
        combined.append(&e.ledger().sequence().to_xdr(e));
        combined.append(&salt_int.to_xdr(e));
        // TODO: add a time based salt
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

    fn validate_setup_commitment(e: &Env, setup_commitments: &Map<PawnCommitment, ()>, parameters: &LobbyParameters) -> bool {
        for pawn in setup_commitments.keys().iter() {
            if !Self::validate_pos(e, &pawn.starting_pos, &parameters.board_def) {
                return false;
            }
        }
        true
    }

    fn validate_pos(e: &Env, pos: &Pos, board_def: &BoardDef) -> bool {
        let tile = match board_def.tiles.get(pos.clone()) {
            Some(v) => v,
            None => return false,
        };
        tile.is_passable
    }

}
// endregion

mod test;