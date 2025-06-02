#![no_std]
use soroban_sdk::*;
use soroban_sdk::xdr::*;

// region global state defs

pub type LobbyId = u32;
pub type PawnGuid = String;
pub type PawnGuidHash = String;
pub type PawnDefHash = String;
pub type PosHash = String;
pub type BoardHash = BytesN<32>;
pub type Rank = u32;
pub type Turn = u32;
pub type PackedUser = BytesN<8>;
pub type PackedLobbyInfo = BytesN<93>;
pub type ShortAddress = u64;
pub type SetupHash = BytesN<32>;

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
    TurnAlreadyInitialized = 13,
    TurnHashConflict = 14,
    LobbyAlreadyExists = 15,
    LobbyHasNoHost = 16,
    JoinerIsHost = 17,
    SetupStateNotFound = 18,
    IsUserHostError = 19,
    AlreadyCommittedSetup = 20,
    NotInLobby = 21,
    NoSetupCommitment = 22,
    NoOpponentSetupCommitment = 23,
    SetupHashFail = 24,
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
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum MailType {
    Taunt = 0,
    Message = 1,
    ProveSetupCommit = 2,
}
// endregion
// region level 0 structs
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Mail {
    pub mail_type: MailType,
    pub message: String,
    pub ledger: u32,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct User {
    pub current_lobby: LobbyId,
    pub games_completed: u32,
    pub index: Address,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MaxRank {
    pub max: u32,
    pub rank: Rank,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Pos {
    pub x: i32,
    pub y: i32,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PawnCommit {
    pub pawn_def_hash: PawnDefHash,
    pub pawn_id: PawnGuid,
    pub starting_pos: Pos,
}

// endregion
// region level 1 structs

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Mailbox {
    pub mail: Vec<Mail>,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Pawn {
    pub is_alive: bool,
    pub is_moved: bool,
    pub is_revealed: bool,
    pub pawn_def_hash: PawnDefHash,
    pub pawn_id: PawnGuid,
    pub pos: Pos,
    pub team: Team,
}
// endregion
// region level 2 structs
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct LobbyParameters {
    pub board_hash: BoardHash,
    pub dev_mode: bool,
    pub host_team: Team,
    pub max_ranks: Vec<MaxRank>,
    pub must_fill_all_tiles: bool,
    pub security_mode: bool,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct LobbyInfo {
    pub index: LobbyId,
    pub guest_address: Address,
    pub host_address: Address,
    pub phase: Phase,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct UserState {
    pub setup: Vec<PawnCommit>,
    pub setup_hash: SetupHash,
    pub setup_hash_salt: u32,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MoveState {
    pub turns: Vec<u32>
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
    pub lobby_id: LobbyId,
    pub parameters: LobbyParameters,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct JoinLobbyReq {
    pub lobby_id: LobbyId,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct LeaveLobbyReq {
    pub lobby_id: LobbyId,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct SetupCommitReq {
    pub lobby_id: LobbyId,
    pub setup_hash: SetupHash,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct ProveSetupReq {
    pub lobby_id: LobbyId,
    pub setup: Vec<PawnCommit>,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MoveCommitReq {
    pub lobby_id: LobbyId,
    pub move_pos_hash: PosHash, // hash of the Pos it's moving to
    pub pawn_id_hash: PawnGuidHash,
    pub turn: u32,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MoveSubmitReq {
    pub lobby: LobbyId,
    pub move_pos: Pos,
    pub pawn_id: PawnGuid,
    pub turn: u32,
    pub user_address: Address,
}

// endregion
// region keys

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum DataKey {
    Admin, // Address
    PackedUser(Address),
    LobbyInfo(LobbyId), // lobby specific data
    LobbyParameters(LobbyId),
    UserState(LobbyId, Address),
    Mailbox(Address),
}

// endregion
// region contract
#[contract]
pub struct Contract;
#[contractimpl]
impl Contract {
    pub fn make_lobby(e: &Env, address: Address, req: MakeLobbyReq) -> Result<(), Error> {
        address.require_auth();
        let persistent = e.storage().persistent();
        let temporary = e.storage().temporary();
        let mut host_user = Self::get_or_make_user(e, &address);
        // validation
        if host_user.current_lobby != 0 {
            return Err(Error::HostAlreadyInLobby)
        }
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        if temporary.has(&lobby_info_key) {
            return Err(Error::LobbyAlreadyExists)
        }
        // update
        host_user.current_lobby = req.lobby_id.clone();
        let lobby_info = LobbyInfo {
            index: req.lobby_id.clone(),
            guest_address: Self::empty_address(e),
            host_address: address.clone(),
            phase: Phase::Uninitialized,
        };
        // write
        // let packed_lobby_info = Self::pack_lobby_info(e, &lobby_info);
        // temporary.set(&lobby_info_key, &packed_lobby_info);
        temporary.set(&lobby_info_key, &lobby_info);
        let lobby_parameters_key = DataKey::LobbyParameters(req.lobby_id.clone());
        temporary.set(&lobby_parameters_key, &req.parameters);
        let user_key = DataKey::PackedUser(address);
        // let packed_user = Self::pack_user(e, &host_user);
        persistent.set(&user_key, &host_user);

        persistent.extend_ttl(&user_key, 259200, 518400);
        Ok(())
    }

    pub fn leave_lobby(e: &Env, address: Address) -> Result<(), Error> {
        address.require_auth();
        let persistent = e.storage().persistent();
        let temporary = e.storage().temporary();
        let packed_user_key = DataKey::PackedUser(address.clone());
        let mut user: User = match persistent.get(&packed_user_key) {
            // Some(packed_user) => Self::unpack_user(e, packed_user, &address),
            Some(user) => user,
            None => return Err(Error::UserNotFound), // TODO: make a version of this function that sends a lobbyId
        };
        let left_lobby = user.current_lobby.clone();
        // write user
        user.current_lobby = 0;
        // persistent.set(&packed_user_key, &Self::pack_user(e, &user));
        persistent.set(&packed_user_key, &user);
        // NOTE: we won't deal with extending user TTL here
        // persistent.extend_ttl(&packed_user_key, 8640, 8640);
        let lobby_info_key = DataKey::LobbyInfo(left_lobby);
        let mut lobby_info: LobbyInfo = match temporary.get(&lobby_info_key) {
            // Some(packed_lobby_info) => Self::unpack_lobby_info(e, packed_lobby_info),
            Some(lobby_info) => lobby_info,
            None => return Ok(()), // just write user and return if the lobby cant be found anymore
        };
        // update
        if address == lobby_info.host_address {
            lobby_info.host_address = Self::empty_address(e);
        }
        else if address == lobby_info.guest_address {
            lobby_info.guest_address = Self::empty_address(e);
        }
        else {
            return Ok(()) // if the user wasn't in this lobby then return
        }
        //TODO: handle detecting if the game is in progress to award victory/defeat
        lobby_info.phase = Phase::Aborted;

        // temporary.set(&lobby_info_key, &Self::pack_lobby_info(e, &lobby_info));
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.extend_ttl(&lobby_info_key, 8640, 8640);
        Ok(())
    }

    pub fn join_lobby(e: &Env, address: Address, req: JoinLobbyReq) -> Result<(), Error> {
        address.require_auth();
        let empty_hash = BytesN::from_array(e, &[0u8; 32]);
        let persistent = e.storage().persistent();
        let temporary = e.storage().temporary();
        let user_key = DataKey::PackedUser(address.clone());
        let mut user = Self::get_or_make_user(e, &address);
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        let mut lobby_info: LobbyInfo = match temporary.get(&lobby_info_key) {
            // Some(packed_lobby_info) => Self::unpack_lobby_info(e, packed_lobby_info),
            Some(lobby_info) => lobby_info,
            None => return Err(Error::LobbyNotFound),
        };
        // validation
        if lobby_info.phase != Phase::Uninitialized {
            return Err(Error::AlreadyInitialized)
        }
        if user.current_lobby != 0 {
            return Err(Error::GuestAlreadyInLobby);
        }
        if Self::is_address_empty(e, &lobby_info.host_address) {
            return Err(Error::LobbyHasNoHost)
        }
        if !Self::is_address_empty(e, &lobby_info.guest_address) {
            return Err(Error::GuestAlreadyInLobby)
        }
        if address == lobby_info.host_address {
            return Err(Error::JoinerIsHost)
        }
        // update
        user.current_lobby = req.lobby_id.clone();
        lobby_info.guest_address = address.clone();
        lobby_info.phase = Phase::Setup;
        // make new UserStates
        let host_user_state = UserState {
            setup: Vec::new(e),
            setup_hash: empty_hash.clone(),
            setup_hash_salt: 0,
        };
        let guest_user_state = UserState {
            setup: Vec::new(e),
            setup_hash: empty_hash.clone(),
            setup_hash_salt: 0,
        };
        // write
        persistent.set(&user_key, &user);
        temporary.set(&lobby_info_key, &lobby_info);
        let host_user_state_key = DataKey::UserState(req.lobby_id.clone(), lobby_info.host_address.clone());
        temporary.set(&host_user_state_key, &host_user_state);
        let guest_user_state_key = DataKey::UserState(req.lobby_id.clone(), lobby_info.guest_address.clone());
        temporary.set(&guest_user_state_key, &guest_user_state);

        persistent.extend_ttl(&user_key, 8640, 8640);
        temporary.extend_ttl(&lobby_info_key, 8640, 8640);
        Ok(())
    }

    pub fn commit_setup(e: &Env, address: Address, req: SetupCommitReq) -> Result<(), Error> {
        address.require_auth();
        let empty_hash = BytesN::from_array(e, &[0u8; 32]);
        let temporary = e.storage().temporary();
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        let lobby_info: LobbyInfo = match temporary.get(&lobby_info_key) {
            // Some(packed_lobby_info) => Self::unpack_lobby_info(e, packed_lobby_info),
            Some(lobby_info) => lobby_info,
            None => return Err(Error::LobbyNotFound),
        };
        let user_state_key = DataKey::UserState(req.lobby_id.clone(), address.clone());
        let mut user_state: UserState = match temporary.get(&user_state_key) {
            Some(thing) => thing,
            None => return Err(Error::SetupStateNotFound),
        };
        let other_address = Self::get_other_address(&address, &lobby_info);
        let other_user_state_key = DataKey::UserState(req.lobby_id.clone(), other_address.clone());
        let other_user_state: UserState = match temporary.get(&other_user_state_key) {
            Some(thing) => thing,
            None => return Err(Error::SetupStateNotFound),
        };
        // validation
        if lobby_info.phase != Phase::Setup {
            return Err(Error::WrongPhase)
        }
        if lobby_info.host_address != address || lobby_info.guest_address != address {
            return Err(Error::NotInLobby)
        }
        if user_state.setup_hash != empty_hash {
            return Err(Error::AlreadyCommittedSetup)
        }
        // update
        user_state.setup_hash = req.setup_hash;
        // write
        temporary.set(&user_state, &user_state_key);
        // mail if both players submitted
        if other_user_state.setup_hash != empty_hash {
            // mail both players
            let mail = Mail {
                ledger: e.ledger().sequence(),
                message: String::from_str(e, ""),
                mail_type: MailType::ProveSetupCommit,
            };
            let _ = Self::insert_mail(e, &address, &mail);
            let _ = Self::insert_mail(e, &other_address, &mail);
        }
        temporary.extend_ttl(&lobby_info_key, 8640, 8640);
        temporary.extend_ttl(&user_state_key, 8640, 8640);
        temporary.extend_ttl(&other_user_state_key, 8640, 8640);
        Ok(())
    }

    pub fn prove_setup(e: &Env, address: Address, req: ProveSetupReq) -> Result<(), Error> {
        address.require_auth();
        let empty_hash = BytesN::from_array(e, &[0u8; 32]);
        let temporary = e.storage().temporary();
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        let mut lobby_info: LobbyInfo = match temporary.get(&lobby_info_key) {
            // Some(packed_lobby_info) => Self::unpack_lobby_info(e, packed_lobby_info),
            Some(lobby_info) => lobby_info,
            None => return Err(Error::LobbyNotFound),
        };
        let user_state_key = DataKey::UserState(req.lobby_id.clone(), address.clone());
        let mut user_state: UserState = match temporary.get(&user_state_key) {
            Some(thing) => thing,
            None => return Err(Error::SetupStateNotFound),
        };
        let other_address = Self::get_other_address(&address, &lobby_info);
        let other_user_state_key = DataKey::UserState(req.lobby_id.clone(), other_address.clone());
        let other_user_state: UserState = match temporary.get(&other_user_state_key) {
            Some(thing) => thing,
            None => return Err(Error::UserNotFound),
        };
        // validation
        if lobby_info.phase != Phase::Setup {
            return Err(Error::WrongPhase)
        }
        if lobby_info.host_address != address || lobby_info.guest_address != address {
            return Err(Error::NotInLobby)
        }
        if user_state.setup_hash == empty_hash {
            return Err(Error::NoSetupCommitment)
        }
        let serialized = req.setup.clone().to_xdr(e);
        let setup_hash = e.crypto().sha256(&serialized).to_bytes();
        if setup_hash != user_state.setup_hash {
            return Err(Error::SetupHashFail)
        }
        user_state.setup = req.setup.clone();
        temporary.set(&user_state_key, &user_state);
        // if other user already proved their setup
        if other_user_state.setup.len() != 0 {
            lobby_info.phase = Phase::Movement;
            temporary.set(&lobby_info_key, &lobby_info);
        }


        temporary.extend_ttl(&lobby_info_key, 8640, 8640);
        temporary.extend_ttl(&user_state_key, 8640, 8640);
        temporary.extend_ttl(&other_user_state_key, 8640, 8640);
        Ok(())
    }

    pub(crate) fn insert_mail(e: &Env, address: &Address, mail: &Mail) -> Result<(), Error>  {
        let persistent = e.storage().persistent();
        let key = DataKey::Mailbox(address.clone());
        let mut mailbox : Mailbox = persistent.get(&key).unwrap_or_else(||
            Mailbox {
                mail: Vec::new(e),
            }
        );
        mailbox.mail.push_back(mail.clone());
        if mailbox.mail.len() > 5
        {
            mailbox.mail.pop_front_unchecked();
        }
        persistent.set(&key, &mailbox);
        Ok(())
    }

    pub(crate) fn empty_address(e: &Env) -> Address {
        Address::from_str(e, "GAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAWHF")
    }

    pub(crate) fn is_address_empty(e: &Env, address: &Address) -> bool {
        address.eq(&Self::empty_address(e))
    }

    pub(crate) fn pack_user(e: &Env, user: &User) -> PackedUser {
        let mut buf = [0u8; 8];
        buf[0..4].copy_from_slice(&user.current_lobby.to_be_bytes());
        buf[4..8].copy_from_slice(&user.games_completed.to_be_bytes());
        BytesN::from_array(e, &buf)
    }

    pub(crate) fn unpack_user(_e: &Env, packed_user: PackedUser, address: &Address) -> User {
        let arr: [u8; 8] = packed_user.to_array();
        User {
            current_lobby: u32::from_be_bytes(arr[0..4].try_into().unwrap()),
            games_completed: u32::from_be_bytes(arr[4..8].try_into().unwrap()),
            index: address.clone(),
        }
    }

    pub(crate) fn pack_lobby_info(e: &Env, lobby_info: &LobbyInfo) -> PackedLobbyInfo {
        let mut buf = [0u8; 93];
        buf[0..4].copy_from_slice(&lobby_info.index.to_be_bytes());
        lobby_info.guest_address.clone().to_xdr(e).copy_into_slice(&mut buf[4..48]);
        lobby_info.host_address.clone().to_xdr(e).copy_into_slice(&mut buf[48..92]);
        buf[92] = lobby_info.phase.clone() as u8;
        BytesN::from_array(e, &buf)
    }

    pub(crate) fn unpack_lobby_info(e: &Env, packed_lobby_info: PackedLobbyInfo) -> LobbyInfo {
        let arr: [u8; 93] = packed_lobby_info.to_array();
        LobbyInfo {
            index: u32::from_be_bytes(arr[0..4].try_into().unwrap()),
            guest_address: Address::from_xdr(e, &Bytes::from_slice(e, &arr[4..48])).unwrap(),
            host_address: Address::from_xdr(e, &Bytes::from_slice(e, &arr[48..92])).unwrap(),
            phase: match arr[92] {
                0 => Phase::Uninitialized,
                1 => Phase::Setup,
                2 => Phase::Movement,
                3 => Phase::Commitment,
                4 => Phase::Resolve,
                5 => Phase::Ending,
                6 => Phase::Aborted,
                _ => {panic!()}
            },
        }
    }

    pub(crate) fn get_or_make_user(e: &Env, address: &Address) -> User {
        let storage = e.storage().persistent();
        let key = DataKey::PackedUser(address.clone());
        // 1) Try to load an existing packed user
        if let Some(packed) = storage.get(&key) {
            // Found one → immediately unpack and return it
            // return Self::unpack_user(e, packed, &address);
            return packed
        }
        // 2) Not found → create a new User
        let new_user = User {
            current_lobby: 0,
            games_completed: 0,
            index: address.clone(),
        };
        new_user
    }

    pub(crate) fn is_user_host(e: &Env, address: &Address, lobby_info: &LobbyInfo) -> Result<bool, Error> {
        if Self::is_address_empty(e, address) {
            return Err(Error::IsUserHostError)
        }
        if Self::is_address_empty(e, &lobby_info.host_address) {
            return Err(Error::IsUserHostError)
        }
        Ok(address.clone() == lobby_info.host_address.clone())
    }

    pub(crate) fn get_other_address(address: &Address, lobby_info: &LobbyInfo) -> Address {
        if address.clone() == lobby_info.host_address.clone() {
            lobby_info.guest_address.clone()
        }
        else
        {
            lobby_info.host_address.clone()
        }
    }
}
// endregion

mod test;// run tests