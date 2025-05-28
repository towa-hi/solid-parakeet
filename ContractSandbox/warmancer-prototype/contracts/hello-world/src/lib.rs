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
pub type BoardHash = String;
pub type SetupHash = String;
pub type Rank = u32;
pub type Turn = u32;

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
    pub current_lobby: LobbyGuid,
    pub games_completed: u32,
    pub index: UserAddress,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MaxRank {
    pub rank: Rank,
    pub max: u32,
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
    pub index: LobbyGuid,
    pub guest_address: UserAddress,
    pub host_address: UserAddress,
    pub phase: Phase,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct UserState {
    pub setup: Bytes,
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
    pub parameters: LobbyParameters,
    pub lobby_id: LobbyGuid,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct JoinLobbyReq {
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
    pub setup_hash: SetupHash,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct ProveSetupReq {
    pub lobby_id: LobbyGuid,
    pub setup: Vec<PawnCommit>,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MoveCommitReq {
    pub lobby_id: LobbyGuid,
    pub move_pos_hash: PosHash, // hash of the Pos it's moving to
    pub pawn_id_hash: PawnGuidHash,
    pub turn: u32,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MoveSubmitReq {
    pub lobby: LobbyGuid,
    pub move_pos: Pos,
    pub pawn_id: PawnGuid,
    pub turn: u32,
    pub user_address: UserAddress,
}

// endregion
// region keys

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum DataKey {
    Admin, // Address
    User(UserAddress),
    LobbyInfo(LobbyGuid), // lobby specific data
    LobbyParameters(LobbyGuid),
    UserState(LobbyGuid, UserAddress),
    Mailbox(UserAddress),
}

// endregion
// region contract
#[contract]
pub struct Contract;
#[contractimpl]
impl Contract {
    pub fn make_lobby(e: &Env, address: Address, req: MakeLobbyReq) -> Result<(), Error> {
        address.require_auth();
        let user_address: UserAddress = address.to_string();
        let persistent = e.storage().persistent();
        let mut host_user = Self::get_or_make_user(e, &user_address);
        // validation
        if !host_user.current_lobby.is_empty() {
            return Err(Error::HostAlreadyInLobby)
        }
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        if persistent.has(&lobby_info_key) {
            return Err(Error::LobbyAlreadyExists)
        }
        // update
        host_user.current_lobby = req.lobby_id.clone();
        let lobby_info = LobbyInfo {
            index: req.lobby_id.clone(),
            host_address: user_address.clone(),
            guest_address: String::from_str(e, ""),
            phase: Phase::Uninitialized,
        };
        // write
        persistent.set(&lobby_info_key, &lobby_info);
        let lobby_parameters_key = DataKey::LobbyParameters(req.lobby_id.clone());
        persistent.set(&lobby_parameters_key, &req.parameters);
        let user_key = DataKey::User(user_address.clone());
        persistent.set(&user_key, &host_user);
        Ok(())
    }

    pub fn leave_lobby(e: &Env, address: Address) -> Result<(), Error> {
        address.require_auth();
        let user_address: UserAddress = address.to_string();
        let persistent = e.storage().persistent();
        let user_key = DataKey::User(user_address.clone());
        let mut user: User = match persistent.get(&user_key) {
            Some(thing) => thing,
            None => return Err(Error::UserNotFound),
        };
        let lobby_info_key = DataKey::LobbyInfo(user.current_lobby.clone());
        let mut lobby_info = match persistent.get(&lobby_info_key) {
            Some(thing) => thing,
            None => return Err(Error::LobbyNotFound),
        };
        // validation unneeded past this point
        // update
        if user_address == lobby_info.host_address {
            lobby_info.host_address = String::from_str(e, "")
        }
        else if user_address == lobby_info.guest_address {
            lobby_info.guest_address = String::from_str(e, "")
        }
        else {
            return Err(Error::LobbyNotFound)
        }
        //TODO: handle detecting if the game is in progress to award victory/defeat
        lobby_info.phase = Phase::Aborted;
        user.current_lobby = String::from_str(e, "");
        // write
        persistent.set(&user_key, &user);
        persistent.set(&lobby_info_key, &lobby_info);
        Ok(())
    }

    pub fn join_lobby(e: &Env, address: Address, req: JoinLobbyReq) -> Result<(), Error> {
        address.require_auth();
        let user_address: UserAddress = address.to_string();
        let persistent = e.storage().persistent();
        let user_key = DataKey::User(user_address.clone());
        let mut user: User = match persistent.get(&user_key) {
            Some(thing) => thing,
            None => return Err(Error::UserNotFound),
        };
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        let mut lobby_info = match persistent.get(&lobby_info_key) {
            Some(thing) => thing,
            None => return Err(Error::LobbyNotFound),
        };
        // validation
        if lobby_info.phase != Phase::Uninitialized {
            return Err(Error::AlreadyInitialized)
        }
        if !user.current_lobby.is_empty() {
            return Err(Error::GuestAlreadyInLobby);
        }
        if lobby_info.host_address.is_empty() {
            return Err(Error::LobbyHasNoHost)
        }
        if !lobby_info.guest_address.is_empty() {
            return Err(Error::GuestAlreadyInLobby)
        }
        if user_address == lobby_info.host_address {
            return Err(Error::JoinerIsHost)
        }
        // update
        user.current_lobby = req.lobby_id.clone();
        lobby_info.guest_address = user_address.clone();
        lobby_info.phase = Phase::Setup;
        // make new UserStates
        let host_user_state = UserState {
            setup: Bytes::new(e),
            setup_hash: String::from_str(e, ""),
            setup_hash_salt: 0,
        };
        let guest_user_state = UserState {
            setup: Bytes::new(e),
            setup_hash: String::from_str(e, ""),
            setup_hash_salt: 0,
        };
        // write
        persistent.set(&user_key, &user);
        persistent.set(&lobby_info_key, &lobby_info);
        let host_user_state_key = DataKey::UserState(req.lobby_id.clone(), lobby_info.host_address.clone());
        persistent.set(&host_user_state_key, &host_user_state);
        let guest_user_state_key = DataKey::UserState(req.lobby_id.clone(), lobby_info.guest_address.clone());
        persistent.set(&guest_user_state_key, &guest_user_state);
        Ok(())
    }

    pub fn commit_setup(e: &Env, address: Address, req: SetupCommitReq) -> Result<(), Error> {
        address.require_auth();
        let user_address: UserAddress = address.to_string();
        let persistent = e.storage().persistent();
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        let lobby_info = match persistent.get(&lobby_info_key) {
            Some(thing) => thing,
            None => return Err(Error::LobbyNotFound),
        };
        let user_state_key = DataKey::UserState(req.lobby_id.clone(), user_address.clone());
        let mut user_state: UserState = match persistent.get(&user_state_key) {
            Some(thing) => thing,
            None => return Err(Error::SetupStateNotFound),
        };
        let other_address = Self::get_other_address(&user_address, &lobby_info);
        let other_user_state_key = DataKey::UserState(req.lobby_id.clone(), other_address.clone());
        let other_user_state = match persistent.get(&other_user_state_key) {
            Some(thing) => thing,
            None => return Err(Error::UserNotFound),
        };
        // validation
        if lobby_info.phase != Phase::Setup {
            return Err(Error::WrongPhase)
        }
        if !user_state.setup_hash.is_empty() {
            return Err(Error::AlreadyCommittedSetup)
        }
        // update
        user_state.setup_hash = req.setup_hash;
        // write
        persistent.set(&user_state, &user_state_key);
        // mail if both players submitted
        if !other_user_state.setup_hash.is_empty() {
            // mail both players
            let mail = Mail {
                ledger: e.ledger().sequence(),
                message: String::from_str(e, ""),
                mail_type: MailType::ProveSetupCommit,
            };
            Self::insert_mail(e, &user_address, &mail);
            Self::insert_mail(e, &other_address, &mail);
        }
        Ok(())
    }

    pub fn prove_setup(e: &Env, address: Address, req: ProveSetupReq) -> Result<(), Error> {
        address.require_auth();
        let user_address: UserAddress = address.to_string();
        let persistent = e.storage().persistent();
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        let lobby_info = match persistent.get(&lobby_info_key) {
            Some(thing) => thing,
            None => return Err(Error::LobbyNotFound),
        };
        let user_state_key = DataKey::UserState(req.lobby_id.clone(), user_address.clone());
        let user_state: UserState = match persistent.get(&user_state_key) {
            Some(thing) => thing,
            None => return Err(Error::SetupStateNotFound),
        };
        let other_address = Self::get_other_address(&user_address, &lobby_info);
        let other_user_state_key = DataKey::UserState(req.lobby_id.clone(), other_address.clone());
        let other_user_state = match persistent.get(&other_user_state_key) {
            Some(thing) => thing,
            None => return Err(Error::UserNotFound),
        };

        let serialized = req.setup.to_xdr(e);
        let hash = e.crypto().sha256(&serialized);
        // validation
        if lobby_info.phase != Phase::Setup {
            return Err(Error::WrongPhase)
        }
        Ok(())
    }

    pub(crate) fn insert_mail(e: &Env, user_address: &UserAddress, mail: &Mail) -> Result<(), Error>  {
        let persistent = e.storage().persistent();
        let key = DataKey::Mailbox(user_address.clone());
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

    pub(crate) fn get_or_make_user(e: &Env, user_address: &UserAddress) -> User {
        let storage = e.storage().persistent();
        let key = DataKey::User(user_address.clone());
        storage.get(&key).unwrap_or_else(|| {
            let new_user = User {
                current_lobby: String::from_str(e, ""),
                games_completed: 0,
                index: user_address.clone(),
            };
            storage.set(&key, &new_user);
            new_user
        })
    }
    pub(crate) fn get_index(user_address: &UserAddress, lobby_info: &LobbyInfo) -> Result<u32, Error> {
        if user_address.clone() == lobby_info.host_address.clone() {
            Ok(0)
        }
        else if user_address.clone() == lobby_info.guest_address.clone() {
            Ok(1)
        } else
        {
            return Err(Error::InvalidArgs)
        }
    }

    pub(crate) fn is_user_host(user_address: &UserAddress, lobby_info: &LobbyInfo) -> Result<bool, Error> {
        if user_address.is_empty() {
            return Err(Error::IsUserHostError)
        }
        if lobby_info.host_address.is_empty() {
            return Err(Error::IsUserHostError)
        }
        Ok(user_address.clone() == lobby_info.host_address.clone())
    }

    pub(crate) fn get_other_address(user_address: &UserAddress, lobby_info: &LobbyInfo) -> UserAddress {
        if user_address.clone() == lobby_info.host_address.clone() {
            lobby_info.guest_address.clone()
        }
        else
        {
            lobby_info.host_address.clone()
        }
    }

    /*
        pub fn upgrade(env: Env, new_wasm_hash: BytesN<32>) {
            let admin: Address = env.storage().instance().get(&DataKey::Admin).unwrap();
            admin.require_auth();
            env.deployer().update_current_contract_wasm(new_wasm_hash);
        }

        pub fn send_mail(env: Env, address: Address, req: SendMailReq) -> Result<bool, Error> {
            address.require_auth();
            if address.to_string() != req.mail.sender {
                return Err(Error::InvalidArgs)
            }
            let persistent = &env.storage().persistent();
            let key = DataKey::Mail(req.lobby.clone());
            let mut mailbox : Mailbox = persistent.get(&key).unwrap_or_else(||
                Mailbox {
                    lobby: req.lobby.clone(),
                    mail: Vec::new(&env),
                }
            );
            mailbox.mail.push_back(req.mail);
            if mailbox.mail.len() > 5
            {
                mailbox.mail.pop_front_unchecked();
            }
            persistent.set(&key, &mailbox);
            Ok(true)
        }

        pub fn make_lobby(env: Env, address: Address, req: MakeLobbyReq) -> Result<LobbyGuid, Error> {
            address.require_auth();
            if address.to_string() != req.host_address {
                return Err(Error::InvalidArgs)
            }
            let persistent = &env.storage().persistent();
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
                    committed: false,
                    lobby_state: UserLobbyState::NotAccepted,
                    setup_commitments: Vec::new(&env),
                    team: Team::Blue,
                    user_address: String::from_str(&env, ""),
                },
                host_address: host_user.index.clone(),
                host_state: UserState {
                    committed: false,
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
            let other_user_address = if user_is_host {&lobby.guest_address} else {&lobby.host_address};
            if user_is_host {
                lobby.host_state.lobby_state = UserLobbyState::Left;
            }
            else {
                lobby.guest_state.lobby_state = UserLobbyState::Left;
            }
            lobby.game_end_state = EndState::Aborted;
            persistent.set(&lobby_key, &lobby);
            if !other_user_address.is_empty() {
                env.events().publish((EVENT_USER_LEFT, lobby.index, user.index.clone(), other_user_address.clone()), user.index);
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
            lobby.guest_state.user_address = req.guest_address.clone();
            lobby.guest_state.lobby_state = UserLobbyState::InGame;
            lobby.host_state.lobby_state = UserLobbyState::InGame;
            lobby.phase = Phase::Setup;
            // generate pawns and commitments
            let mut pawn_id_counter: u32 = 0;
            let purgatory_pos = Pos { x: -666, y: -666 };
            for max_pawn in lobby.parameters.max_pawns.iter() {
                for _ in 0..max_pawn.max {
                    let pawn = Pawn {
                        is_alive: false,
                        is_moved: false,
                        is_revealed: false,
                        pawn_def_hash: String::from_str(&env, ""),
                        pawn_id: Self::generate_uuid(&env, pawn_id_counter),
                        pos: purgatory_pos.clone(),
                        team: lobby.host_state.team.clone(),
                    };
                    let commitment = PawnCommitment {
                        pawn_def_hash: pawn.pawn_def_hash.clone(),
                        pawn_id: pawn.pawn_id.clone(),
                        starting_pos: pawn.pos.clone(),
                    };
                    lobby.pawns.push_back(pawn);
                    lobby.host_state.setup_commitments.push_back(commitment);
                    pawn_id_counter += 1;
                }
            }
            for max_pawn in lobby.parameters.max_pawns.iter() {
                for _ in 0..max_pawn.max {
                    let pawn = Pawn {
                        is_alive: false,
                        is_moved: false,
                        is_revealed: false,
                        pawn_def_hash: String::from_str(&env, ""),
                        pawn_id: Self::generate_uuid(&env, pawn_id_counter),
                        pos: purgatory_pos.clone(),
                        team: lobby.guest_state.team.clone(),
                    };
                    let commitment = PawnCommitment {
                        pawn_def_hash: pawn.pawn_def_hash.clone(),
                        pawn_id: pawn.pawn_id.clone(),
                        starting_pos: pawn.pos.clone(),
                    };
                    lobby.pawns.push_back(pawn);
                    lobby.guest_state.setup_commitments.push_back(commitment);
                    pawn_id_counter += 1;
                }
            }
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
            let (user_state, other_user_state) = if user_address == lobby.host_address {
                (&mut lobby.host_state, &mut lobby.guest_state)
            } else if user_address == lobby.guest_address {
                (&mut lobby.guest_state, &mut lobby.host_state)
            } else {
                return Err(Error::InvalidArgs);
            };
            // TODO: we got to check to make sure it matches the lobby
            if user_state.setup_commitments.len() != req.setup_commitments.len() {
                return Err(Error::InvalidArgs);
            }
            user_state.setup_commitments = req.setup_commitments.clone();
            user_state.committed = true;
            // go to movement phase if both players have submitted
            if other_user_state.committed
            {
                // apply commits for both users
                let mut commitment_map: Map<PawnGuid, PawnCommitment> = Map::new(&env);
                for commit in lobby.host_state.setup_commitments.iter() {
                    commitment_map.set(commit.pawn_id.clone(), commit.clone());
                }
                for commit in lobby.guest_state.setup_commitments.iter() {
                    commitment_map.set(commit.pawn_id.clone(), commit.clone());
                }
                for i in 0..lobby.pawns.len() {
                    let mut pawn = lobby.pawns.get_unchecked(i);
                    if let Some(commit) = commitment_map.get(pawn.pawn_id.clone()) {
                        pawn.pos = commit.starting_pos;
                        pawn.pawn_def_hash = commit.pawn_def_hash;
                        pawn.is_alive = true;
                        lobby.pawns.set(i, pawn);
                    }
                }
                let first_turn = Turn {
                    guest_events: Vec::new(&env),
                    guest_events_hash: String::from_str(&env, ""),
                    guest_turn: TurnMove {
                        initialized: false,
                        pawn_id: String::from_str(&env, ""),
                        pos: Pos { x: -666, y: -666, },
                        turn: 0,
                        user_address: lobby.guest_address.clone(),
                    },
                    host_events: Vec::new(&env),
                    host_events_hash:  String::from_str(&env, ""),
                    host_turn: TurnMove {
                        initialized: false,
                        pawn_id: String::from_str(&env, ""),
                        pos: Pos { x: -666, y: -666, },
                        turn: 0,
                        user_address: lobby.host_address.clone(),
                    },
                    turn: 0,
                };
                lobby.turns.push_back(first_turn);
                lobby.phase = Phase::Movement;
            }
            persistent.set(&lobby_key, &lobby);
            Ok(())
        }

        pub fn submit_move(env: Env, address: Address, req: MoveSubmitReq) -> Result<(), Error> {
            address.require_auth();
            if address.to_string() != req.user_address {
                return Err(Error::InvalidArgs)
            }
            let persistent = env.storage().persistent();
            let lobby_key = DataKey::Lobby(req.lobby.clone());
            let mut lobby: Lobby = match persistent.get(&lobby_key) {
                Some(v) => v,
                None => return Err(Error::LobbyNotFound),
            };
            if lobby.phase != Phase::Movement {
                return Err(Error::WrongPhase)
            }
            let mut turn = lobby.turns.last_unchecked();
            if req.turn != turn.turn
            {
                return Err(Error::InvalidArgs)
            }
            if req.user_address == turn.host_turn.user_address {
                let mut host_turn = turn.host_turn.clone();
                if host_turn.initialized {
                    return Err(Error::TurnAlreadyInitialized)
                }
                host_turn.initialized = true;
                host_turn.pos = req.move_pos.clone();
                host_turn.pawn_id = req.pawn_id.clone();
                turn.host_turn = host_turn;
                lobby.turns.set(lobby.turns.len() - 1, turn.clone());
            } else if req.user_address == turn.guest_turn.user_address {
                let mut guest_turn = turn.guest_turn.clone();
                if guest_turn.initialized {
                    return Err(Error::TurnAlreadyInitialized)
                }
                guest_turn.initialized = true;
                guest_turn.pos = req.move_pos.clone();
                guest_turn.pawn_id = req.pawn_id.clone();
                turn.guest_turn = guest_turn;
                lobby.turns.set(lobby.turns.len() - 1, turn.clone());
            }
            else
            {
                return Err(Error::InvalidArgs)
            }

            persistent.set(&lobby_key, &lobby);
            Ok(())
        }

        pub fn resolve_move(env: Env, address: Address, req: MoveResolveReq) -> Result<(), Error> {
            address.require_auth();
            if address.to_string() != req.user_address {
                return Err(Error::InvalidArgs)
            }
            let persistent = env.storage().persistent();
            let lobby_key = DataKey::Lobby(req.lobby.clone());
            let mut lobby: Lobby = match persistent.get(&lobby_key) {
                Some(v) => v,
                None => return Err(Error::LobbyNotFound),
            };
            if lobby.phase != Phase::Movement {
                return Err(Error::WrongPhase)
            }
            let mut turn = lobby.turns.last_unchecked();
            if req.turn != turn.turn
            {
                return Err(Error::InvalidArgs)
            }
            if req.user_address == turn.host_turn.user_address {
                if turn.host_turn.initialized {
                    turn.host_events = req.events.clone();
                    turn.host_events_hash = req.events_hash.clone();
                }
                else
                {
                    return Err(Error::InvalidArgs);
                }
            } else if req.user_address == turn.guest_turn.user_address {
                if turn.guest_turn.initialized {
                    turn.guest_events = req.events.clone();
                    turn.guest_events_hash = req.events_hash.clone();
                }
                else
                {
                    return Err(Error::InvalidArgs);
                }
            }
            else {
                return Err(Error::InvalidArgs);
            }
            lobby.turns.set(lobby.turns.len() - 1, turn.clone());
            // if both players resolved
            if !turn.host_events_hash.is_empty() && !turn.guest_events_hash.is_empty() {
                if turn.host_events_hash == turn.guest_events_hash
                {
                    let purgatory_pos = Pos { x: -666, y: -666 };
                    // update the lobby state
                    // for now we just trust the guest
                    for resolve_event in req.events.clone() {
                        match resolve_event.event_type {
                            ResolveEventType::Move => {
                                for i in 0..lobby.pawns.len() {
                                    let mut updated_pawn = lobby.pawns.get_unchecked(i);
                                    if updated_pawn.pawn_id == resolve_event.pawn_id {
                                        updated_pawn.pos = resolve_event.target_pos;
                                        updated_pawn.is_moved = true;
                                        lobby.pawns.set(i, updated_pawn);
                                        break;
                                    }
                                }
                            }
                            ResolveEventType::Conflict => {
                                for i in 0..lobby.pawns.len() {
                                    let mut updated_pawn = lobby.pawns.get_unchecked(i);
                                    let mut processed_count = 0;
                                    if updated_pawn.pawn_id == resolve_event.pawn_id || updated_pawn.pawn_id == resolve_event.defender_pawn_id {
                                        updated_pawn.is_revealed = true;
                                        lobby.pawns.set(i, updated_pawn);
                                        processed_count += 1;
                                    }
                                    if processed_count >= 2 {
                                        break;
                                    }
                                }
                            }
                            ResolveEventType::SwapConflict => {
                                for i in 0..lobby.pawns.len() {
                                    let mut updated_pawn = lobby.pawns.get_unchecked(i);
                                    let mut processed_count = 0;
                                    if updated_pawn.pawn_id == resolve_event.pawn_id || updated_pawn.pawn_id == resolve_event.defender_pawn_id {
                                        updated_pawn.is_revealed = true;
                                        lobby.pawns.set(i, updated_pawn);
                                        processed_count += 1;
                                    }
                                    if processed_count >= 2 {
                                        break;
                                    }
                                }
                            }
                            ResolveEventType::Death => {
                                for i in 0..lobby.pawns.len() {
                                    let mut updated_pawn = lobby.pawns.get_unchecked(i);
                                    if updated_pawn.pawn_id == resolve_event.pawn_id
                                    {
                                        updated_pawn.is_alive = false;
                                        updated_pawn.pos = purgatory_pos.clone();
                                        lobby.pawns.set(i, updated_pawn);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    let mut red_throne_alive = false;
                    let mut blue_throne_alive = false;
                    // TODO: figure out a good way to check if all alive pawns are movable for a team
                    for i in 0..lobby.pawns.len() {
                        let pawn = lobby.pawns.get_unchecked(i);
                        if pawn.pawn_def_hash == String::from_str(&env, "Throne") && pawn.is_alive {
                            if pawn.team == Team::Red {
                                red_throne_alive = true;
                            }
                            if pawn.team == Team::Blue {
                                blue_throne_alive = true;
                            }
                        }
                    }
                    if !red_throne_alive {
                        lobby.game_end_state = EndState::Blue;
                    }
                    if !blue_throne_alive {
                        lobby.game_end_state = EndState::Red;
                    }
                    if !red_throne_alive && !blue_throne_alive {
                        lobby.game_end_state = EndState::Tie;
                    }
                    let next_turn_index = lobby.turns.len() as i32;
                    let next_turn = Turn {
                        guest_events: Vec::new(&env),
                        guest_events_hash: String::from_str(&env, ""),
                        guest_turn: TurnMove {
                            initialized: false,
                            pawn_id: String::from_str(&env, ""),
                            pos: Pos { x: -666, y: -666, },
                            turn: next_turn_index,
                            user_address: lobby.guest_address.clone(),
                        },
                        host_events: Vec::new(&env),
                        host_events_hash: String::from_str(&env, ""),
                        host_turn: TurnMove {
                            initialized: false,
                            pawn_id: String::from_str(&env, ""),
                            pos: Pos { x: -666, y: -666, },
                            turn: next_turn_index,
                            user_address: lobby.host_address.clone(),
                        },
                        turn: next_turn_index,
                    };
                    lobby.turns.push_back(next_turn);
                }
                else
                {
                    return Err(Error::TurnHashConflict);
                }
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

        pub(crate) fn create_pawn_def(e: &Env, rank: Rank) -> PawnDef {
            match rank {
                0 => PawnDef {
                    id: 0,
                    name: String::from_str(e, "Throne"),
                    rank: 0,
                    power: 0,
                    movement_range: 0,
                },
                1 => PawnDef {
                    id: 1,
                    name: String::from_str(e, "Assassin"),
                    rank: 1,
                    power: 1,
                    movement_range: 1,
                },
                2 => PawnDef {
                    id: 2,
                    name: String::from_str(e, "Scout"),
                    rank: 2,
                    power: 2,
                    movement_range: 11, // Scout has special movement
                },
                3 => PawnDef {
                    id: 3,
                    name: String::from_str(e, "Seer"),
                    rank: 3,
                    power: 3,
                    movement_range: 1,
                },
                4 => PawnDef {
                    id: 4,
                    name: String::from_str(e, "Grunt"),
                    rank: 4,
                    power: 4,
                    movement_range: 1,
                },
                5 => PawnDef {
                    id: 5,
                    name: String::from_str(e, "Knight"),
                    rank: 5,
                    power: 5,
                    movement_range: 1,
                },
                6 => PawnDef {
                    id: 6,
                    name: String::from_str(e, "Wraith"),
                    rank: 6,
                    power: 6,
                    movement_range: 1,
                },
                7 => PawnDef {
                    id: 7,
                    name: String::from_str(e, "Reaver"),
                    rank: 7,
                    power: 7,
                    movement_range: 1,
                },
                8 => PawnDef {
                    id: 8,
                    name: String::from_str(e, "Herald"),
                    rank: 8,
                    power: 8,
                    movement_range: 1,
                },
                9 => PawnDef {
                    id: 9,
                    name: String::from_str(e, "Champion"),
                    rank: 9,
                    power: 9,
                    movement_range: 1,
                },
                10 => PawnDef {
                    id: 10,
                    name: String::from_str(e, "Warlord"),
                    rank: 10,
                    power: 10,
                    movement_range: 1,
                },
                11 => PawnDef {
                    id: 11,
                    name: String::from_str(e, "Trap"),
                    rank: 11,
                    power: 11,
                    movement_range: 1,
                },
                99 => PawnDef {
                    id: 99,
                    name: String::from_str(e, "Unknown"),
                    rank: 99,
                    power: 0,
                    movement_range: 0,
                },
                _ => PawnDef {
                    id: 99,
                    name: String::from_str(e, "Unknown"),
                    rank: 99,
                    power: 0,
                    movement_range: 0,
                },
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
         */
}
// endregion

mod test;// run tests