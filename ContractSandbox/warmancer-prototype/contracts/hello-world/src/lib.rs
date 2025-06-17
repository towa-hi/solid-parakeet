#![no_std]
use soroban_sdk::*;
use soroban_sdk::xdr::*;

// region global state defs

pub type LobbyId = u32;
pub type PawnId = u32;
pub type HiddenRankHash = BytesN<32>;
pub type HiddenMoveHash = BytesN<32>;
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
    GetPlayerIndexError = 19,
    AlreadyCommittedSetup = 20,
    NotInLobby = 21,
    NoSetupCommitment = 22,
    NoOpponentSetupCommitment = 23,
    SetupHashFail = 24,
    GameStateNotFound = 25,
    GameNotInProgress = 26,
    AlreadySubmittedSetup = 27,
    InvalidContractState = 28,
    WrongInstruction = 29,
    HiddenMoveHashFail = 30,
    PawnNotTeam = 31,
    PawnNotFound = 32,
    RedMoveInvalid = 33,
    BlueMoveInvalid = 34,
    BothMovesInvalid = 35,
    HiddenRankHashFail = 36,
    PawnCommitNotFound = 37,
    WrongPawnId = 38,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum Instruction {
    None = 0,
    Reserved = 1,
    RequestingSetupCommit = 2,
    WaitingOppSetupCommit = 3,
    RequestingSetupProof = 4,
    WaitingOppSetupProof = 5,
    RequestingMoveCommit = 6,
    WaitingOppMoveCommit = 7,
    RequestingMovePosProof = 8,
    WaitingOppMovePosProof = 9,
    RequestingMoveRankProof = 10,
    WaitingOppMoveRankProof = 11,
    EndGame = 12,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum Phase {
    Setup = 0,
    Movement = 1,
    Completed = 2,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum LobbyStatus {
    WaitingForPlayers = 0,
    GameInProgress = 1,
    HostWin = 2,
    GuestWin = 3,
    Draw = 4,
    Aborted = 5,
    None = 6,
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
pub struct User {
    pub current_lobby: LobbyId,
    pub games_completed: u32,
    pub index: Address,
    pub instruction: u32,
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
pub struct HiddenMove {
    pub pawn_id: PawnId,
    pub pos: Pos,
    pub salt: u64,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct HiddenRank {
    pub rank: Rank,
    pub salt: u64,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct HiddenRankProof {
    pub hidden_rank: HiddenRank,
    pub hidden_rank_hash: HiddenRankHash,
    pub pawn_id: PawnId,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PawnCommit {
    pub hidden_rank_hash: HiddenRankHash,
    pub pawn_id: PawnId,
}

// endregion
// region level 2 structs
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct LobbyParameters {
    pub board_hash: BoardHash,
    pub dev_mode: bool,
    pub host_team: u32,
    pub max_ranks: Vec<MaxRank>,
    pub must_fill_all_tiles: bool,
    pub security_mode: bool,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct LobbyInfo {
    pub index: LobbyId,
    pub guest_address: Address,
    pub host_address: Address,
    pub status: LobbyStatus,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PawnState {
    pub alive: bool,
    pub moved: bool,
    pub moved_scout: bool,
    pub pawn_id: PawnId,
    pub pos: Pos,
    pub revealed_rank: Rank,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct TileState {
    pub pos: Pos,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct BoardState {
    pub initialized: bool,
    pub pawns: Vec<PawnState>,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct GameState {
    pub board_state: BoardState,
    pub phase: Phase,
    pub user_states: Vec<UserState>,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct UserState {
    pub instruction: Instruction, // tells the client what function to do to continue the game
    pub latest_move: HiddenMove, // set in prove_move() cleared and sent to old_moves between turns
    pub latest_move_hash: HiddenMoveHash,
    pub old_moves: Vec<HiddenMove>, // old raw HiddenMoves not including latest_move
    pub requested_pawn_ranks: Vec<PawnId>, // list of ids to submit raw HiddenRank when instruction is RequestingMoveRankProof
    pub setup: Vec<PawnCommit>,
    pub setup_hash: SetupHash,
    pub setup_hash_salt: u32,
}

// endregion
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
pub struct CommitSetupReq {
    pub lobby_id: LobbyId,
    pub setup_hash: SetupHash,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct ProveSetupReq {
    pub lobby_id: LobbyId,
    pub salt: u32,
    pub setup: Vec<PawnCommit>,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct CommitMoveReq {
    pub lobby_id: LobbyId,
    pub move_hash: HiddenMoveHash,
    // TODO: add a state hash to check if the last movement was valid
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct ProveMoveReq {
    pub hidden_move: HiddenMove,
    pub lobby_id: LobbyId,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct ProveRankReq {
    pub hidden_ranks: Vec<HiddenRank>,
    pub lobby_id: LobbyId,
}

// endregion
// region keys

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum DataKey {
    Admin, // Address
    PackedUser(Address),
    LobbyInfo(LobbyId), // lobby specific data
    LobbyParameters(LobbyId), // immutable lobby data
    GameState(LobbyId),
    Mailbox(Address),
}

// endregion
// region contract
#[contract]
pub struct Contract;
#[contractimpl]
impl Contract {

    pub fn test_hash(e: &Env, address : Address, req: ProveSetupReq) -> Result<BytesN<32>, Error> {
        let serialized = req.clone().to_xdr(e);
        let setup_hash = e.crypto().sha256(&serialized);
        Ok(setup_hash.to_bytes())
    }

    pub fn make_lobby(e: &Env, address: Address, req: MakeLobbyReq) -> Result<(), Error> {
        address.require_auth();
        let persistent = e.storage().persistent();
        let temporary = e.storage().temporary();
        let mut host_user = Self::get_or_make_user(e, &address);
        let lobby_parameters_key = DataKey::LobbyParameters(req.lobby_id.clone());
        let user_key = DataKey::PackedUser(address.clone());
        // validation
        if host_user.current_lobby != 0 {
            return Err(Error::HostAlreadyInLobby)
        }
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        if temporary.has(&lobby_info_key) {
            return Err(Error::LobbyAlreadyExists)
        }
        // make
        let lobby_info = LobbyInfo {
            index: req.lobby_id.clone(),
            guest_address: Self::empty_address(e),
            host_address: address,
            status: LobbyStatus::WaitingForPlayers,
        };
        // update
        host_user.current_lobby = req.lobby_id.clone();
        // save
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&lobby_parameters_key, &req.parameters);
        persistent.set(&user_key, &host_user);

        Ok(())
    }

    pub fn leave_lobby(e: &Env, address: Address) -> Result<(), Error> {
        address.require_auth();
        let persistent = e.storage().persistent();
        let temporary = e.storage().temporary();
        let packed_user_key = DataKey::PackedUser(address.clone());
        let mut user: User = match persistent.get(&packed_user_key) {
            Some(user) => user,
            None => return Err(Error::UserNotFound), // TODO: make a version of this function that sends a lobbyId
        };
        let left_lobby_id = user.current_lobby.clone();
        // save user early and early return if lobby is gone
        user.current_lobby = 0;
        persistent.set(&packed_user_key, &user);
        let lobby_info_key = DataKey::LobbyInfo(left_lobby_id);
        let mut lobby_info: LobbyInfo = match temporary.get(&lobby_info_key) {
            // Some(packed_lobby_info) => Self::unpack_lobby_info(e, packed_lobby_info),
            Some(lobby_info) => lobby_info,
            None => return Ok(()),
        };
        // update
        if address == lobby_info.host_address {
            lobby_info.host_address = Self::empty_address(e);
        }
        else if address == lobby_info.guest_address {
            lobby_info.guest_address = Self::empty_address(e);
        }
        lobby_info.status = LobbyStatus::Aborted; // TODO: handle detecting if the game is in progress to award victory/defeat
        // save
        temporary.set(&lobby_info_key, &lobby_info);

        Ok(())
    }

    pub fn join_lobby(e: &Env, address: Address, req: JoinLobbyReq) -> Result<(), Error> {
        address.require_auth();
        let empty_hash = BytesN::from_array(e, &[0u8; 32]);
        let empty_move = HiddenMove {
            pawn_id: 0,
            pos: Pos {
                x: 0,
                y: 0,
            },
            salt: 0,
        };
        let persistent = e.storage().persistent();
        let temporary = e.storage().temporary();
        let user_key = DataKey::PackedUser(address.clone());
        let mut user = Self::get_or_make_user(e, &address);
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        let game_state_key = DataKey::GameState(req.lobby_id.clone());
        let mut lobby_info: LobbyInfo = match temporary.get(&lobby_info_key) {
            // Some(packed_lobby_info) => Self::unpack_lobby_info(e, packed_lobby_info),
            Some(lobby_info) => lobby_info,
            None => return Err(Error::LobbyNotFound),
        };
        // validation
        if lobby_info.status != LobbyStatus::WaitingForPlayers {
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
        let empty_user_state = UserState {
            instruction: Instruction::RequestingSetupCommit,
            latest_move: empty_move.clone(),
            latest_move_hash: empty_hash.clone(),
            old_moves: Vec::new(e),
            requested_pawn_ranks: Vec::new(e),
            setup: Vec::new(e),
            setup_hash: empty_hash.clone(),
            setup_hash_salt: 0,
        };
        // make
        let game_state = GameState {
            board_state: BoardState {
                initialized: false,
                pawns: Vec::new(e),
            },
            phase: Phase::Setup,
            user_states: Vec::from_array(e, [
                empty_user_state.clone(),
                empty_user_state.clone(),
            ]),
        };
        // update
        user.current_lobby = req.lobby_id.clone();
        lobby_info.guest_address = address.clone();
        lobby_info.status = LobbyStatus::GameInProgress;
        // save
        persistent.set(&user_key, &user);
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&game_state_key, &game_state);

        Ok(())
    }

    pub fn commit_setup(e: &Env, address: Address, req: CommitSetupReq) -> Result<(), Error> {
        address.require_auth();
        let temporary = e.storage().temporary();
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        let lobby_info: LobbyInfo = match temporary.get(&lobby_info_key) {
            Some(lobby_info) => lobby_info,
            None => return Err(Error::LobbyNotFound),
        };
        let game_state_key = DataKey::GameState(req.lobby_id.clone());
        let mut game_state: GameState = match temporary.get(&game_state_key) {
            Some(game_state) => game_state,
            None => return Err(Error::GameStateNotFound),
        };
        let player_index = Self::get_player_index(e, &address, &lobby_info);
        let opponent_index = Self::get_opponent_index(e, &address, &lobby_info);
        let mut user_state = game_state.user_states.get_unchecked(player_index);
        let mut opponent_state = game_state.user_states.get_unchecked(opponent_index);
        // validation
        if lobby_info.status != LobbyStatus::GameInProgress {
            return Err(Error::GameNotInProgress)
        }
        if lobby_info.host_address != address && lobby_info.guest_address != address {
            return Err(Error::NotInLobby)
        }
        if game_state.phase != Phase::Setup {
            return Err(Error::WrongPhase)
        }
        if user_state.instruction != Instruction::RequestingSetupCommit {
            return Err(Error::WrongInstruction)
        }
        // update
        user_state.setup_hash = req.setup_hash;
        user_state.instruction = Instruction::WaitingOppSetupCommit;
        if opponent_state.instruction == Instruction::WaitingOppSetupCommit {
            user_state.instruction = Instruction::RequestingSetupProof;
            opponent_state.instruction = Instruction::RequestingSetupProof;
        }
        game_state.user_states.set(player_index, user_state);
        game_state.user_states.set(opponent_index, opponent_state);
        // save
        temporary.set(&game_state_key, &game_state);

        Ok(())
    }

    pub fn prove_setup(e: &Env, address: Address, req: ProveSetupReq) -> Result<(), Error> {
        address.require_auth();
        let temporary = e.storage().temporary();
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        let lobby_info: LobbyInfo = match temporary.get(&lobby_info_key) {
            // Some(packed_lobby_info) => Self::unpack_lobby_info(e, packed_lobby_info),
            Some(lobby_info) => lobby_info,
            None => return Err(Error::LobbyNotFound),
        };
        let game_state_key = DataKey::GameState(req.lobby_id.clone());
        let mut game_state: GameState = match temporary.get(&game_state_key) {
            Some(game_state) => game_state,
            None => return Err(Error::GameStateNotFound),
        };
        let player_index = Self::get_player_index(e, &address, &lobby_info);
        let mut user_state = game_state.user_states.get_unchecked(player_index);
        let opponent_index = Self::get_opponent_index(e, &address, &lobby_info);
        let mut opponent_state = game_state.user_states.get_unchecked(opponent_index);
        // validation
        if lobby_info.status != LobbyStatus::GameInProgress {
            return Err(Error::GameNotInProgress)
        }
        if lobby_info.host_address != address && lobby_info.guest_address != address {
            return Err(Error::NotInLobby)
        }
        if game_state.phase != Phase::Setup {
            return Err(Error::WrongPhase)
        }
        if user_state.instruction != Instruction::RequestingSetupProof {
            return Err(Error::WrongInstruction)
        }
        let serialized = req.clone().to_xdr(e);
        let setup_hash: SetupHash = e.crypto().sha256(&serialized).to_bytes();
        if setup_hash != user_state.setup_hash {
            return Err(Error::SetupHashFail)
        }
        // update
        user_state.setup = req.setup.clone();
        user_state.instruction = Instruction::WaitingOppSetupProof;
        if opponent_state.instruction == Instruction::WaitingOppSetupProof {
            let mut pawns = Vec::new(e);
            for commit in user_state.setup.iter().chain(opponent_state.setup.iter()) {
                let starting_pos = Self::decode_pawn_id(&commit.pawn_id).0;
                let pawn = PawnState {
                    alive: true,
                    moved: false,
                    moved_scout: false,
                    pawn_id: commit.pawn_id.clone(),
                    pos: starting_pos,
                    revealed_rank: 63,
                };
                pawns.push_back(pawn);
            }
            game_state.board_state.pawns = pawns;
            game_state.phase = Phase::Movement;
            user_state.instruction = Instruction::RequestingMoveCommit;
            opponent_state.instruction = Instruction::RequestingMoveCommit;
        }
        game_state.user_states.set(player_index, user_state);
        game_state.user_states.set(opponent_index, opponent_state);
        // save
        temporary.set(&game_state_key, &game_state);

        Ok(())
    }

    pub fn commit_move(e: &Env, address: Address, req: CommitMoveReq) -> Result<(), Error> {
        address.require_auth();
        let temporary = e.storage().temporary();
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        let lobby_info: LobbyInfo = match temporary.get(&lobby_info_key) {
            Some(lobby_info) => lobby_info,
            None => return Err(Error::LobbyNotFound),
        };
        let game_state_key = DataKey::GameState(req.lobby_id.clone());
        let mut game_state: GameState = match temporary.get(&game_state_key) {
            Some(game_state) => game_state,
            None => return Err(Error::GameStateNotFound),
        };
        let player_index = Self::get_player_index(e, &address, &lobby_info);
        let opponent_index = Self::get_opponent_index(e, &address, &lobby_info);
        let mut user_state = game_state.user_states.get_unchecked(player_index);
        let mut opponent_state = game_state.user_states.get_unchecked(opponent_index);
        // validation
        if lobby_info.status != LobbyStatus::GameInProgress {
            return Err(Error::GameNotInProgress)
        }
        if lobby_info.host_address != address && lobby_info.guest_address != address {
            return Err(Error::NotInLobby)
        }
        if game_state.phase != Phase::Movement {
            return Err(Error::WrongPhase)
        }
        if user_state.instruction != Instruction::RequestingMoveCommit {
            return Err(Error::WrongInstruction)
        }
        // update
        user_state.latest_move_hash = req.move_hash.clone();
        user_state.instruction = Instruction::WaitingOppMoveCommit;
        if opponent_state.instruction == Instruction::WaitingOppMoveCommit {
            user_state.instruction = Instruction::RequestingMovePosProof;
            opponent_state.instruction = Instruction::RequestingMovePosProof;
        }
        game_state.user_states.set(player_index, user_state);
        game_state.user_states.set(opponent_index, opponent_state);
        // save
        temporary.set(&game_state_key, &game_state);

        Ok(())
    }

    pub fn prove_move(e: &Env, address: Address, req: ProveMoveReq) -> Result<(), Error> {
        // if we detect any irregularity when resolving, the game is suspended and a winner is immediately assigned
        // 0-1 are player_indexes, 2 means both moves were invalid, 63 is sentinel
        address.require_auth();
        let temporary = e.storage().temporary();
        let empty_move = HiddenMove {
            pawn_id: 0,
            pos: Pos {
                x: 0,
                y: 0,
            },
            salt: 0,
        };
        // this layer of validation is for forgivable offenses in the req that can be resubmitted correctly
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        let mut lobby_info: LobbyInfo = match temporary.get(&lobby_info_key) {
            Some(lobby_info) => lobby_info,
            None => return Err(Error::LobbyNotFound),
        };
        let game_state_key = DataKey::GameState(req.lobby_id.clone());
        let mut game_state: GameState = match temporary.get(&game_state_key) {
            Some(game_state) => game_state,
            None => return Err(Error::GameStateNotFound),
        };
        if lobby_info.host_address != address && lobby_info.guest_address != address {
            return Err(Error::NotInLobby)
        }
        let u_index = Self::get_player_index(e, &address, &lobby_info);
        let o_index = Self::get_opponent_index(e, &address, &lobby_info);
        let mut u_state = game_state.user_states.get_unchecked(u_index);
        let mut o_state = game_state.user_states.get_unchecked(o_index);
        if lobby_info.status != LobbyStatus::GameInProgress {
            return Err(Error::GameNotInProgress)
        }
        if lobby_info.host_address != address && lobby_info.guest_address != address {
            return Err(Error::NotInLobby)
        }
        if game_state.phase != Phase::Movement {
            return Err(Error::WrongPhase)
        }
        if u_state.instruction != Instruction::RequestingMovePosProof {
            return Err(Error::WrongInstruction)
        }
        // check if req.hidden_move hash matches the commit
        let serialized = req.hidden_move.clone().to_xdr(e);
        let hidden_move_hash: HiddenMoveHash = e.crypto().sha256(&serialized).to_bytes();
        if hidden_move_hash != u_state.latest_move_hash {
            return Err(Error::HiddenMoveHashFail)
        }
        // update user_state
        u_state.latest_move = req.hidden_move.clone();
        u_state.instruction = Instruction::WaitingOppMovePosProof;
        // index map
        let mut pawns_map: Map<PawnId, (u32, PawnState)> = Map::new(e);
        for tuple in game_state.board_state.pawns.iter().enumerate() {
            pawns_map.set(tuple.1.pawn_id.clone(), (tuple.0 as u32, tuple.1));
        }
        let both_players_submitted = o_state.instruction == Instruction::WaitingOppMovePosProof;
        //  both players have submitted moves, resolve
        if both_players_submitted {
            // from this point on, any irregularities are unforgivable and a winner must be assigned
            let u_move = u_state.latest_move.clone();
            let o_move = o_state.latest_move.clone();
            let (u_pawn_index, mut u_pawn) = pawns_map.get_unchecked(u_move.pawn_id.clone());
            let (o_pawn_index, mut o_pawn) = pawns_map.get_unchecked(o_move.pawn_id.clone());
            let u_move_start_pos = u_pawn.pos.clone();
            let o_move_start_pos = o_pawn.pos.clone();
            // check to make sure the move is valid
            let u_valid_move = Self::valid_move(&u_move, u_index, &u_pawn, &game_state);
            let o_valid_move = Self::valid_move(&o_move, o_index, &o_pawn, &game_state);
            // immediate failure if a user submits a invalid move
            // suspend game because a player submitted an invalid move and assign winner
            if let Some(winner) = Self::winner_index(u_valid_move, u_index, o_valid_move, o_index) {
                if winner == 0 {
                    lobby_info.status = LobbyStatus::HostWin;
                }
                else if winner == 1 {
                    lobby_info.status = LobbyStatus::GuestWin;
                }
                else if winner == 2 {
                    lobby_info.status = LobbyStatus::Draw;
                }
                u_state.instruction = Instruction::EndGame;
                o_state.instruction = Instruction::EndGame;
                game_state.user_states.set(u_index, u_state);
                game_state.user_states.set(o_index, o_state);
                temporary.set(&game_state_key, &game_state);
                temporary.set(&lobby_info_key, &lobby_info);
                return Ok(())
            }
            // resolve
            let mut u_proof_list: Vec<PawnId> = Vec::new(e);
            let mut o_proof_list: Vec<PawnId> = Vec::new(e);
            // naively update a copy of pawns with the latest moves
            // TODO: remove this let mut pawns = game_state.board_state.pawns.clone();
            // update both pawn states
            if u_pawn.pos != u_move.pos {
                u_pawn.moved = true;
            }
            if o_pawn.pos != o_move.pos {
                o_pawn.moved = true;
            }
            if !u_pawn.moved_scout {
                u_pawn.moved_scout = Self::is_scout_move(&u_pawn.pos, &u_move.pos);
            }
            if !o_pawn.moved_scout {
                o_pawn.moved_scout = Self::is_scout_move(&o_pawn.pos, &o_move.pos);
            }
            // we must always update pawns_map after setting u_pawn and o_pawn and *x_pawnsn
            u_pawn.pos = u_move.pos.clone();
            o_pawn.pos = o_move.pos.clone();
            pawns_map.set(u_pawn.pawn_id, (u_pawn_index, u_pawn.clone()));
            pawns_map.set(u_pawn.pawn_id, (o_pawn_index, o_pawn.clone()));

            // after pawns map is set, check for collisions

            let mut double_collision: Option<(PawnId, PawnId)> = None; // both players moved to the same square
            let mut swap_collision: Option<(PawnId, PawnId)> = None; // both players try to swap positions
            let mut u_collision: Option<(PawnId, PawnId)> = None; // u moved to a occupied pos
            let mut o_collision: Option<(PawnId, PawnId)> = None; // o moved to a occupied pos
            // if both players ran into each other we dont need to check for any other collisions
            if u_move.pos == o_move.pos {
                double_collision = Some((u_pawn.pawn_id.clone(), o_pawn.pawn_id.clone()));
            }
            else if u_move.pos == o_move_start_pos && o_move.pos == u_move_start_pos {
                swap_collision = Some((u_pawn.pawn_id.clone(), o_pawn.pawn_id.clone()));
            }
            else {
                // iterate and check collisions with other alive pawns
                for (x_pawn_id, (_, x_pawn)) in pawns_map.iter() {
                    if u_pawn.pawn_id == x_pawn_id {
                        continue
                    }
                    if o_pawn.pawn_id == x_pawn_id {
                        continue
                    }
                    if !x_pawn.alive {
                        continue
                    }
                    if u_pawn.pos == x_pawn.pos {
                        u_collision = Some((u_pawn.pawn_id.clone(), x_pawn_id.clone()));
                    }
                    if o_pawn.pos == x_pawn.pos {
                        o_collision = Some((o_pawn.pawn_id.clone(), x_pawn_id.clone()));
                    }
                    if u_collision.is_some() && o_collision.is_some() {
                        break;
                    }
                }
            }
            // check to see if we need to ask to reveal pawns
            // double collision and swap collision cant happen at the same time
            u_state.instruction = Instruction::WaitingOppMoveRankProof;
            o_state.instruction = Instruction::WaitingOppMoveRankProof;
            // handle collisions between moving u_pawn and moving o_pawn
            if double_collision.is_some() || swap_collision.is_some() {
                let mut can_resolve_now = true;
                if u_pawn.revealed_rank == 63 {
                    u_proof_list.push_back(u_pawn.pawn_id.clone());
                    can_resolve_now = false;
                }
                if o_pawn.revealed_rank == 63 {
                    o_proof_list.push_back(o_pawn.pawn_id.clone());
                    can_resolve_now = false;
                }
                if can_resolve_now {
                    // resolve conflict
                    // this mutates u_pawn and o_pawn!!!
                    (u_pawn, o_pawn) = Self::resolve_collision(&u_pawn, &o_pawn);
                    pawns_map.set(u_pawn.pawn_id, (u_pawn_index, u_pawn.clone()));
                    pawns_map.set(u_pawn.pawn_id, (o_pawn_index, o_pawn.clone()));
                }
            }
            else {
                // handle collision between a u_pawn and a stationary opponent pawn
                if let Some((_, ox_pawn_id)) = u_collision {
                    let mut can_resolve_now = true;
                    let (ox_pawn_index, mut ox_pawn) = pawns_map.get_unchecked(ox_pawn_id);
                    if u_pawn.revealed_rank == 63 {
                        u_proof_list.push_back(u_pawn.pawn_id.clone());
                        can_resolve_now = false;
                    }
                    if ox_pawn.revealed_rank == 63 {
                        o_proof_list.push_back(ox_pawn_id.clone());
                        can_resolve_now = false;
                    }
                    if can_resolve_now {
                        // resolve conflict
                        // this mutates u_pawn and n_pawn!!!
                        (u_pawn, ox_pawn) = Self::resolve_collision(&u_pawn, &ox_pawn);
                        pawns_map.set(u_pawn.pawn_id, (u_pawn_index, u_pawn.clone()));
                        pawns_map.set(u_pawn.pawn_id, (ox_pawn_index, ox_pawn.clone()));
                    }
                }
                // handle collision between o_pawn and a stationary user pawn
                if let Some((_, ux_pawn_id)) = o_collision {
                    let mut can_resolve_now = true;
                    let (ux_pawn_index, mut ux_pawn) = pawns_map.get_unchecked(ux_pawn_id);
                    if o_pawn.revealed_rank == 63 {
                        o_proof_list.push_back(o_pawn.pawn_id.clone());
                        can_resolve_now = false;
                    }
                    if ux_pawn.revealed_rank == 63 {
                        u_proof_list.push_back(ux_pawn_id.clone());
                        can_resolve_now = false;
                    }
                    if can_resolve_now {
                        // resolve conflict
                        (o_pawn, ux_pawn) = Self::resolve_collision(&o_pawn, &ux_pawn);
                        pawns_map.set(o_pawn.pawn_id, (o_pawn_index, o_pawn.clone()));
                        pawns_map.set(ux_pawn.pawn_id, (ux_pawn_index, ux_pawn.clone()));
                    }
                }
            }
            match (u_proof_list.is_empty(), o_proof_list.is_empty()) {
                (true, true) => {
                    // TODO: check if state is won
                    if let Some(status) = Self::is_game_over(e, &lobby_info, &game_state) {
                        // suspend game and early return
                        lobby_info.status = status;

                        u_state.instruction = Instruction::EndGame;
                        o_state.instruction = Instruction::EndGame;
                        game_state.user_states.set(u_index, u_state.clone());
                        game_state.user_states.set(o_index, o_state.clone());
                        temporary.set(&game_state_key, &game_state);
                        temporary.set(&lobby_info_key, &lobby_info);
                        return Ok(())
                    }
                    // transition to next turn
                    u_state.requested_pawn_ranks = Vec::new(e);
                    o_state.requested_pawn_ranks = Vec::new(e);
                    u_state.instruction = Instruction::RequestingMoveCommit;
                    o_state.instruction = Instruction::RequestingMoveCommit;
                    u_state.old_moves.push_back(u_state.latest_move.clone());
                    o_state.old_moves.push_back(o_state.latest_move.clone());
                    u_state.latest_move = empty_move.clone();
                    o_state.latest_move = empty_move.clone();
                }
                (_, false) => {
                    o_state.requested_pawn_ranks = o_proof_list.clone();
                    o_state.instruction = Instruction::RequestingMoveRankProof;

                }
                (false, _) => {
                    u_state.requested_pawn_ranks = u_proof_list.clone();
                    u_state.instruction = Instruction::RequestingMoveRankProof;
                }
            }
            // we're done writing pawns
            for (index, pawn) in pawns_map.values().iter() {
                game_state.board_state.pawns.set(index, pawn);
            }
            game_state.user_states.set(o_index, o_state);
        }
        // if the user was the first to submit their proof and the upper block is skipped, we just mutate
        // * user_state.latest_move = req.hidden_move
        // * user_state.instruction = Instruction::WaitingOppMovePosProof
        game_state.user_states.set(u_index, u_state);
        // save
        temporary.set(&game_state_key, &game_state);
        // lobby_info.status shouldn't change unless someone has won or quit the game
        // temporary.set(&lobby_info_key, &lobby_info);
        Ok(())
    }

    pub fn prove_ranks(e: &Env, address: Address, req: ProveRankReq) -> Result<(), Error> {
        address.require_auth();
        let temporary = e.storage().temporary();
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        let lobby_info: LobbyInfo = match temporary.get(&lobby_info_key) {
            // Some(packed_lobby_info) => Self::unpack_lobby_info(e, packed_lobby_info),
            Some(lobby_info) => lobby_info,
            None => return Err(Error::LobbyNotFound),
        };
        let game_state_key = DataKey::GameState(req.lobby_id.clone());
        let mut game_state: GameState = match temporary.get(&game_state_key) {
            Some(game_state) => game_state,
            None => return Err(Error::GameStateNotFound),
        };
        let player_index = Self::get_player_index(e, &address, &lobby_info);
        let mut user_state = game_state.user_states.get_unchecked(player_index);
        let opponent_index = Self::get_opponent_index(e, &address, &lobby_info);
        let mut opponent_state = game_state.user_states.get_unchecked(opponent_index);

        // check setup to see if rank matches
        let mut commit_map: Map<HiddenRankHash, PawnCommit> = Map::new(e);
        for pawn_commit in user_state.setup.iter() {
            commit_map.set(pawn_commit.hidden_rank_hash.clone(), pawn_commit);
        }
        let mut pawns_map: Map<PawnId, (u32, PawnState)> = Map::new(e);
        for tuple in game_state.board_state.pawns.iter().enumerate() {
            pawns_map.set(tuple.1.pawn_id.clone(), (tuple.0 as u32, tuple.1));
        }
        let mut error: Option<Error> = None;
        for hidden_rank in req.hidden_ranks.iter() {
            let serialized = hidden_rank.clone().to_xdr(e);
            let rank_hash: HiddenRankHash = e.crypto().sha256(&serialized).to_bytes();
            if let Some(pawn_commit) = commit_map.get(rank_hash.clone()) {
                if let Some((mut pawn_index, mut pawn)) = pawns_map.get(pawn_commit.pawn_id) {
                    if pawn.pawn_id == pawn_commit.pawn_id {
                        if rank_hash == pawn_commit.hidden_rank_hash {
                            // write
                            pawn.revealed_rank = hidden_rank.rank;
                            pawns_map.set(pawn.pawn_id, (pawn_index, pawn));
                        }
                        else {
                            error = Some(Error::HiddenRankHashFail);
                            break;
                        }
                    }
                    else {
                        error = Some(Error::WrongPawnId);
                        break;
                    }
                }
                else {
                    error = Some(Error::PawnNotFound);
                    break;
                }
            }
            else {
                error = Some(Error::WrongPawnId);
                break;
            }
        }
        for (index, pawn) in pawns_map.values().iter() {
            game_state.board_state.pawns.set(index, pawn);
        }
        if let Some(e) = error {
            return Err(e);
        }
        return Ok(())
    }

    pub(crate) fn is_game_over(e: &Env, lobby_info: &LobbyInfo, game_state: &GameState) -> Option<LobbyStatus> {
        // TODO: check game over state
        return None
    }

    pub(crate) fn resolve_collision(a_pawn: &PawnState, b_pawn: &PawnState) -> (PawnState, PawnState) {
        let mut a_pawn = a_pawn.clone();
        let mut b_pawn = b_pawn.clone();
        // special case for trap vs seer (seer wins)
        if a_pawn.revealed_rank == 11 && b_pawn.revealed_rank == 3 {
            a_pawn.alive = false;
        }
        else if a_pawn.revealed_rank == 3 && b_pawn.revealed_rank == 11 {
            b_pawn.alive = false;
        }
        // special case for warlord vs assassin (assassin wins)
        else if a_pawn.revealed_rank == 10 && b_pawn.revealed_rank == 1 {
            a_pawn.alive = false;
        }
        else if a_pawn.revealed_rank == 1 && b_pawn.revealed_rank == 10 {
            b_pawn.alive = false;
        }
        // all other cases
        else if a_pawn.revealed_rank < b_pawn.revealed_rank {
            a_pawn.alive = false;
        }
        else if a_pawn.revealed_rank > b_pawn.revealed_rank {
            b_pawn.alive = false;
        }
        else {
            // if equal ranks, both die
            a_pawn.alive = false;
            b_pawn.alive = false;
        }
        return (a_pawn, b_pawn)
    }

    pub(crate) fn valid_move(mv: &HiddenMove, player_index: u32, pawn: &PawnState, game_state: &GameState) -> bool {
        // cond: pawn must exist (we take this for granted if it's being fed into parameters)

        // TODO: cond: target pos must exist
        // cond: pawn must be owned by this player
        let owner = Self::decode_pawn_id(&pawn.pawn_id).1;
        if player_index != owner {
            return false
        }
        // cond: pawn must be alive
        if !pawn.alive {
            return false
        }
        // if pawn rank is revealed
        if pawn.revealed_rank != 63 {
            // cond: rank is not flag
            if pawn.revealed_rank == 0 {
                return false
            }
            // cond: rank is not trap
            if pawn.revealed_rank == 11 {
                return false
            }
            // TODO: cond: target pos must be in set of valid movable tiles
        }
        // iterate thru other alive pawns
        for n_pawn in game_state.board_state.pawns.iter() {
            // skip self
            if n_pawn.pawn_id == pawn.pawn_id {
                continue
            }
            if !n_pawn.alive {
                continue
            }
            if n_pawn.pos == mv.pos {
                // cond: pawn on target pos cant be same team
                let other_owner = Self::decode_pawn_id(&n_pawn.pawn_id).1;
                if other_owner == player_index {
                    return false
                }
            }
        }
        return true
    }
    pub(crate) fn winner_index(u_valid: bool, u_index: u32, o_valid: bool, o_index: u32) -> Option<u32> {
        match (u_valid, o_valid) {
            (true,  true ) => Some(2),
            (false, false) => None,
            (false, true ) => Some(o_index),
            (true, false ) => Some(u_index),
        }
    }

    pub(crate) fn empty_address(e: &Env) -> Address {
        Address::from_str(e, "GAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAWHF")
    }

    pub(crate) fn is_address_empty(e: &Env, address: &Address) -> bool {
        address.eq(&Self::empty_address(e))
    }

    pub(crate) fn is_scout_move(start: &Pos, end: &Pos) -> bool {
        let dx = if start.x > end.x { start.x - end.x } else { end.x - start.x };
        let dy = if start.y > end.y { start.y - end.y } else { end.y - start.y };
        // exactly one axis must change, and it must be at least 1
        (dx > 0 && dy == 0) || (dy > 0 && dx == 0)
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
            instruction: 0,
        }
    }
    //
    // pub(crate) fn pack_lobby_info(e: &Env, lobby_info: &LobbyInfo) -> PackedLobbyInfo {
    //     let mut buf = [0u8; 93];
    //     buf[0..4].copy_from_slice(&lobby_info.index.to_be_bytes());
    //     lobby_info.guest_address.clone().to_xdr(e).copy_into_slice(&mut buf[4..48]);
    //     lobby_info.host_address.clone().to_xdr(e).copy_into_slice(&mut buf[48..92]);
    //     buf[92] = lobby_info.phase.clone() as u8;
    //     BytesN::from_array(e, &buf)
    // }
    //
    // pub(crate) fn unpack_lobby_info(e: &Env, packed_lobby_info: PackedLobbyInfo) -> LobbyInfo {
    //     let arr: [u8; 93] = packed_lobby_info.to_array();
    //     LobbyInfo {
    //         index: u32::from_be_bytes(arr[0..4].try_into().unwrap()),
    //         guest_address: Address::from_xdr(e, &Bytes::from_slice(e, &arr[4..48])).unwrap(),
    //         host_address: Address::from_xdr(e, &Bytes::from_slice(e, &arr[48..92])).unwrap(),
    //         phase: match arr[92] {
    //             0 => Phase::Uninitialized,
    //             1 => Phase::Setup,
    //             2 => Phase::Movement,
    //             3 => Phase::Commitment,
    //             4 => Phase::Resolve,
    //             5 => Phase::Ending,
    //             6 => Phase::Aborted,
    //             _ => {panic!()}
    //         },
    //     }
    // }

    pub(crate) fn decode_pawn_id(pawn_id: &PawnId) -> (Pos, u32) {
        let is_red = (pawn_id & 1) == 0;
        let base_id = pawn_id >> 1;
        let pos = Pos {
            x: base_id  as i32 / 101,
            y: base_id  as i32 % 101,
        };
        (pos, if is_red {0} else {1})
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
            instruction: 0,
        };
        new_user
    }

    pub(crate) fn get_player_index(e: &Env, address: &Address, lobby_info: &LobbyInfo) -> u32 {
        // player index is also an identifier encoded into PawnId
        if Self::is_address_empty(e, address) {
            return 99
        }
        if address.clone() == lobby_info.host_address.clone() {
            return 0
        }
        if address.clone() == lobby_info.guest_address.clone() {
            return 1
        }
        return 99
    }

    pub(crate) fn get_opponent_index(e: &Env, address: &Address, lobby_info: &LobbyInfo) -> u32 {
        if Self::is_address_empty(e, address) {
            return 99
        }
        if address.clone() == lobby_info.host_address.clone() {
            return 1
        }
        if address.clone() == lobby_info.guest_address.clone() {
            return 0
        }
        return 99
    }

}
// endregion

mod test;// run tests