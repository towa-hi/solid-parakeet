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
pub type HiddenRankHash = BytesN<16>; // always the hash of HiddenRank struct
pub type HiddenMoveHash = BytesN<16>; // always the hash of HiddenMove struct
pub type SetupHash = BytesN<16>; // always the hash of Setup struct
pub type BoardHash = BytesN<16>; // not used at the moment
pub type MerkleHash = BytesN<16>;
pub type Rank = u32;
pub type PackedTile = u32;

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
    ParametersInvalid = 43,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum Phase {
    Lobby = 0,
    SetupCommit = 1,
    MoveCommit = 2,
    MoveProve = 3,
    RankProve = 4,
    Finished = 5,
    Aborted = 6,
}

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum Subphase {
    Host = 0, // the host must do something
    Guest = 1, // the guest must do something
    Both = 2, // both must do something
    None = 3, // either nothing needs to be done, or a flag where both players have done something
}

// endregion
// region merkel structs

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MerkleProof {
    pub leaf_index: u32,
    pub siblings: Vec<MerkleHash>,
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
    pub current_lobby: LobbyId,
    pub games_completed: u32,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Tile {           // packs into 32 bit PackedTile
    pub passable: bool,     // bit 0
    pub pos: Pos,           // bit 1-18, x & y range -256 to 255
    pub setup: u32,         // bit 19-21 range 0-2 for now
    pub setup_zone: u32,    // bit 22-24 range 0-4 for now
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Board {
    pub hex: bool,
    pub name: String,
    pub size: Pos,
    pub tiles: Vec<PackedTile>,
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
//    pub hidden_rank_hash: HiddenRankHash,
    pub moved: bool,
    pub moved_scout: bool,
    pub pawn_id: PawnId,
    pub pos: Pos,
    pub rank: Vec<Rank>,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct UserMove {
    pub move_hash: HiddenMoveHash,
    pub move_proof: Vec<HiddenMove>,
    pub needed_rank_proofs: Vec<PawnId>,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct GameState {
    pub moves: Vec<UserMove>,
    pub pawns: Vec<PawnState>,
    pub rank_roots: Vec<MerkleHash>,
    pub turn: u32,
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
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct History {
    pub guest_moves: Vec<HiddenMove>,
    pub host_moves: Vec<HiddenMove>,
    pub lobby_id: LobbyId,
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
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct CommitSetupReq {
    pub lobby_id: LobbyId,
    pub rank_commitment_root: MerkleHash,
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
    pub merkle_proofs: Vec<MerkleProof>,
}

// // endregion
// // region keys

#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub enum DataKey {
    User(Address),
    LobbyInfo(LobbyId), // lobby specific data
    LobbyParameters(LobbyId), // immutable lobby data
    GameState(LobbyId), // game state
    History(LobbyId),
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
    pub o_pawn_id: Option<PawnId>,
    pub u_collision_target: Option<PawnId>,
    pub u_pawn_id: Option<PawnId>,
}

#[contractimpl]
impl Contract {

    pub fn test_easy(_e: &Env, address: Address) -> Result<(), Error> {
        address.require_auth();
        Ok(())
    }

    pub fn test_function(_e: &Env, address: Address, _req: MakeLobbyReq) -> Result<(), Error> {
        address.require_auth();
        Ok(())
    }
    
    pub fn make_lobby(e: &Env, address: Address, req: MakeLobbyReq) -> Result<(), Error> {
        address.require_auth();
        let persistent = e.storage().persistent();
        let temporary = e.storage().temporary();
        let user_key = DataKey::User(address.clone());
        let mut user =  persistent.get(&user_key).unwrap_or_else(|| {
            User {
                current_lobby: 0,
                games_completed: 0,
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
        for packed_tile in board.tiles.iter() {
            let tile = Self::unpack_tile(packed_tile);
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
            if tile.setup_zone != 0 && tile.setup_zone != 1 && tile.setup_zone != 2 && tile.setup_zone != 3 && tile.setup_zone != 4 {
                board_invalid = true;
                break;
            }
        }
        if board_invalid {
            return Err(Error::InvalidBoard)
        }
        let mut parameters_invalid = false;
        // parameter validation
        for (i, max) in req.parameters.max_ranks.iter().enumerate() {
            let index = i as u32;
            if index == 0 {
                if max == 0 {
                    parameters_invalid = true;
                    break;
                }
            }
            // cant submit rank unknown pawns
            if index == 12 {
                if max != 0 {
                    parameters_invalid = true;
                    break;
                }
            }
        }
        if parameters_invalid {
            return Err(Error::ParametersInvalid)
        }
        // update
        let lobby_info = LobbyInfo {
            index: req.lobby_id.clone(),
            guest_address: Vec::new(e),
            host_address: Vec::from_array(e, [address.clone()]),
            phase: Phase::Lobby,
            subphase: Subphase::None,
        };
        user.current_lobby = req.lobby_id.clone();
        let history = History {
            lobby_id: req.lobby_id.clone(),
            host_moves: Vec::new(e),
            guest_moves: Vec::new(e),
        };
        let history_key = DataKey::History(req.lobby_id.clone());
        // save
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&lobby_parameters_key, &req.parameters);
        temporary.set(&history_key, &history);
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
        if user.current_lobby == 0 {
            return Err(Error::LobbyNotFound)
        }
        let (lobby_info, _, _, lobby_info_key, _, _) = Self::get_lobby_data(e, &user.current_lobby, true, false, false)?;
        let mut lobby_info = lobby_info.unwrap();
        let user_index = Self::get_player_index(&address, &lobby_info)?;
        // update
        if lobby_info.host_address.contains(&address) {
            lobby_info.host_address.remove(0);
        }
        else if lobby_info.guest_address.contains(&address) {
            lobby_info.guest_address.remove(0);
        }
        user.current_lobby = 0;
        // if left while game is ongoing, assign winner
        if lobby_info.phase != Phase::Lobby && lobby_info.phase != Phase::Aborted && lobby_info.phase != Phase::Finished {
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
                current_lobby: 0,
                games_completed: 0,
            }
        });
        let old_lobby_id = user.current_lobby;
        if temporary.has(&DataKey::LobbyInfo(old_lobby_id.clone())) {
            return Err(Error::GuestAlreadyInLobby)
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
        user.current_lobby = req.lobby_id.clone();
        lobby_info.guest_address = Vec::from_array(e, [address]);
        // start game automatically
        lobby_info.phase = Phase::SetupCommit;
        lobby_info.subphase = Subphase::Both;
        // generate pawns
        let mut pawns: Vec<PawnState> = Vec::new(e);
        for packed_tile in lobby_parameters.board.tiles.iter() {
            let tile = Self::unpack_tile(packed_tile);
            if tile.setup == 0 || tile.setup == 1 {
                let pos = tile.pos;
                let team = tile.setup;
                let pawn_id = Self::encode_pawn_id(&pos, &team);
                let pawn_state = PawnState {
                    alive: true,
//                    hidden_rank_hash: HiddenRankHash::from_array(e, &[0u8; 16]),
                    moved: false,
                    moved_scout: false,
                    pawn_id: pawn_id,
                    pos: pos.clone(),
                    rank: Vec::new(e),
                };
                pawns.push_back(pawn_state);
            }
        }
        let game_state = GameState {
            moves: Vec::new(e),
            pawns: pawns,
            rank_roots: Vec::from_array(e, [MerkleHash::from_array(e, &[0u8; 16]), MerkleHash::from_array(e, &[0u8; 16]),]),
            turn: 0,
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
        // validate the proof
        let mut setup_valid = true;
        let pawns_map = Self::create_pawns_map(e, &game_state);
        for commit in req.setup.setup_commits.iter() {
            let pawn = match pawns_map.get(commit.pawn_id) {
                Some(pawn) => pawn,
                None => {
                    setup_valid = false;
                    break;
                }
            };
            // pawn id ownership check
            let (_, x_index) = Self::decode_pawn_id(&commit.pawn_id);
            if x_index != u_index {
                setup_valid = false;
                break;
            }
        }
        game_state.rank_roots.set(u_index, req.rank_commitment_root);
        if !setup_valid {
            // immediately abort the game
            debug_log!(e, "prove_setup: Invalid setup! Setting phase to Aborted");
            lobby_info.phase = Phase::Aborted;
            lobby_info.subphase = Self::opponent_subphase_from_player_index(&u_index);
            temporary.set(&lobby_info_key, &lobby_info);
            return Ok(())
        }
        // for commit in req.setup.setup_commits.iter() {
        //     let (pawn_index, mut pawn_state) = pawns_map.get_unchecked(commit.pawn_id.clone());
        //     // pawn_state.hidden_rank_hash = commit.hidden_rank_hash.clone();
        //     game_state.pawns.set(pawn_index, pawn_state);
        // }
        if next_subphase == Subphase::None {
            lobby_info.phase = Phase::MoveCommit;
            lobby_info.subphase = Subphase::Both;
            game_state.moves = Self::create_empty_moves(e);
        }
        else {
            lobby_info.subphase = next_subphase;
        }

        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&game_state_key, &game_state);
        Ok(())
    }

    pub fn commit_move(e: &Env, address: Address, req: CommitMoveReq) -> Result<LobbyInfo, Error> {
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
        u_move.move_hash = req.move_hash.clone();
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
        Ok(lobby_info)
    }

    pub fn commit_move_and_prove_move(e: &Env, address: Address, req: CommitMoveReq, req2: ProveMoveReq) -> Result<LobbyInfo, Error> {
        Self::commit_move(e, address.clone(), req);
        Self::prove_move_internal(e, address, req2)
    }

    pub fn prove_move(e: &Env, address: Address, req: ProveMoveReq) -> Result<LobbyInfo, Error> {
        address.require_auth();
        Self::prove_move_internal(e, address, req)
    }

    pub(crate) fn prove_move_internal(e: &Env, address: Address, req: ProveMoveReq) -> Result<LobbyInfo, Error> {
        let temporary = e.storage().temporary();
        let (lobby_info, game_state, lobby_parameters, lobby_info_key, game_state_key, _) = Self::get_lobby_data(e, &req.lobby_id, true, true, true)?;
        let mut lobby_info = lobby_info.unwrap();
        let mut game_state = game_state.unwrap();
        let lobby_parameters = lobby_parameters.unwrap();
        let pawn_indexes = Self::create_pawn_indexes(e, &game_state.pawns);
        if lobby_info.phase != Phase::MoveProve {
            debug_log!(e, "prove_move: WrongPhase - expected MoveProve, got {:?}", lobby_info.phase);
            return Err(Error::WrongPhase)
        }

        let u_index = Self::get_player_index(&address, &lobby_info)?;
        let o_index = Self::get_opponent_index(&address, &lobby_info)?;
        // validate and update user move
        {
            let mut u_move = game_state.moves.get_unchecked(u_index);
            let serialized_move_proof = req.move_proof.clone().to_xdr(e);
            let full_hash = e.crypto().sha256(&serialized_move_proof).to_bytes().to_array();
            let submitted_hash = HiddenMoveHash::from_array(e, &full_hash[0..16].try_into().unwrap());
            if u_move.move_hash != submitted_hash {
                return Err(Error::HiddenMoveHashFail)
            }
            let u_move_valid = Self::validate_move_proof(e, &req.move_proof, &u_index, &game_state, &lobby_parameters);
            if !u_move_valid {
                // immediately abort the game
                debug_log!(e, "prove_move: Invalid move! Setting phase to Aborted");
                lobby_info.phase = Phase::Aborted;
                lobby_info.subphase = Self::opponent_subphase_from_player_index(&u_index);
                temporary.set(&lobby_info_key, &lobby_info);
                return Ok(lobby_info)
            }
            u_move.move_proof = Vec::from_array(e, [req.move_proof.clone()]);
            game_state.moves.set(u_index, u_move);
        }

        let next_subphase = Self::next_subphase(&lobby_info.subphase, &u_index)?;
        if next_subphase == Subphase::None {
            // update pawns
            {
                let u_move_proof = game_state.moves.get_unchecked(u_index).move_proof.get_unchecked(0);
                let o_move_proof = game_state.moves.get_unchecked(o_index).move_proof.get_unchecked(0);
                let u_pawn_index = pawn_indexes.get_unchecked(u_move_proof.pawn_id.clone());
                let o_pawn_index = pawn_indexes.get_unchecked(o_move_proof.pawn_id.clone());
                let mut u_pawn = game_state.pawns.get_unchecked(u_pawn_index);
                let mut o_pawn = game_state.pawns.get_unchecked(o_pawn_index);
                Self::apply_move_to_pawn(e, &u_move_proof, &mut u_pawn);
                Self::apply_move_to_pawn(e, &o_move_proof, &mut o_pawn);
                game_state.pawns.set(u_pawn_index, u_pawn);
                game_state.pawns.set(o_pawn_index, o_pawn);
            }
            // set needed rank proofs
            {
                let collision_detection = Self::detect_collisions(e, &game_state, &pawn_indexes, u_index, o_index);
                let (u_proof_list, o_proof_list) = Self::get_needed_rank_proofs(e, &collision_detection, &game_state, &pawn_indexes);
                let mut u_move = game_state.moves.get_unchecked(u_index);
                let mut o_move = game_state.moves.get_unchecked(o_index);
                u_move.needed_rank_proofs = u_proof_list.clone();
                o_move.needed_rank_proofs = o_proof_list.clone();
                game_state.moves.set(u_index, u_move);
                game_state.moves.set(o_index, o_move);
            }
            // check if rank proofs are needed
            match (game_state.moves.get_unchecked(u_index).needed_rank_proofs.is_empty(), game_state.moves.get_unchecked(o_index).needed_rank_proofs.is_empty()) {
                (true, true) => {
                    debug_log!(e, "prove_move: No rank proofs needed, using shared resolution function");
                    Self::set_history(e, &req.lobby_id, &game_state)?;
                    Self::complete_move_resolution(e, &mut game_state);
                    if let Some(winner) = Self::check_game_over(e, &game_state, &lobby_parameters)? {
                        lobby_info.phase = Phase::Finished;
                        lobby_info.subphase = winner.clone();
                        temporary.set(&lobby_info_key, &lobby_info);
                        debug_log!(e, "prove_move: Game over, winner={:?}", winner);
                    }
                    else {
                        lobby_info.phase = Phase::MoveCommit;
                        lobby_info.subphase = Subphase::Both;
                        game_state.turn += 1;
                        debug_log!(e, "prove_move: Transitioned to MoveCommit/Both for next turn");
                    }
                }
                (true, false) => {
                    debug_log!(e, "prove_move: Opponent needs rank proofs, transitioning to RankProve/Opponent");
                    lobby_info.phase = Phase::RankProve;
                    lobby_info.subphase = Self::opponent_subphase_from_player_index(&u_index);
                }
                (false, true) => {
                    debug_log!(e, "prove_move: User needs rank proofs, transitioning to RankProve/User");
                    lobby_info.phase = Phase::RankProve;
                    lobby_info.subphase = Self::user_subphase_from_player_index(&u_index);
                }
                (false, false) => {
                    debug_log!(e, "prove_move: Both need rank proofs, transitioning to RankProve/Both");
                    lobby_info.phase = Phase::RankProve;
                    lobby_info.subphase = Subphase::Both;
                }
            }
        } else {
            lobby_info.subphase = next_subphase;
        }

        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&game_state_key, &game_state);
        debug_log!(e, "prove_move: Completed successfully. Final phase={:?}, subphase={:?}", lobby_info.phase, lobby_info.subphase);
        Ok(lobby_info)
    }


    pub fn prove_move_and_prove_rank(e: &Env, address: Address, req: ProveMoveReq, req2: ProveRankReq) -> Result<LobbyInfo, Error> {
        address.require_auth();
        Self::prove_move_internal(e, address.clone(), req);
        Self::prove_rank_internal(e, address, req2)
    }

    pub fn prove_rank_test(e: &Env, address: Address, req: ProveRankReq) -> Result<bool, Error> {
        let (lobby_info, game_state, lobby_parameters, lobby_info_key, game_state_key, _) = Self::get_lobby_data(e, &req.lobby_id, true, true, true)?;
        let lobby_info = lobby_info.unwrap();
        let game_state = game_state.unwrap();
        let u_index = Self::get_player_index(&address, &lobby_info)?;
        let rank_root = game_state.rank_roots.get_unchecked(u_index);
        let valid = Self::validate_rank_proofs(e, &req.hidden_ranks, &req.merkle_proofs, &game_state, &rank_root);
        Ok(valid)
    }

    pub fn prove_rank(e: &Env, address: Address, req: ProveRankReq) -> Result<LobbyInfo, Error> {
        address.require_auth();
        Self::prove_rank_internal(e, address, req)
    }

    pub fn prove_rank_internal(e: &Env, address: Address, req: ProveRankReq) -> Result<LobbyInfo, Error> {
        debug_log!(e, "=== prove_rank_internal START ===");
        debug_log!(e, "Address: {:?}", address);
        debug_log!(e, "Lobby ID: {}", req.lobby_id);
        debug_log!(e, "Hidden ranks count: {}", req.hidden_ranks.len());
        debug_log!(e, "Merkle proofs count: {}", req.merkle_proofs.len());
        
        for (i, hidden_rank) in req.hidden_ranks.iter().enumerate() {
            debug_log!(e, "Rank request {}: pawn_id={}, rank={}, salt={}", 
                      i as u32, hidden_rank.pawn_id, hidden_rank.rank, hidden_rank.salt);
        }
        
        let temporary = e.storage().temporary();
        let (lobby_info, game_state, lobby_parameters, lobby_info_key, game_state_key, _) = Self::get_lobby_data(e, &req.lobby_id, true, true, true)?;
        let mut lobby_info = lobby_info.unwrap();
        let mut game_state = game_state.unwrap();
        let lobby_parameters = lobby_parameters.unwrap();
        let u_index = Self::get_player_index(&address, &lobby_info)?;
        
        debug_log!(e, "Player index: {}", u_index);
        debug_log!(e, "Current phase: {:?}", lobby_info.phase);
        debug_log!(e, "Current subphase: {:?}", lobby_info.subphase);
        
        if lobby_info.phase != Phase::RankProve {
            debug_log!(e, "FAIL: Wrong phase - expected RankProve, got {:?}", lobby_info.phase);
            return Err(Error::WrongPhase)
        }

        let u_move = game_state.moves.get_unchecked(u_index);
        debug_log!(e, "Needed rank proofs: {}", u_move.needed_rank_proofs.len());
        
        for (i, needed_pawn_id) in u_move.needed_rank_proofs.iter().enumerate() {
            debug_log!(e, "Needed rank proof {}: pawn_id={}", i as u32, needed_pawn_id);
        }
        
        if u_move.needed_rank_proofs.is_empty() {
            debug_log!(e, "FAIL: No rank proofs needed");
            return Err(Error::NoRankProofsNeeded)
        }
        if u_move.needed_rank_proofs.len() != req.hidden_ranks.len() {
            debug_log!(e, "FAIL: Mismatch - needed {} proofs, got {} hidden ranks", 
                      u_move.needed_rank_proofs.len(), req.hidden_ranks.len());
            return Err(Error::InvalidArgs)
        }

        let pawn_indexes = Self::create_pawn_indexes(e, &game_state.pawns);
        let rank_root = game_state.rank_roots.get_unchecked(u_index);
        debug_log!(e, "Using rank root: {:?}", rank_root.to_array());
        
        if !Self::validate_rank_proofs(e, &req.hidden_ranks, &req.merkle_proofs, &game_state, &rank_root) {
            // abort the game
            debug_log!(e, "prove_rank: Invalid rank proof! Setting phase to Aborted");
            lobby_info.phase = Phase::Aborted;
            lobby_info.subphase = Self::opponent_subphase_from_player_index(&u_index);
            temporary.set(&lobby_info_key, &lobby_info);
            return Ok(lobby_info)
        }
        debug_log!(e, "SUCCESS: Rank proofs validated, updating pawn ranks");
        for hidden_rank in req.hidden_ranks.iter() {
            let pawn_index = pawn_indexes.get_unchecked(hidden_rank.pawn_id.clone());
            let mut pawn = game_state.pawns.get_unchecked(pawn_index);
            debug_log!(e, "Setting pawn {} rank to {}", hidden_rank.pawn_id, hidden_rank.rank);
            pawn.rank = Vec::from_array(e, [hidden_rank.rank.clone()]);
            game_state.pawns.set(pawn_index, pawn);
        }
        // clear needed_rank_proofs
        let mut u_move = game_state.moves.get_unchecked(u_index);
        u_move.needed_rank_proofs = Vec::new(e);
        game_state.moves.set(u_index, u_move);
        debug_log!(e, "Cleared needed rank proofs for player {}", u_index);

        let next_subphase = Self::next_subphase(&lobby_info.subphase, &u_index)?;
        debug_log!(e, "Next subphase: {:?}", next_subphase);
        
        if next_subphase == Subphase::None {
            // Both players have acted, check if we can transition to next phase
            debug_log!(e, "Both players completed rank proofs, resolving moves");
            Self::set_history(e, &req.lobby_id, &game_state)?;
            Self::complete_move_resolution(e, &mut game_state);
            if let Some(winner) = Self::check_game_over(e, &game_state, &lobby_parameters)? {
                lobby_info.phase = Phase::Finished;
                lobby_info.subphase = winner.clone();
                temporary.set(&lobby_info_key, &lobby_info);
                debug_log!(e, "prove_rank: Game over, winner={:?}", winner);
            }
            else {
                // Transition to next turn
                lobby_info.phase = Phase::MoveCommit;
                lobby_info.subphase = Subphase::Both;
                game_state.turn += 1;
                debug_log!(e, "prove_rank: Transitioned to MoveCommit/Both for next turn");
            }
        } else {
            // Standard case: advance to next player's turn
            debug_log!(e, "Advanced to next player's turn: {:?}", next_subphase);
            lobby_info.subphase = next_subphase;
        }
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&game_state_key, &game_state);
        debug_log!(e, "=== prove_rank_internal END: SUCCESS ===");
        Ok(lobby_info)
    }
    // region helper functions

    pub(crate) fn create_pawns_map(e: &Env, game_state: &GameState) -> Map<PawnId, (u32, PawnState)> {
        // we never use this to mutate
        let mut pawns_map: Map<PawnId, (u32, PawnState)> = Map::new(e);
        for tuple in game_state.pawns.iter().enumerate() {
            pawns_map.set(tuple.1.pawn_id.clone(), (tuple.0 as u32, tuple.1));
        }
        pawns_map
    }


    pub(crate) fn create_empty_moves(e: &Env) -> Vec<UserMove> {
        Vec::from_array(e, [
            UserMove {
                move_hash: HiddenRankHash::from_array(e, &[0u8; 16]),
                move_proof: Vec::new(e),
                needed_rank_proofs: Vec::new(e),
            },
            UserMove {
                move_hash: HiddenRankHash::from_array(e, &[0u8; 16]),
                move_proof: Vec::new(e),
                needed_rank_proofs: Vec::new(e),
            },
        ])
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
    pub(crate) fn is_scout_move(hidden_move: &HiddenMove) -> bool {
        // TODO: something seems weird about this scout_move is usually wrong
        let dx = hidden_move.target_pos.x - hidden_move.start_pos.x;
        let dy = hidden_move.target_pos.y - hidden_move.start_pos.y;
        if dx.abs() > 1 || dy.abs() > 1 {
            return false
        }
        return true
    }

    pub(crate) fn validate_rank_proofs(e: &Env, hidden_ranks: &Vec<HiddenRank>, merkle_proofs: &Vec<MerkleProof>, game_state: &GameState, root: &MerkleHash) -> bool {
        debug_log!(e, "=== validate_rank_proofs START ===");
        debug_log!(e, "Number of hidden ranks: {}", hidden_ranks.len());
        debug_log!(e, "Number of merkle proofs: {}", merkle_proofs.len());
        debug_log!(e, "Root hash: {:?}", root.to_array());
        
        let mut valid_rank_proof = true;
        let pawns_map = Self::create_pawns_map(e, game_state);
        
        // Check that we have the same number of hidden ranks and merkle proofs
        if hidden_ranks.len() != merkle_proofs.len() {
            debug_log!(e, "FAIL: Mismatch in number of hidden ranks ({}) vs merkle proofs ({})", 
                      hidden_ranks.len(), merkle_proofs.len());
            return false;
        }
        
        for (i, hidden_rank) in hidden_ranks.iter().enumerate() {
            debug_log!(e, "--- Validating rank proof {} ---", i as u32);
            debug_log!(e, "Pawn ID: {}, Rank: {}, Salt: {}", 
                      hidden_rank.pawn_id, hidden_rank.rank, hidden_rank.salt);
            
            let serialized_hidden_rank = hidden_rank.clone().to_xdr(e);
            let full_hash = e.crypto().sha256(&serialized_hidden_rank).to_bytes().to_array();
            let rank_hash = HiddenRankHash::from_array(e, &full_hash[0..16].try_into().unwrap());
            debug_log!(e, "Calculated rank hash: {:?}", rank_hash.to_array());

            let merkle_proof = merkle_proofs.get(i as u32).unwrap();
            debug_log!(e, "Merkle proof leaf_index: {}, siblings: {}", 
                      merkle_proof.leaf_index, merkle_proof.siblings.len());
            
            // Log all sibling hashes
            for (j, sibling) in merkle_proof.siblings.iter().enumerate() {
                debug_log!(e, "  Sibling {}: {:?}", j as u32, sibling.to_array());
            }
            
            let is_valid = Self::verify_merkle_proof(e, &rank_hash, &merkle_proof, root);
            debug_log!(e, "Merkle proof verification result: {}", is_valid);

            if !is_valid {
                debug_log!(e, "FAIL: Merkle proof verification failed for pawn {}", hidden_rank.pawn_id);
                valid_rank_proof = false;
            } else {
                debug_log!(e, "PASS: Merkle proof verification succeeded for pawn {}", hidden_rank.pawn_id);
            }

            // Check if pawn exists in game state
            match pawns_map.get(hidden_rank.pawn_id.clone()) {
                Some(_) => {
                    debug_log!(e, "PASS: Pawn {} exists in game state", hidden_rank.pawn_id);
                }
                None => {
                    debug_log!(e, "FAIL: Pawn {} not found in game state", hidden_rank.pawn_id);
                    valid_rank_proof = false;
                }
            }
        }
        
        debug_log!(e, "=== validate_rank_proofs END: {} ===", valid_rank_proof);
        valid_rank_proof
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
        // tile must exist
        let mut tile_exists = false;
        for packed_tile in lobby_parameters.board.tiles.iter() {
            let tile = Self::unpack_tile(packed_tile);
            if tile.pos == move_proof.target_pos {
                tile_exists = true;
                if !tile.passable {
                    debug_log!(e, "validate_move_proof: FAIL - target pos is not walkable");
                    return false;
                }
            }
        }
        if !tile_exists {
            debug_log!(e, "validate_move_proof: FAIL - target pos does not exist on board");
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
        game_state: &GameState,
        pawn_indexes: &Map<PawnId, u32>,
        u_index: u32,
        o_index: u32,
    ) -> CollisionDetection {
        let mut has_double_collision = false;
        let mut has_swap_collision = false;
        let mut has_u_collision = false;
        let mut has_o_collision = false;
        let mut u_collision_target: Option<PawnId> = None;
        let mut o_collision_target: Option<PawnId> = None;
        // Get move proofs from game state
        let u_move = game_state.moves.get_unchecked(u_index);
        let o_move = game_state.moves.get_unchecked(o_index);
        let u_move_proof = u_move.move_proof.get_unchecked(0);
        let o_move_proof = o_move.move_proof.get_unchecked(0);
        
        let u_pawn_id = u_move_proof.pawn_id;
        let o_pawn_id = o_move_proof.pawn_id;
        let u_start_pos = u_move_proof.start_pos;
        let o_start_pos = o_move_proof.start_pos;
        let u_target_pos = u_move_proof.target_pos;
        let o_target_pos = o_move_proof.target_pos;
        
        let u_pawn_index = pawn_indexes.get_unchecked(u_pawn_id);
        let o_pawn_index = pawn_indexes.get_unchecked(o_pawn_id);
        let u_pawn = game_state.pawns.get_unchecked(u_pawn_index);
        let o_pawn = game_state.pawns.get_unchecked(o_pawn_index);  
        
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
            for x_pawn in game_state.pawns.iter() {
                if u_pawn.pawn_id == x_pawn.pawn_id || o_pawn.pawn_id == x_pawn.pawn_id || !x_pawn.alive {
                    continue;
                }
                if u_pawn.pos == x_pawn.pos {
                    has_u_collision = true;
                    u_collision_target = Some(x_pawn.pawn_id.clone());
                    debug_log!(e, "Detected user collision between {} and {}", u_pawn.pawn_id, x_pawn.pawn_id);
                }
                if o_pawn.pos == x_pawn.pos {
                    has_o_collision = true;
                    o_collision_target = Some(x_pawn.pawn_id.clone());
                    debug_log!(e, "Detected opponent collision between {} and {}", o_pawn.pawn_id, x_pawn.pawn_id);
                }
                if has_u_collision && has_o_collision {
                    break;
                }
            }
        }
        
        debug_log!(e, "collision detection: double: {:?}, swap: {:?}, u_collision: {:?}, o_collision: {:?}", 
        has_double_collision, has_swap_collision, has_u_collision, has_o_collision);
        
        // Only set pawn IDs if they're involved in collisions
        let u_pawn_involved = has_double_collision || has_swap_collision || has_u_collision;
        let o_pawn_involved = has_double_collision || has_swap_collision || has_o_collision;
        
        CollisionDetection {
            has_double_collision,
            has_o_collision,
            has_swap_collision,
            has_u_collision,
            o_pawn_id: if o_pawn_involved { Some(o_pawn_id) } else { None },
            u_pawn_id: if u_pawn_involved { Some(u_pawn_id) } else { None },
            o_collision_target,
            u_collision_target,
        }
    }

         pub(crate) fn get_needed_rank_proofs(e: &Env, collision_detection: &CollisionDetection, game_state: &GameState, pawn_indexes: &Map<PawnId, u32>) -> (Vec<PawnId>, Vec<PawnId>) {
         let mut u_proof_list: Vec<PawnId> = Vec::new(e);
         let mut o_proof_list: Vec<PawnId> = Vec::new(e);
         
         if collision_detection.has_double_collision || collision_detection.has_swap_collision {
             // Both pawns involved in double/swap collision need rank proofs if they don't have ranks
             if let Some(u_pawn_id) = &collision_detection.u_pawn_id {
                 let u_pawn_index = pawn_indexes.get_unchecked(*u_pawn_id);
                 let u_pawn = game_state.pawns.get_unchecked(u_pawn_index);
                 if u_pawn.rank.is_empty() {
                     u_proof_list.push_back(*u_pawn_id);
                 }
             }
             if let Some(o_pawn_id) = &collision_detection.o_pawn_id {
                 let o_pawn_index = pawn_indexes.get_unchecked(*o_pawn_id);
                 let o_pawn = game_state.pawns.get_unchecked(o_pawn_index);
                 if o_pawn.rank.is_empty() {
                     o_proof_list.push_back(*o_pawn_id);
                 }
             }
         }
         else {
             // Handle individual collisions with stationary pawns
             if let Some(u_collision_target) = &collision_detection.u_collision_target {
                 if let Some(u_pawn_id) = &collision_detection.u_pawn_id {
                     let u_pawn_index = pawn_indexes.get_unchecked(*u_pawn_id);
                     let u_pawn = game_state.pawns.get_unchecked(u_pawn_index);
                     if u_pawn.rank.is_empty() {
                         u_proof_list.push_back(*u_pawn_id);
                     }
                 }
                 let ux_pawn_index = pawn_indexes.get_unchecked(*u_collision_target);
                 let ux_pawn = game_state.pawns.get_unchecked(ux_pawn_index);
                 if ux_pawn.rank.is_empty() {
                     o_proof_list.push_back(*u_collision_target);
                 }
             }
             if let Some(o_collision_target) = &collision_detection.o_collision_target {
                 if let Some(o_pawn_id) = &collision_detection.o_pawn_id {
                     let o_pawn_index = pawn_indexes.get_unchecked(*o_pawn_id);
                     let o_pawn = game_state.pawns.get_unchecked(o_pawn_index);
                     if o_pawn.rank.is_empty() {
                         o_proof_list.push_back(*o_pawn_id);
                     }
                 }
                 let ox_pawn_index = pawn_indexes.get_unchecked(*o_collision_target);
                 let ox_pawn = game_state.pawns.get_unchecked(ox_pawn_index);
                 if ox_pawn.rank.is_empty() {
                     u_proof_list.push_back(*o_collision_target);
                 }
             }
         }
         
         (u_proof_list, o_proof_list)
     }
    pub(crate) fn check_game_over(e: &Env, game_state: &GameState, _lobby_parameters: &LobbyParameters) -> Result<Option<Subphase>, Error> {
        // game over check happens at the end of turn resolution
        // let pawn_indexes = Self::create_pawn_indexes(e, &game_state.pawns);
        // case: game ends when a flag is not alive. if both flags are dead, game ends in a draw
        // find flag
        let mut h_flag_alive = true;
        let mut g_flag_alive = true;
        for pawn in game_state.pawns.iter() {
            if !pawn.alive {
                if pawn.rank.get_unchecked(0) == 0 {
                    let (_, team) = Self::decode_pawn_id(&pawn.pawn_id);
                    if team == 0 {
                        h_flag_alive = false;
                    }
                    else {
                        g_flag_alive = false;
                    }
                }
            }
        }
        match (h_flag_alive, g_flag_alive) {
            (true, false) => return Ok(Some(Subphase::Host)),
            (false, true) => return Ok(Some(Subphase::Guest)),
            (false, false) => return Ok(Some(Subphase::None)),
            _ => (),
        }
        // case: game ends if no legal move can be made (not implemented yet)
        Ok(None)
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

    pub(crate) fn apply_move_to_pawn(e: &Env, move_proof: &HiddenMove, pawn: &mut PawnState) -> () {
        if pawn.pos != move_proof.start_pos {
            pawn.moved = true;
        }
        if Self::is_scout_move(move_proof) {
            pawn.moved_scout = true;
        }
        pawn.pos = move_proof.target_pos.clone();
    }

    pub(crate) fn complete_move_resolution(e: &Env, game_state: &mut GameState) -> () {
        let u_index = 0;
        let o_index = 1;
        let u_move = game_state.moves.get_unchecked(u_index);
        let o_move = game_state.moves.get_unchecked(o_index);
        if !u_move.needed_rank_proofs.is_empty() || !o_move.needed_rank_proofs.is_empty() {
            panic!("complete_move_resolution: Needed rank proofs are not empty");
        }
        
        // Now perform collision resolution using the updated game state
        let u_move_proof = u_move.move_proof.get_unchecked(0);
        let o_move_proof = o_move.move_proof.get_unchecked(0);
        
        // Get the updated pawns from game state for collision resolution
        let pawn_indexes = Self::create_pawn_indexes(e, &game_state.pawns);
        
        let u_pawn_index = pawn_indexes.get_unchecked(u_move_proof.pawn_id.clone());
        let o_pawn_index = pawn_indexes.get_unchecked(o_move_proof.pawn_id.clone());
        let mut u_pawn = game_state.pawns.get_unchecked(u_pawn_index);
        let mut o_pawn = game_state.pawns.get_unchecked(o_pawn_index);
        
        // Detect and resolve collisions
        let collision_detection = Self::detect_collisions(e, game_state, &pawn_indexes, u_index, o_index);
        
        if collision_detection.has_double_collision {
            debug_log!(e, "complete_move_resolution: Resolving double collision");
            Self::resolve_collision(&mut u_pawn, &mut o_pawn);
            game_state.pawns.set(u_pawn_index, u_pawn.clone());
            game_state.pawns.set(o_pawn_index, o_pawn.clone());
        }
        else if collision_detection.has_swap_collision {
            debug_log!(e, "complete_move_resolution: Resolving swap collision");
            Self::resolve_collision(&mut u_pawn, &mut o_pawn);
            game_state.pawns.set(u_pawn_index, u_pawn.clone());
            game_state.pawns.set(o_pawn_index, o_pawn.clone());
        }
        else {
            if let Some(u_collision_target) = collision_detection.u_collision_target {
                debug_log!(e, "complete_move_resolution: Resolving user collision with stationary pawn {}", u_collision_target);
                let ux_pawn_index = pawn_indexes.get_unchecked(u_collision_target);
                let mut ux_pawn = game_state.pawns.get_unchecked(ux_pawn_index);
                Self::resolve_collision(&mut u_pawn, &mut ux_pawn);
                game_state.pawns.set(u_pawn_index, u_pawn.clone());
                game_state.pawns.set(ux_pawn_index, ux_pawn.clone());
            }
            
            if let Some(o_collision_target) = collision_detection.o_collision_target {
                debug_log!(e, "complete_move_resolution: Resolving opponent collision with stationary pawn {}", o_collision_target);
                let ox_pawn_index = pawn_indexes.get_unchecked(o_collision_target);
                let mut ox_pawn = game_state.pawns.get_unchecked(ox_pawn_index);
                Self::resolve_collision(&mut o_pawn, &mut ox_pawn);
                game_state.pawns.set(o_pawn_index, o_pawn.clone());
                game_state.pawns.set(ox_pawn_index, ox_pawn.clone());
            }
        }
        // Reset moves for next turn
        game_state.moves = Self::create_empty_moves(e);
        
    }

    // Data Access Helpers

    pub(crate) fn verify_merkle_proof(e: &Env, leaf: &MerkleHash, proof: &MerkleProof, root: &MerkleHash) -> bool {
        debug_log!(e, "=== verify_merkle_proof START ===");
        debug_log!(e, "Leaf hash: {:?}", leaf.to_array());
        debug_log!(e, "Proof leaf_index: {}", proof.leaf_index);
        debug_log!(e, "Proof siblings count: {}", proof.siblings.len());
        debug_log!(e, "Expected root: {:?}", root.to_array());
        
        let mut current_hash = leaf.clone();
        let mut index = proof.leaf_index;
        
        debug_log!(e, "Starting with current_hash: {:?}, index: {}", current_hash.to_array(), index);
        
        for (level, sibling) in proof.siblings.iter().enumerate() {
            debug_log!(e, "--- Level {} ---", level as u32);
            debug_log!(e, "Current hash: {:?}", current_hash.to_array());
            debug_log!(e, "Sibling hash: {:?}", sibling.to_array());
            debug_log!(e, "Current index: {}", index);
            
            // Create a 32-byte array directly for concatenation
            let mut combined_bytes = [0u8; 32];
            
            // Determine order based on index (even = current is left, odd = current is right)
            if index % 2 == 0 {
                // Current hash goes on the left, sibling on the right
                debug_log!(e, "Index {} is even: current (left) + sibling (right)", index);
                combined_bytes[0..16].copy_from_slice(&current_hash.to_array());
                combined_bytes[16..32].copy_from_slice(&sibling.to_array());
            } else {
                // Sibling goes on the left, current hash on the right
                debug_log!(e, "Index {} is odd: sibling (left) + current (right)", index);
                combined_bytes[0..16].copy_from_slice(&sibling.to_array());
                combined_bytes[16..32].copy_from_slice(&current_hash.to_array());
            }
            
            debug_log!(e, "Combined bytes: {:?}", combined_bytes);
            
            // Hash the combined bytes
            let parent_full = e.crypto().sha256(&Bytes::from_array(e, &combined_bytes));
            
            // Take first 16 bytes as the new current hash
            let parent_bytes = parent_full.to_array();
            current_hash = MerkleHash::from_array(e, &parent_bytes[0..16].try_into().unwrap());
            
            debug_log!(e, "New current hash: {:?}", current_hash.to_array());
            
            // Move up the tree
            index = index / 2;
            debug_log!(e, "New index: {}", index);
        }
        
        debug_log!(e, "Final computed hash: {:?}", current_hash.to_array());
        debug_log!(e, "Expected root hash: {:?}", root.to_array());
        
        let result = current_hash == *root;
        debug_log!(e, "=== verify_merkle_proof END: {} ===", result);
        
        result
    }

    pub(crate) fn set_history(e: &Env, lobby_id: &LobbyId, game_state: &GameState) -> Result<(), Error> {
        let temporary = e.storage().temporary();
        let history_key = DataKey::History(lobby_id.clone());
        let mut history: History = match temporary.get(&history_key) {
            Some(h) => h,
            None => return Err(Error::LobbyNotFound),
        };
        history.host_moves.push_back(game_state.moves.get_unchecked(0).move_proof.get_unchecked(0).clone());
        history.guest_moves.push_back(game_state.moves.get_unchecked(1).move_proof.get_unchecked(0).clone());
        temporary.set(&history_key, &history);
        Ok(())
    }
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

    pub(crate) fn create_pawn_indexes(e: &Env, pawns: &Vec<PawnState>) -> Map<PawnId, u32> {
        let mut indexes = Map::new(e);
        for (index, pawn) in pawns.iter().enumerate() {
            indexes.set(pawn.pawn_id, index as u32);
        }
        indexes
    }

    // Tile Packing Functions
    pub(crate) fn pack_tile(tile: &Tile) -> PackedTile {
        let mut packed: u32 = 0;
        // Pack passable (1 bit) - bit 0
        if tile.passable {
            packed |= 1;
        }
        // Pack x coordinate (9 bits) - bits 1-9
        let x_val = (tile.pos.x as u32) & 0x1FF; // Mask to 9 bits
        packed |= x_val << 1;
        // Pack y coordinate (9 bits) - bits 10-18  
        let y_val = (tile.pos.y as u32) & 0x1FF; // Mask to 9 bits
        packed |= y_val << 10;
        // Pack setup (3 bits) - bits 19-21
        let setup_val = tile.setup & 0x7; // Mask to 3 bits
        packed |= setup_val << 19;
        // Pack setup_zone (3 bits) - bits 22-24
        let setup_zone_val = tile.setup_zone & 0x7; // Mask to 3 bits
        packed |= setup_zone_val << 22;
        packed
    }

    pub(crate) fn unpack_tile(packed: PackedTile) -> Tile {
        // Extract passable (bit 0)
        let passable = (packed & 1) != 0;
        // Extract x coordinate (bits 1-9)
        let x = ((packed >> 1) & 0x1FF) as i32;
        // Extract y coordinate (bits 10-18)
        let y = ((packed >> 10) & 0x1FF) as i32;
        // Extract setup (bits 19-21)
        let setup = (packed >> 19) & 0x7;
        // Extract setup_zone (bits 22-24)
        let setup_zone = (packed >> 22) & 0x7;
        Tile {
            passable,
            pos: Pos { x, y },
            setup,
            setup_zone,
        }
    }

    pub(crate) fn validate_packed_tile(packed: PackedTile) -> bool {
        // Check if reserved bits (25-31) are zero
        if (packed >> 25) != 0 {
            return false;
        }
        
        // Extract and validate setup field (bits 19-21)
        let setup = (packed >> 19) & 0x7;
        if setup > 2 {
            return false;
        }
        
        // Extract and validate setup_zone field (bits 22-24)
        let setup_zone = (packed >> 22) & 0x7;
        if setup_zone > 4 {
            return false;
        }
        
        // Extract coordinates and check reasonable bounds
        let x = ((packed >> 1) & 0x1FF) as i32;
        let y = ((packed >> 10) & 0x1FF) as i32;
        
        // Check if coordinates are within reasonable range (-256 to 255)
        if x > 255 || y > 255 {
            return false;
        }
        
        true
    }

    pub(crate) fn validate_tile_consistency(tile: &Tile) -> bool {
        // Validate setup field
        if tile.setup > 2 {
            return false;
        }
        
        // Validate setup_zone field
        if tile.setup_zone > 4 {
            return false;
        }
        
        // Validate that impassable tiles are only setup type 2
        if !tile.passable && tile.setup != 2 {
            return false;
        }
        
        // Validate coordinates are within signed 9-bit range (-256 to 255)
        if tile.pos.x < -256 || tile.pos.x > 255 || tile.pos.y < -256 || tile.pos.y > 255 {
            return false;
        }
        
        true
    }

    pub(crate) fn test_tile_packing_roundtrip(tile: &Tile) -> bool {
        if !Self::validate_tile_consistency(tile) {
            return false;
        }
        
        let packed = Self::pack_tile(tile);
        if !Self::validate_packed_tile(packed) {
            return false;
        }
        
        let unpacked = Self::unpack_tile(packed);
        *tile == unpacked
    }
    // endregion

}
// endregion


mod test_utils; // test utilities
mod tests; // organized test modules