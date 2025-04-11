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
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum UserLobbyState {
    NotAccepted = 0,
    InLobby = 1,
    Ready = 2,
    InGame = 3,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum Team {
    None = 0,
    Red = 1,
    Blue = 2,
}

// endregion
// region level 0 structs
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
    pub string: i32,
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
    pub board_def: BoardDef,
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
pub struct Invite {
    pub expiration_ledger: i32,
    pub guest_address: UserAddress,
    pub host_address: UserAddress,
    pub ledgers_until_expiration: i32,
    pub sent_ledger: i32,
    pub parameters: LobbyParameters,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Lobby {
    pub game_end_state: i32,
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
pub const EVENT_INVITE: &str = "EVENT_INVITE";
pub const EVENT_INVITE_ACCEPT: &str = "EVENT_INVITE_ACCEPT";
pub const EVENT_SETUP_START: &str = "EVENT_SETUP_START";
pub const EVENT_SETUP_END: &str = "EVENT_SETUP_END";

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct EventUpdateUser { pub user: User, }
//endregion

// region requests

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MakeLobbyReq {
    pub host_address: UserAddress,
    pub parameters: LobbyParameters,
    pub salt: u32,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct SendInviteReq {
    pub guest_address: UserAddress,
    pub host_address: UserAddress,
    pub ledgers_until_expiration: i32,
    pub parameters: LobbyParameters,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct AcceptInviteReq {
    pub host_address: UserAddress,
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

}
pub type PendingInvites = Map<UserAddress, Invite>;
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum TempKey {
    PendingInvites(UserAddress), // guests (the recipient) address
    Lobby(LobbyGuid), // lobby specific data
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

    pub fn test_send_invite_req(env: Env, address: Address, req: SendInviteReq) -> Result<SendInviteReq, Error> {
        Ok(req.clone())
    }

    pub fn make_lobby(env: Env, address: Address, req: MakeLobbyReq) -> Result<LobbyGuid, Error> {
        let persistent = &env.storage().persistent();
        let temporary = &env.storage().temporary();
        let mut host_user = Self::get_or_make_user(&env, &req.host_address);
        if !host_user.current_lobby.is_empty()
        {
            return Err(Error::HostAlreadyInLobby)
        }
        let lobby_id:LobbyGuid = Self::generate_uuid(&env, req.salt);
        let lobby = Lobby {
            game_end_state: 0,
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
                team: Team::None,
                user_address: host_user.index.clone(),
            },
            index: lobby_id.clone(),
            parameters: req.parameters,
            pawns: Vec::new(&env),
            phase: Phase::Uninitialized,
            turns: Vec::new(&env),
        };
        let lobby_key = TempKey::Lobby(lobby_id.clone());
        temporary.set(&lobby_key, &lobby);
        host_user.current_lobby = lobby_id.clone();
        Ok(lobby_id)
    }

    pub fn send_invite(env: Env, address: Address, req: SendInviteReq) -> Result<(), Error> {
        address.require_auth();
        const MAX_EXPIRATION_LEDGERS: i32 = 1000;
        let temp = env.storage().temporary();
        if req.host_address != address.to_string() {
            return Err(Error::InvalidAddress)
        }
        // TODO: check if guest_address is a valid account
        if (req.host_address == req.guest_address) || (req.ledgers_until_expiration > MAX_EXPIRATION_LEDGERS) {
            return Err(Error::InvalidArgs)
        }
        // if user is already registered, get their user struct or create a new one for them
        let mut host_user = Self::get_or_make_user(&env, &req.host_address);
        let guest_user = Self::get_or_make_user(&env, &req.guest_address);

        // TODO: prevent users from adding non existent guest users
        // TODO: validate parameters
        let current_ledger = env.ledger().sequence() as i32;
        let invite = Invite {
                host_address: req.host_address.clone(),
                guest_address: req.guest_address.clone(),
                sent_ledger: current_ledger,
                ledgers_until_expiration: req.ledgers_until_expiration,
                expiration_ledger: current_ledger + req.ledgers_until_expiration,
                parameters: req.parameters,
        };
        // add invite to guest's PendingInvites and prune dead invites for guest
        let pending_invites_key = TempKey::PendingInvites(req.guest_address);
        let mut pruned_pending_invites = temp.get(&pending_invites_key)
            .map(|invites| Self::prune_pending_invites(&env, &invites))
            .unwrap_or_else(|| PendingInvites::new(&env));
        pruned_pending_invites.set(req.host_address, invite);
        temp.set(&pending_invites_key, &pruned_pending_invites);
        temp.extend_ttl(&pending_invites_key, 0, req.ledgers_until_expiration as u32);
        Ok(())
    }

    pub fn accept_invite(env: Env, address: Address, req: AcceptInviteReq) -> Result<String, Error> {
        address.require_auth();

        let temp = env.storage().temporary();
        let pending_invites_key = TempKey::PendingInvites(address.to_string());
        // see if invite from this user exists in pending invites
        let pending_invites: PendingInvites = temp.get(&pending_invites_key).unwrap_or_else(|| PendingInvites::new(&env));
        let mut pruned_pending_invites = Self::prune_pending_invites(&env, &pending_invites);
        let invite = match pruned_pending_invites.get(req.host_address.clone()) {
            Some(v) => v,
            None => return Err(Error::InviteNotFound),
        };
        // remove invite
        pruned_pending_invites.remove(req.host_address.clone());
        temp.set(&pending_invites_key, &pending_invites);
        // make a lobby from the invite
        let lobby_id = Self::generate_uuid(&env, env.ledger().sequence());
        let host_state = UserState {
            lobby_state: UserLobbyState::InGame,
            setup_commitments: Vec::new(&env),
            team: Team::Red,
            user_address: invite.host_address.clone(),
        };
        let guest_state = UserState {
            lobby_state: UserLobbyState::InGame,
            setup_commitments: Vec::new(&env),
            team: Team::Blue,
            user_address: invite.guest_address.clone(),
        };
        let lobby = Lobby {
            index: lobby_id.clone(),
            host_address: invite.host_address.clone(),
            guest_address: invite.guest_address.clone(),
            parameters: invite.parameters,
            host_state: host_state,
            guest_state: guest_state,
            game_end_state: 0,
            phase: Phase::Setup,
            pawns: Vec::new(&env),
            turns: Vec::new(&env),
        };
        let lobby_key = TempKey::Lobby(lobby_id.clone());
        temp.set(&lobby_key, &lobby.clone());
        temp.extend_ttl(&lobby_key, 0, 999);
        Ok(lobby_id)
    }

    pub fn commit_setup(env: Env, address: Address, req: SetupCommitReq) -> Result<(), Error> {
        address.require_auth();

        let temp = env.storage().temporary();
        let lobby_key = TempKey::Lobby(req.lobby_id.clone());
        let mut lobby: Lobby = match temp.get(&lobby_key) {
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
        temp.set(&lobby_key, &lobby);
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

    pub(crate) fn prune_pending_invites(e: &Env, pending_invites: &PendingInvites) -> PendingInvites
    {
        let mut new_pending_invites = PendingInvites::new(e);
        let current_ledger = e.ledger().sequence() as i32;
        for (invite_address, invite) in pending_invites.iter() {
            if invite.expiration_ledger >= current_ledger {
                new_pending_invites.set(invite_address, invite);
            }
        }
        new_pending_invites
    }

    pub(crate) fn generate_uuid(e: &Env, salt_int: u32) -> String {
        let random_number: u64 = e.prng().gen();
        const DASH_POSITIONS: [usize; 4] = [8, 13, 18, 23];
        let mut combined = Bytes::new(e);
        combined.append(&random_number.to_xdr(e));
        combined.append(&e.ledger().sequence().to_xdr(e));
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