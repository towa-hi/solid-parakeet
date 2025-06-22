#![no_std]
use soroban_sdk::{*, log};
use soroban_sdk::xdr::*;
const DEBUG_LOGGING: bool = false;
macro_rules! debug_log {
    ($env:expr, $($arg:tt)*) => {
        if DEBUG_LOGGING {
            log!($env, $($arg)*);
        }
    };
}
// region global state defs

pub type LobbyId = u32;
pub type PawnId = u32;
pub type HiddenRankHash = BytesN<32>; // always the hash of HiddenRank struct
pub type HiddenMoveHash = BytesN<32>; // always the hash of HiddenMove struct
pub type SetupHash = BytesN<32>; // always the hash of Setup struct
pub type BoardHash = BytesN<32>; // not used at the moment
pub type Rank = u32;

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
    InvalidPawnId = 39,
    InvalidBoard = 40,
    WrongSubphase = 41,
    NoRankProofsNeeded = 42,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum Phase {
    Lobby = 0,
    SetupCommit = 1,
    SetupProve = 2,
    MoveCommit = 3,
    MoveProve = 4,
    RankProve = 5,
    Finished = 6,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum Subphase {
    Host = 0, // the host must do something
    Guest = 1, // the guest must do something
    Both = 2, // both must do something
    None = 3, // either nothing needs to be done, or a flag where both players have done something
}

// endregion
// region structs

#[contracttype]#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct Pos {
    pub x: i32,
    pub y: i32,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct User {
    pub current_lobby: Vec<LobbyId>,
    pub games_completed: u32,
    pub index: Address,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Tile {
    pub passable: bool,
    pub pos: Pos,
    pub setup: u32,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Board {
    pub hex: bool,
    pub name: String,
    pub size: Pos,
    pub tiles: Vec<Tile>,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct HiddenMove {
    pub pawn_id: PawnId,
    pub salt: u64,
    pub start_pos: Pos,
    pub target_pos: Pos,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct HiddenRank {
    pub pawn_id: PawnId,
    pub rank: Rank,
    pub salt: u64,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct SetupCommit {
    pub hidden_rank_hash: HiddenRankHash,
    pub pawn_id: PawnId,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Setup {
    pub salt: u64,
    pub setup_commits: Vec<SetupCommit>,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PawnState {
    pub alive: bool,
    pub hidden_rank_hash: HiddenRankHash,
    pub moved: bool,
    pub moved_scout: bool,
    pub pawn_id: PawnId,
    pub pos: Pos,
    pub rank: Vec<Rank>,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct UserSetup {
    pub setup: Vec<Setup>,
    pub setup_hash: Vec<SetupHash>,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct UserMove {
    pub move_hash: Vec<HiddenMoveHash>,
    pub move_proof: Vec<HiddenMove>,
    pub needed_rank_proofs: Vec<PawnId>,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct GameState {
    pub moves: Vec<UserMove>,
    pub pawns: Vec<PawnState>,
    pub setups: Vec<UserSetup>,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct LobbyParameters {
    pub board: Board,
    pub board_hash: BoardHash,
    pub dev_mode: bool,
    pub host_team: u32,
    pub max_ranks: Vec<u32>,
    pub must_fill_all_tiles: bool,
    pub security_mode: bool,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct LobbyInfo {
    pub guest_address: Vec<Address>,
    pub host_address: Vec<Address>,
    pub index: LobbyId,
    pub phase: Phase,
    pub subphase: Subphase,
}

// // endregion
// // region requests

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MakeLobbyReq {
    pub lobby_id: LobbyId,
    pub parameters: LobbyParameters,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct JoinLobbyReq {
    pub lobby_id: LobbyId,
}
// #[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
// pub struct LeaveLobbyReq {
//     pub lobby_id: LobbyId,
// }
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct CommitSetupReq {
    pub lobby_id: LobbyId,
    pub setup_hash: SetupHash,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct ProveSetupReq {
    pub lobby_id: LobbyId,
    pub setup: Setup,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct CommitMoveReq {
    pub lobby_id: LobbyId,
    pub move_hash: HiddenMoveHash,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct ProveMoveReq {
    pub lobby_id: LobbyId,
    pub move_proof: HiddenMove,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct ProveRankReq {
    pub hidden_ranks: Vec<HiddenRank>,
    pub lobby_id: LobbyId,
}

// // endregion
// // region keys

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum DataKey {
    User(Address),
    LobbyInfo(LobbyId), // lobby specific data
    LobbyParameters(LobbyId), // immutable lobby data
    GameState(LobbyId), // game state
}

// endregion
// region contract
#[contract]
pub struct Contract;

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct CollisionDetection {
    pub has_double_collision: bool,
    pub has_o_collision: bool,
    pub has_swap_collision: bool, 
    pub has_u_collision: bool,
    pub o_collision_target: Option<PawnId>,
    pub u_collision_target: Option<PawnId>,
}

#[contractimpl]
impl Contract {

    pub fn make_lobby(e: &Env, address: Address, req: MakeLobbyReq) -> Result<(), Error> {
        address.require_auth();
        let persistent = e.storage().persistent();
        let temporary = e.storage().temporary();
        let user_key = DataKey::User(address.clone());
        let mut user =  persistent.get(&user_key).unwrap_or_else(|| {
            User {
                current_lobby: Vec::new(e),
                games_completed: 0,
                index: address.clone(),
            }
        });
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id.clone());
        if temporary.has(&lobby_info_key) {
            return Err(Error::LobbyAlreadyExists)
        }
        let lobby_parameters_key = DataKey::LobbyParameters(req.lobby_id.clone());
        // light validation on boards just to make sure they make sense
        let mut board_invalid = false;
        let board = req.parameters.board.clone();
        if board.tiles.len() as i32 != board.size.x * board.size.y {
            board_invalid = true;
        }
        let mut used_positions: Map<Pos, bool> = Map::new(e);
        for tile in board.tiles.iter() {
            if used_positions.contains_key(tile.pos.clone()) {
                board_invalid = true;
                break;
            }
            used_positions.set(tile.pos.clone(), true);
            if tile.setup != 0 && tile.setup != 1 && tile.setup != 2 {
                board_invalid = true;
                break;
            }
            if tile.setup != 2 {
                if !tile.passable {
                    board_invalid = true;
                    break;
                }
            }
        }
        if board_invalid {
            return Err(Error::InvalidBoard)
        }
        // update
        let lobby_info = LobbyInfo {
            index: req.lobby_id.clone(),
            guest_address: Vec::new(e),
            host_address: Vec::from_array(e, [address.clone()]),
            phase: Phase::Lobby,
            subphase: Subphase::None,
        };
        user.current_lobby = Vec::from_array(e, [req.lobby_id.clone()]);
        // save
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&lobby_parameters_key, &req.parameters);
        persistent.set(&user_key, &user);
        Ok(())
    }

    pub fn leave_lobby(e: &Env, address: Address) -> Result<(), Error> {
        address.require_auth();
        let persistent = e.storage().persistent();
        let temporary = e.storage().temporary();
        let user_key = DataKey::User(address.clone());
        let mut user: User = match persistent.get(&user_key) {
            Some(user) => user,
            None => return Err(Error::UserNotFound),
        };
        let current_lobby_id = match user.current_lobby.get(0) {
            Some(id) => id,
            None => return Err(Error::LobbyNotFound),
        };
        let (lobby_info, _, _, lobby_info_key, _, _) = Self::get_lobby_data(e, &current_lobby_id, true, false, false)?;
        let mut lobby_info = lobby_info.unwrap();
        let user_index = Self::get_player_index(&address, &lobby_info)?;
        // update
        if lobby_info.host_address.contains(&address) {
            lobby_info.host_address.remove(0);
        }
        else if lobby_info.guest_address.contains(&address) {
            lobby_info.guest_address.remove(0);
        }
        user.current_lobby.remove(0);
        // if left while game is ongoing, assign winner
        if lobby_info.phase != Phase::Lobby && lobby_info.phase != Phase::Finished {
            lobby_info.subphase = Self::opponent_subphase_from_player_index(&user_index);
        }
        lobby_info.phase = Phase::Finished;
        // save
        persistent.set(&user_key, &user);
        temporary.set(&lobby_info_key, &lobby_info);
        Ok(())
    }

    pub fn join_lobby(e: &Env, address: Address, req: JoinLobbyReq) -> Result<(), Error> {
        address.require_auth();
        let persistent = e.storage().persistent();
        let temporary = e.storage().temporary();
        let user_key = DataKey::User(address.clone());
        let mut user =  persistent.get(&user_key).unwrap_or_else(|| {
            User {
                current_lobby: Vec::new(e),
                games_completed: 0,
                index: address.clone(),
            }
        });
        let maybe_old_lobby_id = user.current_lobby.get(0);
        if let Some(old_lobby_id) = maybe_old_lobby_id {
            if temporary.has(&DataKey::LobbyInfo(old_lobby_id.clone())) {
                return Err(Error::GuestAlreadyInLobby)
            }
        }
        let (lobby_info, _, lobby_parameters, lobby_info_key, _, _) = Self::get_lobby_data(e, &req.lobby_id, true, false, true)?;
        let mut lobby_info = lobby_info.unwrap();
        let lobby_parameters = lobby_parameters.unwrap();
        
        if lobby_info.phase != Phase::Lobby {
            return Err(Error::LobbyNotJoinable)
        }
        if lobby_info.host_address.is_empty() {
            return Err(Error::LobbyHasNoHost)
        }
        if lobby_info.host_address.contains(&address) {
            return Err(Error::JoinerIsHost)
        }
        if !lobby_info.guest_address.is_empty() {
            return Err(Error::LobbyNotJoinable)
        }
        // update
        user.current_lobby = Vec::from_array(e, [req.lobby_id.clone()]);
        lobby_info.guest_address = Vec::from_array(e, [address]);
        // start game automatically
        lobby_info.phase = Phase::SetupCommit;
        lobby_info.subphase = Subphase::Both;
        // generate pawns
        let mut pawns: Vec<PawnState> = Vec::new(e);
        for tile in lobby_parameters.board.tiles.iter() {
            if tile.setup == 0 || tile.setup == 1 {
                let pos = tile.pos;
                let team = tile.setup;
                let pawn_id = Self::encode_pawn_id(&pos, &team);
                let pawn_state = PawnState {
                    alive: true,
                    hidden_rank_hash: HiddenRankHash::from_array(e, &[0u8; 32]),
                    moved: false,
                    moved_scout: false,
                    pawn_id: pawn_id,
                    pos: pos.clone(),
                    rank: Vec::new(e),
                };
                pawns.push_back(pawn_state);
            }
        }
        let setups: Vec<UserSetup> = Vec::from_array(e, [
            UserSetup {
                setup: Vec::new(e),
                setup_hash: Vec::new(e),
            },
            UserSetup {
                setup: Vec::new(e),
                setup_hash: Vec::new(e),
            },
        ]);
        let game_state = GameState {
            moves: Vec::new(e),
            pawns: pawns,
            setups: setups,
        };
        lobby_info.phase = Phase::SetupCommit;
        lobby_info.subphase = Subphase::Both;
        // save
        persistent.set(&user_key, &user);
        temporary.set(&DataKey::GameState(req.lobby_id.clone()), &game_state);
        temporary.set(&lobby_info_key, &lobby_info);
        Ok(())
    }

    pub fn commit_setup(e: &Env, address: Address, req: CommitSetupReq) -> Result<(), Error> {
        // some state requirements:
        // lobby_info.phase must be Phase::SetupCommit
        // lobby_info.subphase must be Subphase::Both or invoker's Host/Guest
        address.require_auth();
        let temporary = e.storage().temporary();
        let (lobby_info, game_state, _, lobby_info_key, game_state_key, _) = Self::get_lobby_data(e, &req.lobby_id, true, true, false)?;
        let mut lobby_info = lobby_info.unwrap();
        let mut game_state = game_state.unwrap();
        
        let u_index = Self::get_player_index(&address, &lobby_info)?;
        if lobby_info.phase != Phase::SetupCommit {
            return Err(Error::WrongPhase)
        }
        let next_subphase = Self::next_subphase(&lobby_info.subphase, &u_index)?;
        let mut u_setup = game_state.setups.get_unchecked(u_index);
        u_setup.setup_hash = Vec::from_array(e, [req.setup_hash.clone()]);
        // update
        if next_subphase == Subphase::None {
            lobby_info.phase = Phase::SetupProve;
            lobby_info.subphase = Subphase::Both;
        }
        else {
            lobby_info.subphase = next_subphase;
        }
        game_state.setups.set(u_index, u_setup);
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&game_state_key, &game_state);
        Ok(())
    }

    pub fn prove_setup(e: &Env, address: Address, req: ProveSetupReq) -> Result<(), Error> {
        // some state requirements:
        // lobby_info.phase must be Phase::SetupProve
        // lobby_info.subphase must be Subphase::Both or invoker's Host/Guest
        // game_state.setups[u_index].setup_hash[0] must exist
        address.require_auth();
        let temporary = e.storage().temporary();
        
        let (lobby_info, game_state, _, lobby_info_key, game_state_key, _) = Self::get_lobby_data(e, &req.lobby_id, true, true, false)?;
        let mut lobby_info = lobby_info.unwrap();
        let mut game_state = game_state.unwrap();
        let u_index = Self::get_player_index(&address, &lobby_info)?;
        
        if lobby_info.phase != Phase::SetupProve {
            return Err(Error::WrongPhase)
        }
        let next_subphase = Self::next_subphase(&lobby_info.subphase, &u_index)?;
        let u_setup: UserSetup = game_state.setups.get_unchecked(u_index);
        let setup_hash = u_setup.setup_hash.get_unchecked(0);
        let serialized_setup_proof = req.setup.clone().to_xdr(e);
        let submitted_hash = e.crypto().sha256(&serialized_setup_proof).to_bytes();
        if setup_hash != submitted_hash {
            return Err(Error::SetupHashFail)
        }
        // validate the proof
        let mut setup_valid = true;
        let pawns_map = Self::create_pawns_map(e, &game_state);
        for commit in req.setup.setup_commits.iter() {
            let (_, x_index) = Self::decode_pawn_id(&commit.pawn_id);
            if x_index != u_index {
                setup_valid = false;
                break;
            }
            if !pawns_map.contains_key(commit.pawn_id) {
                setup_valid = false;
                break;
            }
        }
        // TODO: validate max_ranks
        if !setup_valid {
            // immediately abort the game
            lobby_info.phase = Phase::Finished;
            lobby_info.subphase = Self::opponent_subphase_from_player_index(&u_index);
            temporary.set(&lobby_info_key, &lobby_info);
            return Ok(())
        }
        // update
        for commit in req.setup.setup_commits.iter() {
            let (pawn_index, mut pawn_state) = pawns_map.get_unchecked(commit.pawn_id.clone());
            pawn_state.hidden_rank_hash = commit.hidden_rank_hash.clone();
            game_state.pawns.set(pawn_index, pawn_state);
        }
        if next_subphase == Subphase::None {
            Self::transition_to_move_commit(&mut lobby_info);
            game_state.moves = Self::create_empty_moves(e);
        }
        else {
            lobby_info.subphase = next_subphase;
        }
        
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&game_state_key, &game_state);
        Ok(())
    }

    pub fn commit_move(e: &Env, address: Address, req: CommitMoveReq) -> Result<(), Error> {
        address.require_auth();
        let temporary = e.storage().temporary();
        
        // First, get lobby info only to check phase and player membership
        let (lobby_info, _, _, lobby_info_key, _, _) = Self::get_lobby_data(e, &req.lobby_id, true, false, false)?;
        let mut lobby_info = lobby_info.unwrap();
        let u_index = Self::get_player_index(&address, &lobby_info)?;
        
        debug_log!(e, "commit_move: lobby_id={}, player_index={}, current_phase={:?}, current_subphase={:?}", 
             req.lobby_id, u_index, lobby_info.phase, lobby_info.subphase);
        
        if lobby_info.phase != Phase::MoveCommit {
            debug_log!(e, "commit_move: WrongPhase - expected MoveCommit, got {:?}", lobby_info.phase);
            return Err(Error::WrongPhase)
        }
        
        // Now get the game state since we know we're in the right phase
        let (_, game_state, _, _, game_state_key, _) = Self::get_lobby_data(e, &req.lobby_id, false, true, false)?;
        let mut game_state = game_state.unwrap();
        let next_subphase = Self::next_subphase(&lobby_info.subphase, &u_index)?;
        debug_log!(e, "commit_move: next_subphase will be {:?}", next_subphase);
        let mut u_move = game_state.moves.get_unchecked(u_index);
        // update
        u_move.move_hash = Vec::from_array(e, [req.move_hash.clone()]);
        if next_subphase == Subphase::None {
            debug_log!(e, "commit_move: Both players committed, transitioning to MoveProve/Both");
            lobby_info.phase = Phase::MoveProve;
            lobby_info.subphase = Subphase::Both;
        }
        else {
            debug_log!(e, "commit_move: Waiting for other player, subphase -> {:?}", next_subphase);
            lobby_info.subphase = next_subphase;
        }
        game_state.moves.set(u_index, u_move);
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&game_state_key, &game_state);
        Ok(())
    }

    pub fn prove_move(e: &Env, address: Address, req: ProveMoveReq) -> Result<(), Error> {
        // some state requirements:
        // lobby_info.phase must be Phase::MoveProve
        // lobby_info.subphase must be Subphase::Both or invoker's Host/Guest
        // game_state.moves[u_index].move_hash[0] must exist
        address.require_auth();
        let temporary = e.storage().temporary();
        
        let (lobby_info, game_state, lobby_parameters, lobby_info_key, game_state_key, _) = Self::get_lobby_data(e, &req.lobby_id, true, true, true)?;
        let mut lobby_info = lobby_info.unwrap();
        let mut game_state = game_state.unwrap();
        let lobby_parameters = lobby_parameters.unwrap();
        let u_index = Self::get_player_index(&address, &lobby_info)?;
        let o_index = Self::get_opponent_index(&address, &lobby_info)?;
        
        debug_log!(e, "prove_move: lobby_id={}, player_index={}, current_phase={:?}, current_subphase={:?}", 
             req.lobby_id, u_index, lobby_info.phase, lobby_info.subphase);
        
        if lobby_info.phase != Phase::MoveProve {
            debug_log!(e, "prove_move: WrongPhase - expected MoveProve, got {:?}", lobby_info.phase);
            return Err(Error::WrongPhase)
        }
        let next_subphase = Self::next_subphase(&lobby_info.subphase, &u_index)?;
        debug_log!(e, "prove_move: next_subphase will be {:?}", next_subphase);
        let mut u_move = game_state.moves.get_unchecked(u_index);
        let mut o_move = game_state.moves.get_unchecked(o_index);
        let serialized_move_proof = req.move_proof.clone().to_xdr(e);
        let submitted_hash = e.crypto().sha256(&serialized_move_proof).to_bytes();
        if u_move.move_hash.get_unchecked(0) != submitted_hash {
            return Err(Error::HiddenMoveHashFail)
        }
        u_move.move_proof = Vec::from_array(e, [req.move_proof.clone()]);
        // lightly validate the proof
        let u_move_valid = Self::validate_move_proof(e, &req.move_proof, &u_index, &game_state, &lobby_parameters);
        debug_log!(e, "prove_move: move validation result = {}", u_move_valid);
        if !u_move_valid {
            // immediately abort the game
            debug_log!(e, "prove_move: Invalid move! Setting phase to Finished");
            lobby_info.phase = Phase::Finished;
            lobby_info.subphase = Self::opponent_subphase_from_player_index(&u_index);
            temporary.set(&lobby_info_key, &lobby_info);
            return Ok(())
        }
        
        // Save the current player's move immediately so it's available for collision detection
        game_state.moves.set(u_index, u_move.clone());
        
        if next_subphase == Subphase::None {

            // apply changes to pawns and prepare to transition to the next MoveCommit or RankProve
            // pawns map will be applied back to game state later
            let mut pawns_map = Self::create_pawns_map(e, &game_state);
            
            let (u_pawn_index, mut u_pawn) = pawns_map.get_unchecked(u_move.move_proof.get_unchecked(0).pawn_id.clone());
            let (o_pawn_index, mut o_pawn) = pawns_map.get_unchecked(o_move.move_proof.get_unchecked(0).pawn_id.clone());
            
            let u_start_pos = u_move.move_proof.get_unchecked(0).start_pos;
            let o_start_pos = o_move.move_proof.get_unchecked(0).start_pos;
            let u_target_pos = u_move.move_proof.get_unchecked(0).target_pos;
            let o_target_pos = o_move.move_proof.get_unchecked(0).target_pos;
            let mut u_proof_list: Vec<PawnId> = Vec::new(e);
            let mut o_proof_list: Vec<PawnId> = Vec::new(e);
            // apply changes to pawns
            if u_pawn.pos != u_target_pos {
                u_pawn.moved = true;
            }
            if o_pawn.pos != o_target_pos {
                o_pawn.moved = true;
            }
            if Self::is_scout_move(&u_start_pos, &u_target_pos) {
                u_pawn.moved_scout = true;
            }
            if Self::is_scout_move(&o_start_pos, &o_target_pos) {
                o_pawn.moved_scout = true;
            }
            u_pawn.pos = u_target_pos.clone();
            o_pawn.pos = o_target_pos.clone();
            pawns_map.set(u_pawn.pawn_id.clone(), (u_pawn_index, u_pawn.clone()));
            pawns_map.set(o_pawn.pawn_id.clone(), (o_pawn_index, o_pawn.clone()));
            // check for collisions (when two pawns occupy the same position)
            let collision_detection = Self::detect_collisions(e, &u_pawn, &o_pawn, u_start_pos, o_start_pos, u_target_pos, o_target_pos, &pawns_map);
            debug_log!(e, "prove_move: collision detection - double: {:?}, swap: {:?}, u_collision: {:?}, o_collision: {:?}", 
                      collision_detection.has_double_collision, collision_detection.has_swap_collision, collision_detection.has_u_collision, collision_detection.has_o_collision);
            
            if collision_detection.has_double_collision || collision_detection.has_swap_collision {
                if u_pawn.rank.is_empty()  {
                    u_proof_list.push_back(u_pawn.pawn_id.clone());
                }
                if o_pawn.rank.is_empty() {
                    o_proof_list.push_back(o_pawn.pawn_id.clone());
                }
            }
            else {
                if let Some(u_collision_target) = collision_detection.u_collision_target {
                    let (_, ux_pawn) = pawns_map.get_unchecked(u_collision_target);
                    if u_pawn.rank.is_empty() {
                        u_proof_list.push_back(u_pawn.pawn_id.clone());
                    }
                    if ux_pawn.rank.is_empty() {
                        o_proof_list.push_back(u_collision_target.clone());
                    }
                }
                if let Some(o_collision_target) = collision_detection.o_collision_target {
                    let (_, ox_pawn) = pawns_map.get_unchecked(o_collision_target);
                    if o_pawn.rank.is_empty() {
                        o_proof_list.push_back(o_pawn.pawn_id.clone());
                    }
                    if ox_pawn.rank.is_empty() {
                        u_proof_list.push_back(o_collision_target.clone());
                    }
                }
            }
            u_move.needed_rank_proofs = u_proof_list.clone();
            o_move.needed_rank_proofs = o_proof_list.clone();
            
            debug_log!(e, "prove_move: rank proofs needed - u_proof_list len: {}, o_proof_list len: {}", 
                      u_proof_list.len(), o_proof_list.len());
            
            match (u_proof_list.is_empty(), o_proof_list.is_empty()) {
                (true, true) => {
                    debug_log!(e, "prove_move: No rank proofs needed, using shared resolution function");
                    
                    // Use shared function to ensure consistency
                    Self::complete_move_resolution(e, &mut game_state, u_move.clone(), o_move.clone(), &pawns_map)?;
                    
                    // Transition to next turn
                    Self::transition_to_move_commit(&mut lobby_info);
                    
                    debug_log!(e, "prove_move: Transitioned to MoveCommit/Both for next turn");
                }
                (true, false) => {
                    debug_log!(e, "prove_move: Opponent needs rank proofs, transitioning to RankProve/Opponent");
                    lobby_info.phase = Phase::RankProve;
                    lobby_info.subphase = Self::opponent_subphase_from_player_index(&u_index);
                    
                    // Apply changes to game state
                    Self::apply_pawns_to_game_state(&mut game_state, &pawns_map);
                    
                    // Set moves for the RankProve phase
                    game_state.moves.set(u_index, u_move.clone());
                    game_state.moves.set(o_index, o_move.clone());
                }
                (false, true) => {
                    debug_log!(e, "prove_move: User needs rank proofs, transitioning to RankProve/User");
                    lobby_info.phase = Phase::RankProve;
                    lobby_info.subphase = Self::user_subphase_from_player_index(&u_index);
                    
                    // Apply changes to game state
                    Self::apply_pawns_to_game_state(&mut game_state, &pawns_map);
                    
                    // Set moves for the RankProve phase
                    game_state.moves.set(u_index, u_move.clone());
                    game_state.moves.set(o_index, o_move.clone());
                }
                (false, false) => {
                    debug_log!(e, "prove_move: Both need rank proofs, transitioning to RankProve/Both");
                    lobby_info.phase = Phase::RankProve;
                    lobby_info.subphase = Subphase::Both;
                    
                    // Apply changes to game state
                    Self::apply_pawns_to_game_state(&mut game_state, &pawns_map);
                    
                    // Set moves for the RankProve phase
                    game_state.moves.set(u_index, u_move.clone());
                    game_state.moves.set(o_index, o_move.clone());
                }
            }
        } else {
            lobby_info.subphase = next_subphase;
        }
        
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&game_state_key, &game_state);
        debug_log!(e, "prove_move: Completed successfully. Final phase={:?}, subphase={:?}", lobby_info.phase, lobby_info.subphase);
        Ok(())
    }

    pub fn prove_rank(e: &Env, address: Address, req: ProveRankReq) -> Result<(), Error> {
        address.require_auth();
        let temporary = e.storage().temporary();
        
        let (lobby_info, game_state, _, lobby_info_key, game_state_key, _) = Self::get_lobby_data(e, &req.lobby_id, true, true, false)?;
        let mut lobby_info = lobby_info.unwrap();
        let mut game_state = game_state.unwrap();
        let u_index = Self::get_player_index(&address, &lobby_info)?;
        
        if lobby_info.phase != Phase::RankProve {
            return Err(Error::WrongPhase)
        }
        
        // Use standard subphase pattern like other functions
        let next_subphase = Self::next_subphase(&lobby_info.subphase, &u_index)?;
        
        let u_move = game_state.moves.get_unchecked(u_index);
        if u_move.needed_rank_proofs.is_empty() {
            return Err(Error::NoRankProofsNeeded)
        }
        
        if u_move.needed_rank_proofs.len() != req.hidden_ranks.len() {
            return Err(Error::InvalidArgs)
        }
        
        let mut valid_rank_proof = true;
        let mut pawns_map = Self::create_pawns_map(e, &game_state);
        for hidden_rank in req.hidden_ranks.iter() {
            // rehash hidden_rank
            let serialized_hidden_rank = hidden_rank.clone().to_xdr(e);
            let rank_hash = e.crypto().sha256(&serialized_hidden_rank).to_bytes();
            let (pawn_index, mut pawn) = match pawns_map.get(hidden_rank.pawn_id.clone()) {
                Some(tuple) => tuple,
                None => {
                    valid_rank_proof = false;
                    break;
                }
            };
            if pawn.hidden_rank_hash != rank_hash {
                valid_rank_proof = false;
                break;
            }
            debug_log!(e, "set rank for pawnid {} to {}", pawn.pawn_id, hidden_rank.rank);
            pawn.rank = Vec::from_array(e, [hidden_rank.rank.clone()]);
            pawns_map.set(pawn.pawn_id.clone(), (pawn_index, pawn.clone()));
        }
        if !valid_rank_proof {
            // abort the game
            lobby_info.phase = Phase::Finished;
            lobby_info.subphase = Self::opponent_subphase_from_player_index(&u_index);
            temporary.set(&lobby_info_key, &lobby_info);
            return Ok(())
        }
        
        // Clear the needed_rank_proofs for the pawns that were just proved
        let mut u_move = game_state.moves.get_unchecked(u_index);
        let mut new_needed_rank_proofs = Vec::new(e);
        for needed_pawn_id in u_move.needed_rank_proofs.iter() {
            let mut was_proved = false;
            for hidden_rank in req.hidden_ranks.iter() {
                if hidden_rank.pawn_id == needed_pawn_id {
                    was_proved = true;
                    break;
                }
            }
            if !was_proved {
                new_needed_rank_proofs.push_back(needed_pawn_id);
            }
        }
        u_move.needed_rank_proofs = new_needed_rank_proofs;
        game_state.moves.set(u_index, u_move.clone());
        
        // Apply pawns to game state
        Self::apply_pawns_to_game_state(&mut game_state, &pawns_map);
        
        // Use standard subphase transition pattern like other functions
        if next_subphase == Subphase::None {
            // Both players have acted, check if we can transition to next phase
            let u_move = game_state.moves.get_unchecked(0);
            let o_move = game_state.moves.get_unchecked(1);
            let all_rank_proofs_satisfied = u_move.needed_rank_proofs.is_empty() && o_move.needed_rank_proofs.is_empty();
            
            if all_rank_proofs_satisfied {
                // All rank proofs satisfied - resolve collision and transition to next turn
                Self::complete_move_resolution(e, &mut game_state, u_move.clone(), o_move.clone(), &pawns_map)?;
                
                // Transition to next turn
                Self::transition_to_move_commit(&mut lobby_info);
            } else {
                // Some rank proofs still needed - determine who needs to act next
                let host_still_needs_proofs = !u_move.needed_rank_proofs.is_empty();
                let guest_still_needs_proofs = !o_move.needed_rank_proofs.is_empty();
                
                lobby_info.subphase = match (host_still_needs_proofs, guest_still_needs_proofs) {
                    (true, true) => Subphase::Both,    // Both players still need to prove
                    (true, false) => Subphase::Host,   // Only host needs to prove
                    (false, true) => Subphase::Guest,  // Only guest needs to prove
                    (false, false) => unreachable!("Should have been caught by all_rank_proofs_satisfied check")
                };
            }
        } else {
            // Standard case: advance to next player's turn
            lobby_info.subphase = next_subphase;
        }
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&game_state_key, &game_state);
        Ok(())
    }

    // region helper functions

    // Redundancy Elimination Helpers
    pub(crate) fn create_pawns_map(e: &Env, game_state: &GameState) -> Map<PawnId, (u32, PawnState)> {
        let mut pawns_map: Map<PawnId, (u32, PawnState)> = Map::new(e);
        for tuple in game_state.pawns.iter().enumerate() {
            pawns_map.set(tuple.1.pawn_id.clone(), (tuple.0 as u32, tuple.1));
        }
        pawns_map
    }

    pub(crate) fn apply_pawns_to_game_state(game_state: &mut GameState, pawns_map: &Map<PawnId, (u32, PawnState)>) {
        for (index, pawn) in pawns_map.values().iter() {
            game_state.pawns.set(index, pawn);
        }
    }

    pub(crate) fn create_empty_moves(e: &Env) -> Vec<UserMove> {
        Vec::from_array(e, [
            UserMove {
                move_hash: Vec::new(e),
                move_proof: Vec::new(e),
                needed_rank_proofs: Vec::new(e),
            },
            UserMove {
                move_hash: Vec::new(e),
                move_proof: Vec::new(e),
                needed_rank_proofs: Vec::new(e),
            },
        ])
    }

    pub(crate) fn transition_to_move_commit(lobby_info: &mut LobbyInfo) {
        lobby_info.phase = Phase::MoveCommit;
        lobby_info.subphase = Subphase::Both;
    }

    // State Management Helpers
    pub(crate) fn next_subphase(current_subphase: &Subphase, u_index: &u32) -> Result<Subphase, Error> {
        let result = match current_subphase {
            Subphase::Both => Ok(Self::opponent_subphase_from_player_index(&u_index)),
            Subphase::Host => if *u_index == 0 {Ok(Subphase::None)} else {return Err(Error::WrongSubphase)},
            Subphase::Guest => if *u_index == 1 {Ok(Subphase::None)} else {return Err(Error::WrongSubphase)},
            Subphase::None => return Err(Error::WrongSubphase),
        };
        result
    }

    pub(crate) fn user_subphase_from_player_index(player_index: &u32) -> Subphase {
        if *player_index == 0 { Subphase::Host } else { Subphase::Guest }
    }

    pub(crate) fn opponent_subphase_from_player_index(player_index: &u32) -> Subphase {
        if *player_index == 0 { Subphase::Guest } else { Subphase::Host }
    }

    // Player and Team Helpers
    pub(crate) fn get_player_index(address: &Address, lobby_info: &LobbyInfo) -> Result<u32, Error> {
        // player index is also an identifier encoded into PawnId
        if lobby_info.host_address.contains(address) {
            return Ok(0)
        }
        if lobby_info.guest_address.contains(address) {
            return Ok(1)
        }
        return Err(Error::NotInLobby)
    }

    pub(crate) fn get_opponent_index(address: &Address, lobby_info: &LobbyInfo) -> Result<u32, Error> {
        if lobby_info.host_address.contains(address) {
            return Ok(1)
        }
        if lobby_info.guest_address.contains(address) {
            return Ok(0)
        }
        return Err(Error::NotInLobby)
    }

    pub(crate) fn encode_pawn_id(pos: &Pos, team: &u32) -> PawnId {
        let mut id = 0;
        id += pos.x as u32 * 101;
        id += pos.y as u32;
        id = id << 1;  // Shift position bits left to make room for team bit
        id += *team;   // Team goes in the least significant bit
        id
    }

    pub(crate) fn decode_pawn_id(pawn_id: &PawnId) -> (Pos, u32) {
        let is_red = (pawn_id & 1) == 0;
        let base_id = pawn_id >> 1;
        let pos = Pos {
            x: base_id  as i32 / 101,
            y: base_id  as i32 % 101,
        };
        (pos, if is_red {0} else {1})
    }

    // Movement and Validation Helpers
    pub(crate) fn is_scout_move(start_pos: &Pos, end_pos: &Pos) -> bool {
        let dx = end_pos.x - start_pos.x;
        let dy = end_pos.y - start_pos.y;
        if dx.abs() > 1 || dy.abs() > 1 {
            return false
        }
        return true
    }

    pub(crate) fn validate_move_proof(e: &Env, move_proof: &HiddenMove, player_index: &u32, game_state: &GameState, lobby_parameters: &LobbyParameters) -> bool {
        debug_log!(e, "validate_move_proof: pawn_id={}, player_index={}, start_pos=({},{}), target_pos=({},{})", 
             move_proof.pawn_id, player_index, move_proof.start_pos.x, move_proof.start_pos.y, 
             move_proof.target_pos.x, move_proof.target_pos.y);
        
        let pawns_map = Self::create_pawns_map(e, game_state);
        // cond: pawn must exist in game state
        let (_index, pawn) = match pawns_map.get(move_proof.pawn_id.clone()) {
            Some(tuple) => tuple,
            None => {
                debug_log!(e, "validate_move_proof: FAIL - pawn not found in game state");
                return false;
            }
        };
        debug_log!(e, "validate_move_proof: Found pawn at pos=({},{}) alive={}", pawn.pos.x, pawn.pos.y, pawn.alive);
        
        // cond: start pos must match
        if move_proof.start_pos != pawn.pos {
            debug_log!(e, "validate_move_proof: FAIL - start pos mismatch. Expected ({},{}), got ({},{})", 
                 pawn.pos.x, pawn.pos.y, move_proof.start_pos.x, move_proof.start_pos.y);
            return false
        }
        
        // cond target pos must be valid - check bounds based on actual board positions
        let min_x = 0;
        let max_x = lobby_parameters.board.size.x;
        let min_y = 0;
        let max_y = lobby_parameters.board.size.y;
        
        if move_proof.target_pos.x < min_x || move_proof.target_pos.x > max_x || 
           move_proof.target_pos.y < min_y || move_proof.target_pos.y > max_y {
            debug_log!(e, "validate_move_proof: FAIL - target pos out of bounds. Board bounds: x=[{},{}], y=[{},{}]", 
                 min_x, max_x, min_y, max_y);
            return false
        }
        
        // cond: pawn must be alive
        if !pawn.alive {
            debug_log!(e, "validate_move_proof: FAIL - pawn is dead");
            return false
        }
        // cond: player is owner
        let (_initial_pos, team) = Self::decode_pawn_id(&move_proof.pawn_id);
        if team != *player_index {
            debug_log!(e, "validate_move_proof: FAIL - player doesn't own pawn. Team={}, player_index={}", team, player_index);
            return false
        }
        // cond: pawn is not unmovable rank (flag or trap)
        if let Some(rank) = pawn.rank.get(0) {
            if rank == 0 {
                debug_log!(e, "validate_move_proof: FAIL - pawn is a flag (rank 0)");
                return false
            }
            if rank == 11 {
                debug_log!(e, "validate_move_proof: FAIL - pawn is a bomb (rank 11)");
                return false
            }
            // TODO: cond: target pos must be in the set of valid positions depending on rank
        }
        for n_pawn in game_state.pawns.iter() {
            if n_pawn.pawn_id == pawn.pawn_id {
                continue
            }
            if !n_pawn.alive {
                continue
            }
            if n_pawn.pos == move_proof.target_pos {
                // cond: pawn on target pos can't be same team
                let other_owner = Self::decode_pawn_id(&n_pawn.pawn_id).1;
                if other_owner == *player_index {
                    debug_log!(e, "validate_move_proof: FAIL - target pos occupied by same team pawn");
                    return false
                }
            }
        }
        debug_log!(e, "validate_move_proof: PASS - move is valid");
        return true
    }

    // Collision Detection and Resolution
    pub(crate) fn detect_collisions(
        e: &Env,
        u_pawn: &PawnState, 
        o_pawn: &PawnState,
        u_start_pos: Pos,
        o_start_pos: Pos, 
        u_target_pos: Pos,
        o_target_pos: Pos,
        pawns_map: &Map<PawnId, (u32, PawnState)>
    ) -> CollisionDetection {
        let mut has_double_collision = false;
        let mut has_swap_collision = false;
        let mut has_u_collision = false;
        let mut has_o_collision = false;
        let mut u_collision_target: Option<PawnId> = None;
        let mut o_collision_target: Option<PawnId> = None;
        
        if u_target_pos == o_target_pos {
            has_double_collision = true;
            debug_log!(e, "Detected double collision between {} and {}", u_pawn.pawn_id, o_pawn.pawn_id);
        }
        else if u_target_pos == o_start_pos && o_target_pos == u_start_pos {
            has_swap_collision = true;
            debug_log!(e, "Detected swap collision between {} and {}", u_pawn.pawn_id, o_pawn.pawn_id);
        }
        else {
            // Check for collisions with stationary pawns
            for (x_pawn_id, (_, x_pawn)) in pawns_map.iter() {
                if u_pawn.pawn_id == x_pawn_id || o_pawn.pawn_id == x_pawn_id || !x_pawn.alive {
                    continue;
                }
                if u_pawn.pos == x_pawn.pos {
                    has_u_collision = true;
                    u_collision_target = Some(x_pawn_id.clone());
                    debug_log!(e, "Detected user collision between {} and {}", u_pawn.pawn_id, x_pawn_id);
                }
                if o_pawn.pos == x_pawn.pos {
                    has_o_collision = true;
                    o_collision_target = Some(x_pawn_id.clone());
                    debug_log!(e, "Detected opponent collision between {} and {}", o_pawn.pawn_id, x_pawn_id);
                }
                if has_u_collision && has_o_collision {
                    break;
                }
            }
        }
        
        CollisionDetection {
            has_double_collision,
            has_o_collision,
            has_swap_collision,
            has_u_collision,
            o_collision_target,
            u_collision_target,
        }
    }

    pub(crate) fn resolve_collision(a_pawn: &mut PawnState, b_pawn: &mut PawnState) -> () {
        if a_pawn.rank.is_empty() || b_pawn.rank.is_empty() {
            panic!("a_pawn or b_pawn has no rank");
        }
        if !a_pawn.alive || !b_pawn.alive {
            panic!("a_pawn or b_pawn is not alive");
        }
        let a_pawn_rank = a_pawn.rank.get_unchecked(0);
        let b_pawn_rank = b_pawn.rank.get_unchecked(0);
        // special case for trap vs seer
        if a_pawn_rank == 11 && b_pawn_rank == 3 {
            a_pawn.alive = false;
        }
        else if b_pawn_rank == 11 && a_pawn_rank == 3 {
            b_pawn.alive = false;
        }
        // special case for warlord vs assassin
        else if a_pawn_rank == 10 && b_pawn_rank == 1 {
            a_pawn.alive = false;
        }
        else if b_pawn_rank == 10 && a_pawn_rank == 1 {
            b_pawn.alive = false;
        }
        // normal cases
        else if a_pawn_rank < b_pawn_rank {
            a_pawn.alive = false;
        }
        else if b_pawn_rank < a_pawn_rank {
            b_pawn.alive = false;
        }
        // both die if equal rank
        else {
            a_pawn.alive = false;
            b_pawn.alive = false;
        }
    }

    pub(crate) fn complete_move_resolution(e: &Env, game_state: &mut GameState, u_move: UserMove, o_move: UserMove, pawns_map: &Map<PawnId, (u32, PawnState)>) -> Result<(), Error> {
        let u_index = 0;
        let o_index = 1;
        
        // Apply pawn changes to game state
        Self::apply_pawns_to_game_state(game_state, pawns_map);
        
        // Set current moves in game state
        game_state.moves.set(u_index, u_move.clone());
        game_state.moves.set(o_index, o_move.clone());
        
        // Now perform collision resolution using the updated game state
        let u_move_proof = u_move.move_proof.get_unchecked(0);
        let o_move_proof = o_move.move_proof.get_unchecked(0);
        
        // Get the updated pawns from game state for collision resolution
        let mut updated_pawns_map = Self::create_pawns_map(e, game_state);
        
        let (u_pawn_index, mut u_pawn) = updated_pawns_map.get_unchecked(u_move_proof.pawn_id.clone());
        let (o_pawn_index, mut o_pawn) = updated_pawns_map.get_unchecked(o_move_proof.pawn_id.clone());
        
        // Detect and resolve collisions
        let collision_detection = Self::detect_collisions(e, &u_pawn, &o_pawn, u_move_proof.start_pos, o_move_proof.start_pos, u_move_proof.target_pos, o_move_proof.target_pos, &updated_pawns_map);
        
        if collision_detection.has_double_collision {
            debug_log!(e, "complete_move_resolution: Resolving double collision");
            Self::resolve_collision(&mut u_pawn, &mut o_pawn);
            updated_pawns_map.set(u_pawn.pawn_id.clone(), (u_pawn_index, u_pawn.clone()));
            updated_pawns_map.set(o_pawn.pawn_id.clone(), (o_pawn_index, o_pawn.clone()));
        }
        else if collision_detection.has_swap_collision {
            debug_log!(e, "complete_move_resolution: Resolving swap collision");
            Self::resolve_collision(&mut u_pawn, &mut o_pawn);
            updated_pawns_map.set(u_pawn.pawn_id.clone(), (u_pawn_index, u_pawn.clone()));
            updated_pawns_map.set(o_pawn.pawn_id.clone(), (o_pawn_index, o_pawn.clone()));
        }
        else {
            if let Some(u_collision_target) = collision_detection.u_collision_target {
                debug_log!(e, "complete_move_resolution: Resolving user collision with stationary pawn {}", u_collision_target);
                let (ux_pawn_index, mut ux_pawn) = updated_pawns_map.get_unchecked(u_collision_target);
                Self::resolve_collision(&mut u_pawn, &mut ux_pawn);
                updated_pawns_map.set(u_pawn.pawn_id.clone(), (u_pawn_index, u_pawn.clone()));
                updated_pawns_map.set(ux_pawn.pawn_id.clone(), (ux_pawn_index, ux_pawn.clone()));
            }
            
            if let Some(o_collision_target) = collision_detection.o_collision_target {
                debug_log!(e, "complete_move_resolution: Resolving opponent collision with stationary pawn {}", o_collision_target);
                let (ox_pawn_index, mut ox_pawn) = updated_pawns_map.get_unchecked(o_collision_target);
                Self::resolve_collision(&mut o_pawn, &mut ox_pawn);
                updated_pawns_map.set(o_pawn.pawn_id.clone(), (o_pawn_index, o_pawn.clone()));
                updated_pawns_map.set(ox_pawn.pawn_id.clone(), (ox_pawn_index, ox_pawn.clone()));
            }
        }
        
        // Apply collision resolution results back to game state
        Self::apply_pawns_to_game_state(game_state, &updated_pawns_map);
        
        // Reset moves for next turn
        game_state.moves = Self::create_empty_moves(e);
        
        Ok(())
    }

    // Data Access Helpers
    pub(crate) fn get_lobby_data(
        e: &Env, 
        lobby_id: &LobbyId, 
        need_lobby_info: bool, 
        need_game_state: bool, 
        need_lobby_parameters: bool
    ) -> Result<(Option<LobbyInfo>, Option<GameState>, Option<LobbyParameters>, DataKey, DataKey, DataKey), Error> {
        let temporary = e.storage().temporary();
        
        let lobby_info_key = DataKey::LobbyInfo(lobby_id.clone());
        let game_state_key = DataKey::GameState(lobby_id.clone());
        let lobby_parameters_key = DataKey::LobbyParameters(lobby_id.clone());
        
        let lobby_info = if need_lobby_info {
            match temporary.get(&lobby_info_key) {
                Some(info) => Some(info),
                None => return Err(Error::LobbyNotFound),
            }
        } else {
            None
        };
        
        let game_state = if need_game_state {
            match temporary.get(&game_state_key) {
                Some(state) => Some(state),
                None => return Err(Error::GameStateNotFound),
            }
        } else {
            None
        };
        
        let lobby_parameters = if need_lobby_parameters {
            match temporary.get(&lobby_parameters_key) {
                Some(params) => Some(params),
                None => return Err(Error::LobbyNotFound),
            }
        } else {
            None
        };
        
        Ok((lobby_info, game_state, lobby_parameters, lobby_info_key, game_state_key, lobby_parameters_key))
    }

    // endregion

}
// endregion


mod test;// run tests
mod test_utils; // test utilities