#![no_std]
use soroban_sdk::{*};
use soroban_sdk::xdr::*;
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
pub type PackedPawn = u32;
// endregion
// region enums & errors
#[contracterror]
#[derive(Copy, Clone, Debug, Eq, PartialEq, PartialOrd, Ord)]
pub enum Error {
    // Category 1: Malformed request - client should fix and retry
    InvalidArgs = 1,
    HashFail = 2,
    // Category 2: Timing/state - client should check state
    WrongPhase = 3,
    WrongSubphase = 4,
    // Category 3: Resource not found - permanent failure
    NotFound = 5,
    // Category 4: Authorization - user not allowed
    Unauthorized = 6,
    // Category 5: Action conflicts
    AlreadyExists = 7,
    LobbyNotJoinable = 8,
    // Category 6: Security mode
    WrongSecurityMode = 0,
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
#[derive(Copy, Clone, Debug, Eq, PartialEq)]
pub enum UserIndex {
    Host = 0,
    Guest = 1,
}
impl UserIndex {
    pub fn u32(self) -> u32 {
        self as u32
    }
    pub fn from_u32(integer: u32) -> UserIndex {
        if integer == 0 {
            return UserIndex::Host;
        }
        if integer == 1 {
            return UserIndex::Guest;
        }
        panic!()
    }
}
// endregion
// region structs
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct MerkleProof {
    pub leaf_index: u32,
    pub siblings: Vec<MerkleHash>,
}
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
    pub passable: bool,
    pub pos: Pos,
    pub setup: u32,         // user_index of the user that can use this for setup
    pub setup_zone: u32,    // used by client for auto setup stuff
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Board {
    pub hex: bool,
    pub name: String,
    pub size: Pos, // max supported size (16, 16)
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
pub struct SetupCommit {
    pub hidden_rank_hash: HiddenRankHash,
    pub pawn_id: PawnId,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct HiddenRank {
    pub pawn_id: PawnId,
    pub rank: Rank,
    pub salt: u64,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PawnState {
    pub alive: bool,
    pub moved: bool,
    pub moved_scout: bool,
    pub pawn_id: PawnId,
    pub pos: Pos,
    pub rank: Vec<Rank>,
    pub zz_revealed: bool,
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
    pub pawns: Vec<PackedPawn>,
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
pub struct CollisionDetection {
    pub has_double_collision: bool,
    pub has_g_collision: bool,
    pub has_swap_collision: bool,
    pub has_h_collision: bool,
    pub g_collision_target: Option<PawnId>,
    pub g_pawn_id: Option<PawnId>,
    pub h_collision_target: Option<PawnId>,
    pub h_pawn_id: Option<PawnId>,
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
    pub zz_hidden_ranks: Vec<HiddenRank>,
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
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct RedeemWinReq {
    pub lobby_id: LobbyId,
}
#[contracttype]#[derive(Clone, Debug, Eq, PartialEq)]
pub struct SurrenderReq {
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
    History(LobbyId),
}
// endregion
// region contract
#[contract]
pub struct Contract;

#[contractimpl]
impl Contract {
    /// # Parameters
    /// - `address`: Creator's address (becomes host)
    /// - `req.lobby_id`: Unique lobby identifier
    /// - `req.parameters`: `LobbyParameters` with board, rank limits, game modes
    /// # Requirements
    /// - User must not be in an unexpired lobby
    /// # State Changes
    /// - Creates `LobbyInfo` in `Phase::Lobby`
    /// - Stores `LobbyParameters`
    /// - Updates user's `current_lobby`
    /// - **Result**: `Phase::Lobby`, `Subphase::Guest` (waiting for guest to call `join_lobby`)
    /// # Errors
    /// - `AlreadyExists`: Lobby ID taken
    /// - `InvalidArgs`: Invalid board or parameters
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
        let lobby_info_key = DataKey::LobbyInfo(req.lobby_id);
        if temporary.has(&lobby_info_key) {
            return Err(Error::AlreadyExists)
        }
        let lobby_parameters_key = DataKey::LobbyParameters(req.lobby_id);
        // light validation on boards just to make sure they make sense
        if !Self::validate_board(e, &req.parameters) {
            return Err(Error::InvalidArgs)
        }
        let mut parameters_invalid = false;
        // parameter validation
        for (i, max) in req.parameters.max_ranks.iter().enumerate() {
            let index = i as u32;
            // no throne
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
            return Err(Error::InvalidArgs)
        }
        // update
        let lobby_info = LobbyInfo {
            index: req.lobby_id,
            guest_address: Vec::new(e),
            host_address: Vec::from_array(e, [address.clone()]),
            phase: Phase::Lobby,
            subphase: Subphase::Guest,
        };
        user.current_lobby = req.lobby_id;
        // save
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&lobby_parameters_key, &req.parameters);
        persistent.set(&user_key, &user);
        Ok(())
    }
    /// Exit current lobby, ending the lobby if a match is in progress.
    /// # Requirements
    /// - User must be in a lobby
    /// # State Changes
    /// - Always clears user's `current_lobby`
    /// - **Result**:
    ///   - During Lobby phase: `Phase::Aborted`, `Subphase::None`, kicks both players out
    ///   - During active game phases: `Phase::Finished`, opponent wins
    ///   - During Finished/Aborted: No game state changes
    /// # Errors
    /// - `NotFound`: User not in lobby
    pub fn leave_lobby(e: &Env, address: Address) -> Result<(), Error> {
        address.require_auth();
        let persistent = e.storage().persistent();
        let temporary = e.storage().temporary();
        let user_key = DataKey::User(address.clone());
        let mut user: User = match persistent.get(&user_key) {
            Some(user) => user,
            None => return Err(Error::NotFound),
        };
        if user.current_lobby == 0 {
            return Err(Error::NotFound)
        }
        let lobby_id = user.current_lobby;
        let mut lobby_info: LobbyInfo = temporary.get(&DataKey::LobbyInfo(lobby_id)).unwrap();
        let original_phase = lobby_info.phase.clone();
        let user_index = Self::get_player_index(&address, &lobby_info);
        user.current_lobby = 0;
        
        // Always clear the leaving player's address from lobby
        if lobby_info.host_address.contains(&address) {
            lobby_info.host_address = Vec::new(e);
        } else if lobby_info.guest_address.contains(&address) {
            lobby_info.guest_address = Vec::new(e);
        }
        
        // Handle different phases
        match original_phase {
            Phase::Lobby => {
                // In lobby phase: any user leaving aborts the game
                lobby_info.phase = Phase::Aborted;
                lobby_info.subphase = Subphase::None;
                // Clear the other player too (kick everyone out)
                if lobby_info.host_address.len() > 0 {
                    lobby_info.host_address = Vec::new(e);
                }
                if lobby_info.guest_address.len() > 0 {
                    lobby_info.guest_address = Vec::new(e);
                }
            },
            Phase::SetupCommit | Phase::MoveCommit | Phase::MoveProve | Phase::RankProve => {
                // Game in progress: leaving player loses, opponent wins
                lobby_info.phase = Phase::Finished;
                lobby_info.subphase = Self::opponent_subphase_from_player_index(user_index);
            },
            Phase::Finished | Phase::Aborted => {
                // Game already ended: just remove the user, don't change game state
                // Address was already cleared above
            }
        }
        // save user (always clear their current_lobby)
        persistent.set(&user_key, &user);
        // always save lobby_info since we always clear the address
        temporary.set(&DataKey::LobbyInfo(lobby_id), &lobby_info);
        Ok(())
    }
    /// Join as guest, automatically starting the game.
    /// # Parameters
    /// - `address`: Joining player (becomes guest)
    /// - `req.lobby_id`: Target lobby
    /// # Requirements
    /// - Phase is Lobby and subphase is Guest (indicates lobby is waiting for a guest)
    /// - User must not be in an unexpired lobby
    /// # State Changes
    /// - Sets guest address
    /// - Initializes `GameState` with pawns on setup tiles
    /// - **Result**: `Phase::SetupCommit`, `Subphase::Both` (both players must call `commit_setup`)
    /// # Errors
    /// - `Unauthorized`: Already in a lobby
    /// - `LobbyNotJoinable`: Not joinable state
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
        if temporary.has(&DataKey::LobbyInfo(old_lobby_id)) {
            return Err(Error::Unauthorized)
        }
        let mut lobby_info: LobbyInfo = temporary.get(&DataKey::LobbyInfo(req.lobby_id)).unwrap();
        let lobby_parameters: LobbyParameters = temporary.get(&DataKey::LobbyParameters(req.lobby_id)).unwrap();
        // Simple validation: lobby must be in Lobby phase with Guest subphase
        if lobby_info.phase != Phase::Lobby || lobby_info.subphase != Subphase::Guest {
            return Err(Error::LobbyNotJoinable)
        }
        // update
        user.current_lobby = req.lobby_id;
        lobby_info.guest_address = Vec::from_array(e, [address.clone()]);
        // start game automatically
        lobby_info.phase = Phase::SetupCommit;
        lobby_info.subphase = Subphase::Both;
        // generate pawns
        let mut pawns: Vec<PackedPawn> = Vec::new(e);
        for packed_tile in lobby_parameters.board.tiles.iter() {
            let tile = Self::unpack_tile(packed_tile);
            if tile.setup == 0 || tile.setup == 1 {
                let pawn_state = PawnState {
                    alive: true,
                    moved: false,
                    moved_scout: false,
                    pawn_id: Self::encode_pawn_id(tile.pos, tile.setup),
                    pos: tile.pos,
                    rank: Vec::new(e),
                    zz_revealed: false,
                };
                pawns.push_back(Self::pack_pawn(pawn_state));
            }
        }
        let game_state = GameState {
            moves: Self::create_empty_moves(e),
            pawns: pawns,
            rank_roots: Vec::from_array(e, [MerkleHash::from_array(e, &[0u8; 16]), MerkleHash::from_array(e, &[0u8; 16]),]),
            turn: 0,
        };
        lobby_info.phase = Phase::SetupCommit;
        lobby_info.subphase = Subphase::Both;
        // save
        persistent.set(&user_key, &user);
        temporary.set(&DataKey::GameState(req.lobby_id), &game_state);
        temporary.set(&DataKey::LobbyInfo(req.lobby_id), &lobby_info);
        Ok(())
    }
    /// Submit Merkle root of all owned pawn rank HiddenRanks.
    /// # Parameters
    /// - `req.rank_commitment_root`: Merkle root hash
    /// # Requirements
    /// - Phase is `Phase::SetupCommit` and subphase is user's UserIndex or Both
    /// # State Changes
    /// - Stores rank root for player
    /// - **Result**:
    ///   - If first to commit: `Phase::SetupCommit`, subphase = opponent (waiting for opponent to call `commit_setup`)
    ///   - If both committed: `Phase::MoveCommit`, `Subphase::Both` (both must call `commit_move`)
    /// # Errors
    /// - `WrongPhase`: Not in setup phase
    /// - `WrongSubphase`: Already committed or not your turn
    pub fn commit_setup(e: &Env, address: Address, req: CommitSetupReq) -> Result<(), Error> {
        address.require_auth();
        let temporary = e.storage().temporary();
        let mut lobby_info: LobbyInfo = temporary.get(&DataKey::LobbyInfo(req.lobby_id)).unwrap();
        let lobby_parameters: LobbyParameters = temporary.get(&DataKey::LobbyParameters(req.lobby_id)).unwrap();
        let mut game_state: GameState = temporary.get(&DataKey::GameState(req.lobby_id)).unwrap();
        let u_index = Self::get_player_index(&address, &lobby_info);
        if lobby_info.phase != Phase::SetupCommit {
            return Err(Error::WrongPhase)
        }
        if !lobby_parameters.security_mode {
            // set the provided setup from zz_hidden_ranks
            let mut hidden_rank_map: Map<PawnId, HiddenRank> = Map::new(e);
            for hidden_rank in req.zz_hidden_ranks {
                hidden_rank_map.set(hidden_rank.pawn_id, hidden_rank);
            }
            for (index, packed_pawn) in game_state.pawns.iter().enumerate() {
                let mut pawn = Self::unpack_pawn(e, packed_pawn);
                let (_, owner_index) = Self::decode_pawn_id(pawn.pawn_id);
                if owner_index == u_index {
                    // this will panic on purpose if the user hasn't provided every hidden_rank
                    let hidden_rank = hidden_rank_map.get_unchecked(pawn.pawn_id);
                    pawn.rank = Vec::from_array(e, [hidden_rank.rank]);
                    // update game_state.pawns
                    game_state.pawns.set(index as u32, Self::pack_pawn(pawn));
                }
            }
            let revealed_rank_counts = Self::get_revealed_rank_counts(e, u_index, &game_state);
            for (rank_index, max_rank) in lobby_parameters.max_ranks.iter().enumerate() {
                let revealed_rank_count = revealed_rank_counts[rank_index];
                if revealed_rank_count > max_rank {
                    return Err(Error::InvalidArgs)
                }
            }
        }
        // TODO: reject non empty zz_hidden_ranks when security_mode true
        let next_subphase = Self::next_subphase(&lobby_info.subphase, u_index)?;
        game_state.rank_roots.set(u_index.u32(), req.rank_commitment_root);
        if next_subphase == Subphase::None {
            lobby_info.phase = Phase::MoveCommit;
            lobby_info.subphase = Subphase::Both;
        }
        else {
            lobby_info.subphase = next_subphase;
        }
        temporary.set(&DataKey::LobbyInfo(req.lobby_id), &lobby_info);
        temporary.set(&DataKey::GameState(req.lobby_id), &game_state);
        Ok(())
    }
    /// Submit hashed move for current turn.
    /// # Parameters
    /// - `req.move_hash`: Hash of `HiddenMove`
    /// # Requirements
    /// - Phase is `Phase::MoveCommit` and subphase is user's UserIndex or Both
    /// - security_mode is true (non security_mode lobbies should have clients only call commit_move_and_prove_move)
    /// # State Changes
    /// - Stores move hash
    /// - **Result**:
    ///   - If first to commit: `Phase::MoveCommit`, subphase = opponent (waiting for opponent to call `commit_move`)
    ///   - If both committed: `Phase::MoveProve`, `Subphase::Both` (both must call `prove_move`)
    /// # Errors
    /// - `WrongSubphase`: Not your turn or already committed
    pub fn commit_move(e: &Env, address: Address, req: CommitMoveReq) -> Result<LobbyInfo, Error> {
        address.require_auth();
        let temporary = e.storage().temporary();
        let mut lobby_info: LobbyInfo = temporary.get(&DataKey::LobbyInfo(req.lobby_id)).unwrap();
        let mut game_state: GameState = temporary.get(&DataKey::GameState(req.lobby_id)).unwrap();
        let lobby_parameters: LobbyParameters = temporary.get(&DataKey::LobbyParameters(req.lobby_id)).unwrap();
        if !lobby_parameters.security_mode {
            return Err(Error::WrongSecurityMode)
        }
        Self::commit_move_internal(&address, &req, &mut lobby_info, &mut game_state)?;
        temporary.set(&DataKey::LobbyInfo(req.lobby_id), &lobby_info);
        temporary.set(&DataKey::GameState(req.lobby_id), &game_state);
        Ok(lobby_info)
    }
    pub fn commit_move_and_prove_move(e: &Env, address: Address, req: CommitMoveReq, req2: ProveMoveReq) -> Result<LobbyInfo, Error> {
        address.require_auth();
        let temporary = e.storage().temporary();
        let mut lobby_info: LobbyInfo = temporary.get(&DataKey::LobbyInfo(req.lobby_id)).unwrap();
        let mut game_state: GameState = temporary.get(&DataKey::GameState(req.lobby_id)).unwrap();
        let lobby_parameters: LobbyParameters = temporary.get(&DataKey::LobbyParameters(req.lobby_id)).unwrap();
        Self::commit_move_internal(&address, &req, &mut lobby_info, &mut game_state)?;
        Self::prove_move_internal(e, &address, &req2, &mut lobby_info, &mut game_state, &lobby_parameters)?;
        temporary.set(&DataKey::LobbyInfo(req.lobby_id), &lobby_info);
        temporary.set(&DataKey::GameState(req.lobby_id), &game_state);
        Ok(lobby_info)
    }
    /// Reveal and validate committed move.
    /// # Parameters
    /// - `req.move_proof`: Actual `HiddenMove`
    /// # Requirements
    /// - Current phase is `Phase::ProveMove` and subphase is user's UserIndex or Both
    /// # State Changes
    /// - Validates move
    /// - Invalid move → **Result**: `Phase::Aborted`, subphase = opponent (opponent wins)
    /// - Valid move:
    ///   - If first to prove: `Phase::MoveProve`, subphase = opponent (waiting for opponent to call `prove_move`)
    ///   - If both proved:
    ///     - Collision detected → `Phase::RankProve`, subphase indicates who must call `prove_rank` (Host/Guest/Both)
    ///     - No collision, victory condition met → `Phase::Finished`, subphase = winner (Host/Guest) or None (tie)
    ///     - No collision, game continues → `Phase::MoveCommit`, `Subphase::Both` (both must call `commit_move`)
    /// # Errors
    /// - `HashFail`: Proof doesn't match hash
    /// - `WrongSubphase`: Not your turn or already proved
    pub fn prove_move(e: &Env, address: Address, req: ProveMoveReq) -> Result<LobbyInfo, Error> {
        address.require_auth();
        let temporary = e.storage().temporary();
        let mut lobby_info: LobbyInfo = temporary.get(&DataKey::LobbyInfo(req.lobby_id)).unwrap();
        let mut game_state: GameState = temporary.get(&DataKey::GameState(req.lobby_id)).unwrap();
        let lobby_parameters: LobbyParameters = temporary.get(&DataKey::LobbyParameters(req.lobby_id)).unwrap();
        if !lobby_parameters.security_mode {
            return Err(Error::WrongSecurityMode)
        }
        Self::prove_move_internal(e, &address, &req, &mut lobby_info, &mut game_state, &lobby_parameters)?;
        temporary.set(&DataKey::LobbyInfo(req.lobby_id), &lobby_info);
        temporary.set(&DataKey::GameState(req.lobby_id), &game_state);
        Ok(lobby_info)
    }
    pub fn prove_move_and_prove_rank(e: &Env, address: Address, req: ProveMoveReq, req2: ProveRankReq) -> Result<LobbyInfo, Error> {
        address.require_auth();
        let temporary = e.storage().temporary();
        let mut lobby_info: LobbyInfo = temporary.get(&DataKey::LobbyInfo(req.lobby_id)).unwrap();
        let mut game_state: GameState = temporary.get(&DataKey::GameState(req.lobby_id)).unwrap();
        let lobby_parameters: LobbyParameters = temporary.get(&DataKey::LobbyParameters(req.lobby_id)).unwrap();
        if !lobby_parameters.security_mode {
            return Err(Error::WrongSecurityMode)
        }
        Self::prove_move_internal(e, &address, &req, &mut lobby_info, &mut game_state, &lobby_parameters)?;
        // skip if game was aborted due to an illegal move
        if lobby_info.phase != Phase::Aborted {
            Self::prove_rank_internal(e, &address, &req2, &mut lobby_info, &mut game_state, &lobby_parameters)?;
        }
        temporary.set(&DataKey::LobbyInfo(req.lobby_id), &lobby_info);
        temporary.set(&DataKey::GameState(req.lobby_id), &game_state);
        Ok(lobby_info)
    }
    /// Reveal ranks for collision resolution.
    ///
    /// # Parameters
    /// - `req.hidden_ranks`: Array of rank reveals
    /// - `req.merkle_proofs`: Validation proofs
    ///
    /// # Requirements
    /// - Current phase is `Phase::ProveRank` and subphase is user's UserIndex or Both
    /// - security_mode is true
    /// # State Changes
    /// - Validates rank proofs
    /// - Invalid proof → **Result**: `Phase::Aborted`, subphase = opponent (opponent wins)
    /// - Valid proofs:
    ///   - If more ranks needed: `Phase::RankProve`, subphase = opponent (waiting for opponent to call `prove_rank`)
    ///   - If all ranks revealed:
    ///     - Victory condition met → `Phase::Finished`, subphase = winner (Host/Guest) or None (tie)
    ///     - Game continues → `Phase::MoveCommit`, `Subphase::Both` (both must call `commit_move`)
    /// # Errors
    /// - `WrongSubphase`: Not your turn or already proved
    pub fn prove_rank(e: &Env, address: Address, req: ProveRankReq) -> Result<LobbyInfo, Error> {
        address.require_auth();
        let temporary = e.storage().temporary();
        let mut lobby_info: LobbyInfo = temporary.get(&DataKey::LobbyInfo(req.lobby_id)).unwrap();
        let mut game_state: GameState = temporary.get(&DataKey::GameState(req.lobby_id)).unwrap();
        let lobby_parameters: LobbyParameters = temporary.get(&DataKey::LobbyParameters(req.lobby_id)).unwrap();
        Self::prove_rank_internal(e, &address, &req, &mut lobby_info, &mut game_state, &lobby_parameters)?;
        temporary.set(&DataKey::LobbyInfo(req.lobby_id), &lobby_info);
        temporary.set(&DataKey::GameState(req.lobby_id), &game_state);
        Ok(lobby_info)
    }
    /// Claim victory due to opponent timeout. WIP
    /// # Requirements
    /// - Game is in progress
    /// # State Changes
    /// **Result**: `Phase::Finished`, subphase = caller (caller wins)
    ///
    /// # Errors
    /// - `WrongPhase`: Invalid game state
    /// - `WrongSubphase`: Not opponent's turn (opponent must be required to act)
    pub fn redeem_win(e: &Env, address: Address, req: RedeemWinReq) -> Result<LobbyInfo, Error> {
        let temporary = e.storage().temporary();
        let mut lobby_info: LobbyInfo = temporary.get(&DataKey::LobbyInfo(req.lobby_id)).unwrap();
        if [Phase::Lobby, Phase::SetupCommit, Phase::Finished, Phase::Aborted].contains(&lobby_info.phase) {
            return Err(Error::WrongPhase)
        }
        let u_index = Self::get_player_index(&address, &lobby_info);
        if lobby_info.subphase != Self::opponent_subphase_from_player_index(u_index) {
            return Err(Error::WrongSubphase)
        }
        // TODO: add a time limit check from the last time lobby_info got updated
        lobby_info.phase = Phase::Finished;
        lobby_info.subphase = Self::user_subphase_from_player_index(u_index);
        temporary.set(&DataKey::LobbyInfo(req.lobby_id), &lobby_info);
        Ok(lobby_info)
    }
    // endregion
    // region internal
    pub(crate) fn commit_move_internal(address: &Address, req: &CommitMoveReq, lobby_info: &mut LobbyInfo, game_state: &mut GameState) -> Result<(), Error> {
        let u_index = Self::get_player_index(address, &lobby_info);
        if lobby_info.phase != Phase::MoveCommit {
            return Err(Error::WrongPhase)
        }
        let next_subphase = Self::next_subphase(&lobby_info.subphase, u_index)?;
        let mut u_move = game_state.moves.get_unchecked(u_index.u32());
        // update
        u_move.move_hash = req.move_hash.clone();
        if next_subphase == Subphase::None {
            lobby_info.phase = Phase::MoveProve;
            lobby_info.subphase = Subphase::Both;
        }
        else {
            lobby_info.subphase = next_subphase;
        }
        game_state.moves.set(u_index.u32(), u_move);
        Ok(())
    }
    pub(crate) fn prove_move_internal(e: &Env, address: &Address, req: &ProveMoveReq, lobby_info: &mut LobbyInfo, game_state: &mut GameState, lobby_parameters: &LobbyParameters) -> Result<(), Error> {
        if lobby_parameters.security_mode {
            if lobby_info.phase != Phase::MoveProve {
                return Err(Error::WrongPhase)
            }
        } else {
            if lobby_info.phase != Phase::MoveProve && lobby_info.phase != Phase::MoveCommit {
                return Err(Error::WrongPhase)
            }
        }
        let u_index = Self::get_player_index(address, &lobby_info);
        let o_index = Self::get_opponent_index(address, &lobby_info);
        // validate and update user move
        {
            let mut u_move = game_state.moves.get_unchecked(u_index.u32());
            let serialized_move_proof = req.move_proof.clone().to_xdr(e);
            let full_hash = e.crypto().sha256(&serialized_move_proof).to_bytes().to_array();
            let submitted_hash = HiddenMoveHash::from_array(e, &full_hash[0..16].try_into().unwrap());
            if u_move.move_hash != submitted_hash {
                return Err(Error::HashFail)
            }
            let pawns_map = Self::create_pawns_map(e, &game_state.pawns);
            let u_move_valid = Self::validate_move_proof(&req.move_proof, u_index, &pawns_map, &lobby_parameters);
            if !u_move_valid {
                // immediately abort the game
                lobby_info.phase = Phase::Aborted;
                lobby_info.subphase = Self::opponent_subphase_from_player_index(u_index);
                return Ok(())
            }
            u_move.move_proof = Vec::from_array(e, [req.move_proof.clone()]);
            game_state.moves.set(u_index.u32(), u_move);
        }
        let next_subphase = Self::next_subphase(&lobby_info.subphase, u_index)?;
        if next_subphase == Subphase::None {
            let pawns_map = Self::create_pawns_map(e, &game_state.pawns);
            let (collision_detection, h_needed_rank_proofs, g_needed_rank_proofs) = Self::detect_collisions_with_target_pos(e, game_state, &pawns_map);
            let mut h_move = game_state.moves.get_unchecked(UserIndex::Host.u32());
            let mut g_move = game_state.moves.get_unchecked(UserIndex::Guest.u32());
            h_move.needed_rank_proofs = h_needed_rank_proofs.clone();
            g_move.needed_rank_proofs = g_needed_rank_proofs.clone();
            game_state.moves.set(UserIndex::Host.u32(), h_move);
            game_state.moves.set(UserIndex::Guest.u32(), g_move);
            // check if rank proofs are needed
            match (game_state.moves.get_unchecked(u_index.u32()).needed_rank_proofs.is_empty(), game_state.moves.get_unchecked(o_index.u32()).needed_rank_proofs.is_empty()) {
                (true, true) => {
                    Self::complete_move_resolution(e, game_state, Some(collision_detection), &pawns_map);
                    let winner = Self::check_game_over(e, &game_state, &lobby_parameters);
                    if winner != Subphase::Both {
                        lobby_info.phase = Phase::Finished;
                        lobby_info.subphase = winner;
                    }
                    else {
                        lobby_info.phase = Phase::MoveCommit;
                        lobby_info.subphase = Subphase::Both;
                        game_state.turn += 1;
                    }
                }
                (true, false) => {
                    lobby_info.phase = Phase::RankProve;
                    lobby_info.subphase = Self::opponent_subphase_from_player_index(u_index);
                }
                (false, true) => {
                    lobby_info.phase = Phase::RankProve;
                    lobby_info.subphase = Self::user_subphase_from_player_index(u_index);
                }
                (false, false) => {
                    lobby_info.phase = Phase::RankProve;
                    lobby_info.subphase = Subphase::Both;
                }
            }
        } else {
            lobby_info.subphase = next_subphase;
        }
        Ok(())
    }
    pub(crate) fn prove_rank_internal(e: &Env, address: &Address, req: &ProveRankReq, lobby_info: &mut LobbyInfo, game_state: &mut GameState, lobby_parameters: &LobbyParameters) -> Result<(), Error> {
        let u_index = Self::get_player_index(address, &lobby_info);
        if lobby_info.phase != Phase::RankProve {
            return Err(Error::WrongPhase)
        }
        let u_move = game_state.moves.get_unchecked(u_index.u32());
        if u_move.needed_rank_proofs.is_empty() {
            return Err(Error::InvalidArgs)
        }
        if u_move.needed_rank_proofs.len() != req.hidden_ranks.len() {
            return Err(Error::InvalidArgs)
        }
        {
            let pawns_map = Self::create_pawns_map(e, &game_state.pawns);
            let rank_root = game_state.rank_roots.get_unchecked(u_index.u32());
            if !Self::validate_rank_proofs(e, &req.hidden_ranks, &req.merkle_proofs, &rank_root) {
                // abort the game
                lobby_info.phase = Phase::Aborted;
                lobby_info.subphase = Self::opponent_subphase_from_player_index(u_index);
                return Ok(())
            }
            for hidden_rank in req.hidden_ranks.iter() {
                let (pawn_index, mut pawn) = pawns_map.get_unchecked(hidden_rank.pawn_id);
                pawn.rank = Vec::from_array(e, [hidden_rank.rank.clone()]);
                game_state.pawns.set(pawn_index, Self::pack_pawn(pawn));
            }
        }
        {
            // check to see if user has committed more ranks than allowed
            let revealed_rank_counts = Self::get_revealed_rank_counts(e, u_index, &game_state);
            for (rank_index, max_rank) in lobby_parameters.max_ranks.iter().enumerate() {
                let revealed_rank_count = revealed_rank_counts[rank_index];
                if revealed_rank_count > max_rank {
                    // abort the game
                    lobby_info.phase = Phase::Aborted;
                    lobby_info.subphase = Self::opponent_subphase_from_player_index(u_index);
                    return Ok(())
                }
            }
        }
        // clear needed_rank_proofs
        {
            let mut u_move = game_state.moves.get_unchecked(u_index.u32());
            u_move.needed_rank_proofs = Vec::new(e);
            game_state.moves.set(u_index.u32(), u_move);
        }
        let next_subphase = Self::next_subphase(&lobby_info.subphase, u_index)?;
        if next_subphase == Subphase::None {
            let pawns_map = Self::create_pawns_map(e, &game_state.pawns);
            Self::complete_move_resolution(e, game_state, None, &pawns_map);
            let winner = Self::check_game_over(e, &game_state, &lobby_parameters);
            if winner != Subphase::Both {
                lobby_info.phase = Phase::Finished;
                lobby_info.subphase = winner;
            }
            else {
                lobby_info.phase = Phase::MoveCommit;
                lobby_info.subphase = Subphase::Both;
                game_state.turn += 1;
            }
        } else {
            // Standard case: advance to next player's turn
            lobby_info.subphase = next_subphase;
        }
        Ok(())
    }
    // endregion
    // region read-only contract simulation
    /// Preview collision detection without state change. Used by client to determine whether to call `prove_move` or `prove_move_and_prove_rank`.
    pub fn simulate_collisions(e: &Env, address: Address, req: ProveMoveReq) -> Result<UserMove, Error> {
        let temporary = e.storage().temporary();
        let lobby_info: LobbyInfo = temporary.get(&DataKey::LobbyInfo(req.lobby_id)).unwrap();
        let mut game_state: GameState = temporary.get(&DataKey::GameState(req.lobby_id)).unwrap();
        let u_index = Self::get_player_index(&address, &lobby_info);
        if lobby_info.phase != Phase::MoveProve {
            return Err(Error::WrongPhase)
        }
        if lobby_info.subphase == Subphase::Both
        {
            // move_hash with all 8s and empty needed_rank_proofs signifies that simulate_collisions is being called too early
            return Ok(UserMove {
                move_hash: HiddenRankHash::from_array(e, &[8u8; 16]),
                move_proof: Vec::new(e),
                needed_rank_proofs: Vec::new(e),
            })
        }
        let next_subphase = Self::next_subphase(&lobby_info.subphase, u_index)?;
        if next_subphase != Subphase::None
        {
            return Err(Error::WrongSubphase)
        }
        // we don't bother to validate the move
        let mut u_move = game_state.moves.get_unchecked(u_index.u32());
        u_move.move_proof = Vec::from_array(e, [req.move_proof.clone()]);
        game_state.moves.set(u_index.u32(), u_move.clone());
        let pawns_map = Self::create_pawns_map(e, &game_state.pawns);
        let (_, h_needed_rank_proofs, g_needed_rank_proofs) = Self::detect_collisions_with_target_pos(e, &game_state, &pawns_map);
        if u_index == UserIndex::Host {
            u_move.needed_rank_proofs = h_needed_rank_proofs;
        }
        else
        {
            u_move.needed_rank_proofs = g_needed_rank_proofs;
        }
        Ok(u_move)
    }
    // endregion
    // region state mutators
    pub(crate) fn resolve_collision(a_pawn: &mut PawnState, b_pawn: &mut PawnState) -> () {
        let a_pawn_rank = a_pawn.rank.get_unchecked(0);
        let b_pawn_rank = b_pawn.rank.get_unchecked(0);
        a_pawn.zz_revealed = true;
        b_pawn.zz_revealed = true;
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
    pub(crate) fn apply_move_to_pawn(move_proof: &HiddenMove, pawn: &mut PawnState) -> () {
        if pawn.pos != move_proof.target_pos {
            pawn.moved = true;
        }
        if Self::is_scout_move(move_proof) {
            pawn.moved_scout = true;
        }
        pawn.pos = move_proof.target_pos;
    }
    pub(crate) fn complete_move_resolution(e: &Env, game_state: &mut GameState, collision_detection: Option<CollisionDetection>, pawns_map: &Map<PawnId, (u32, PawnState)>) -> () {
        let h_move = game_state.moves.get_unchecked(UserIndex::Host.u32());
        let g_move = game_state.moves.get_unchecked(UserIndex::Guest.u32());
        if !h_move.needed_rank_proofs.is_empty() || !g_move.needed_rank_proofs.is_empty() {
            panic!()
        }
        let h_move_proof = h_move.move_proof.get_unchecked(0);
        let g_move_proof = g_move.move_proof.get_unchecked(0);
        let (h_pawn_index, mut h_pawn) = pawns_map.get_unchecked(h_move_proof.pawn_id);
        let (g_pawn_index, mut g_pawn) = pawns_map.get_unchecked(g_move_proof.pawn_id);
        // Now perform collision resolution using the updated game state
        // Get the updated pawns from game state for collision resolution
        // check collisions
        let collision_detection =  collision_detection.unwrap_or_else(|| {
            Self::detect_collisions_with_target_pos(e, game_state, pawns_map).0
        });
        if collision_detection.has_double_collision {
            Self::resolve_collision(&mut h_pawn, &mut g_pawn);
        }
        else if collision_detection.has_swap_collision {
            Self::resolve_collision(&mut h_pawn, &mut g_pawn);
        }
        else {
            if let Some(hx_collided_id) = collision_detection.g_collision_target {
                // hx is the host pawn that guest collided into
                let (hx_pawn_index, mut hx_pawn) = pawns_map.get_unchecked(hx_collided_id);
                Self::resolve_collision(&mut g_pawn, &mut hx_pawn);
                game_state.pawns.set(hx_pawn_index, Self::pack_pawn(hx_pawn));
            }

            if let Some(gx_collided_id) = collision_detection.h_collision_target {
                // gx is the guest pawn that host collided into
                let (gx_pawn_index, mut gx_pawn) = pawns_map.get_unchecked(gx_collided_id);
                Self::resolve_collision(&mut h_pawn, &mut gx_pawn);
                game_state.pawns.set(gx_pawn_index, Self::pack_pawn(gx_pawn));
            }
        }
        if h_pawn.alive
        {
            Self::apply_move_to_pawn(&h_move_proof, &mut h_pawn);
        }
        if g_pawn.alive
        {
            Self::apply_move_to_pawn(&g_move_proof, &mut g_pawn);
        }
        game_state.pawns.set(h_pawn_index, Self::pack_pawn(h_pawn));
        game_state.pawns.set(g_pawn_index, Self::pack_pawn(g_pawn));
        // Reset moves for next turn
        game_state.moves = Self::create_empty_moves(e);
    }
    // endregion
    // region validation

    pub(crate) fn get_revealed_rank_counts(e: &Env, player_index: UserIndex, game_state: &GameState) -> [u32; 13] {
        let mut revealed_ranks_counts = [0u32; 13];
        for packed_pawn in game_state.pawns.iter() {
            let pawn = Self::unpack_pawn(e, packed_pawn);
            let (_, owner_index) = Self::decode_pawn_id(pawn.pawn_id);
            if player_index != owner_index {
                continue
            }
            if pawn.rank.is_empty() {
                continue
            }
            let rank_index = pawn.rank.get_unchecked(0) as u32;
            revealed_ranks_counts[rank_index as usize] += 1;
        }
        revealed_ranks_counts
    }

    pub(crate) fn validate_board(e: &Env, lobby_parameters: &LobbyParameters) -> bool {
        if lobby_parameters.board.tiles.len() as i32 != lobby_parameters.board.size.x * lobby_parameters.board.size.y {
            log!(e, "validate_board: failed [tiles count must match board size]");
            return false;
        }
        let mut tiles_map: Map<Pos, Tile> = Map::new(e);
        let mut total_passable = 0;
        let mut start_pos: Option<Pos> = None;
        let mut host_setup = 0;
        let mut guest_setup = 0;
        for packed_tile in lobby_parameters.board.tiles.iter() {
            let tile = Self::unpack_tile(packed_tile);
            if tiles_map.contains_key(tile.pos) {
                log!(e, "validate_board: failed [pos is unique]");
                return false;
            }
            if ![0, 1, 2].contains(&tile.setup) {
                log!(e, "validate_board: failed [setup is within bounds]");
                return false;
            }
            if [0, 1].contains(&tile.setup) && !tile.passable {
                log!(e, "validate_board: failed [setup tiles are passable]");
                return false;
            }
            if ![0, 1, 2, 3, 4].contains(&tile.setup_zone) {
                log!(e, "validate_board: failed [setup zone within bounds]");
                return false;
            }
            if tile.setup == 0 {
                host_setup += 1;
            }
            if tile.setup == 1 {
                guest_setup += 1;
            }
            if tile.passable {
                total_passable += 1;
                if start_pos.is_none() {
                    start_pos = Some(tile.pos.clone())
                }
            }
            tiles_map.set(tile.pos.clone(), tile);
        }
        if start_pos.is_none() {
            log!(e, "validate_board: failed [must have a passable tile]");
            return false;
        }
        if host_setup == 0 {
            log!(e, "validate_board: failed [has a host setup tile]");
            return false;
        }
        if guest_setup == 0 {
            log!(e, "validate_board: failed [has a guest setup tile]");
            return false;
        }
        const MAX_BOARD_SIZE: usize = 256;
        const MAX_WAVE_SIZE: usize = 256;
        let start_pos = start_pos.unwrap();
        let board_width = lobby_parameters.board.size.x;
        let board_size = (board_width * lobby_parameters.board.size.y) as usize;
        if board_size > MAX_BOARD_SIZE {
            log!(e, "validate_board: failed [board too large]");
            return false;
        }
        let mut visited = [false; MAX_BOARD_SIZE];
        let mut current_wave = [Pos { x: -42069, y: -42069 }; MAX_WAVE_SIZE];
        let mut next_wave = [Pos { x: -42069, y: -42069 }; MAX_WAVE_SIZE];
        let mut current_len = 0usize;
        let mut next_len = 0usize;
        let mut neighbors = [Pos { x: -42069, y: -42069 }; 6];
        visited[(start_pos.y * board_width + start_pos.x) as usize] = true;
        current_wave[0] = start_pos;
        current_len = 1;
        let mut visited_count = 1u32;
        let neighbor_count: usize = if lobby_parameters.board.hex { 6 } else { 4 };
        let max_iterations = board_size as i32;
        for _ in 0..max_iterations {
            if current_len == 0 || visited_count == total_passable {
                break;
            }
            next_len = 0;
            for i in 0..current_len {
                let pos = current_wave[i];
                Self::get_neighbors(&pos, lobby_parameters.board.hex, &mut neighbors);
                for j in 0..neighbor_count {
                    let neighbor = neighbors[j];
                    if neighbor.x < 0 || neighbor.y < 0 || 
                       neighbor.x >= board_width || neighbor.y >= lobby_parameters.board.size.y {
                        continue;
                    }
                    let idx = (neighbor.y * board_width + neighbor.x) as usize;
                    if visited[idx] {
                        continue;
                    }
                    if let Some(tile) = tiles_map.get(neighbor) {
                        if tile.passable {
                            visited[idx] = true;
                            visited_count += 1;
                            if next_len < MAX_WAVE_SIZE {
                                next_wave[next_len] = neighbor;
                                next_len += 1;
                            }
                        }
                    }
                }
            }
            let (temp_wave, _temp_len) = (current_wave, current_len);
            current_wave = next_wave;
            current_len = next_len;
            next_wave = temp_wave;
        }
        if visited_count != total_passable {
            log!(e, "validate_board: failed [passable tiles must be connected]");
            return false;
        }
        true
    }
    pub(crate) fn is_scout_move(hidden_move: &HiddenMove) -> bool {
        let dx = hidden_move.target_pos.x - hidden_move.start_pos.x;
        let dy = hidden_move.target_pos.y - hidden_move.start_pos.y;
        if dx.abs() > 1 || dy.abs() > 1 {
            return true
        }
        false
    }
    pub(crate) fn validate_rank_proofs(e: &Env, hidden_ranks: &Vec<HiddenRank>, merkle_proofs: &Vec<MerkleProof>, root: &MerkleHash) -> bool {
        let mut valid_rank_proof = true;
        // Check that we have the same number of hidden ranks and merkle proofs
        if hidden_ranks.len() != merkle_proofs.len() {
            return false;
        }
        for (i, hidden_rank) in hidden_ranks.iter().enumerate() {
            let serialized_hidden_rank = hidden_rank.clone().to_xdr(e);
            let full_hash = e.crypto().sha256(&serialized_hidden_rank).to_bytes().to_array();
            let rank_hash = HiddenRankHash::from_array(e, &full_hash[0..16].try_into().unwrap());
            let merkle_proof = merkle_proofs.get(i as u32).unwrap();
            let is_valid = Self::verify_merkle_proof(e, &rank_hash, &merkle_proof, root);
            if !is_valid {
                valid_rank_proof = false;
            }
        }
        valid_rank_proof
    }
    pub(crate) fn validate_move_proof(move_proof: &HiddenMove, player_index: UserIndex, pawns_map: &Map<PawnId, (u32, PawnState)>, lobby_parameters: &LobbyParameters) -> bool {
        // cond: player is owner
        let (_initial_pos, owner_index) = Self::decode_pawn_id(move_proof.pawn_id);
        if owner_index != player_index {
            return false
        }
        // cond: pawn must exist in game state
        let (_, pawn) = match pawns_map.get(move_proof.pawn_id) {
            Some(tuple) => tuple,
            None => {
                return false;
            }
        };
        // cond: pawn must be alive
        if !pawn.alive {
            return false
        }
        // cond: start pos must match
        if move_proof.start_pos != pawn.pos {
            return false
        }
        // cond: start and target tiles must exist and be passable
        let mut start_tile_passable = false;
        let mut target_tile_passable = false;
        for packed_tile in lobby_parameters.board.tiles.iter() {
            let tile = Self::unpack_tile(packed_tile);
            if tile.pos == move_proof.start_pos {
                if tile.passable {
                    start_tile_passable = true;
                }
            }
            if tile.pos == move_proof.target_pos {
                if tile.passable {
                    target_tile_passable = true;
                }
            }
        }
        if !start_tile_passable || !target_tile_passable {
            return false
        }
        // validate move based on rank
        if let Some(rank) = pawn.rank.get(0) {
            // cond: pawn is not unmovable rank flag (0) or trap (11)
            if [0, 11].contains(&rank){
                return false
            }
            // cond: if pawn not a scout (2) it cant move more than one neighboring tile
            if rank != 2 {
                let mut temp_neighbors = [Pos { x: -42069, y: -42069 }; 6];
                Self::get_neighbors(&move_proof.start_pos, lobby_parameters.board.hex, &mut temp_neighbors);
                if !temp_neighbors.contains(&move_proof.target_pos) {
                    return false;
                }
            }
        }
        for (_, (_, n_pawn)) in pawns_map.iter() {
            if n_pawn.pawn_id == pawn.pawn_id || !n_pawn.alive {
                continue
            }
            if n_pawn.pos == move_proof.target_pos {
                // cond: pawn on target pos can't be same owner
                let other_owner_index = Self::decode_pawn_id(n_pawn.pawn_id).1;
                if other_owner_index == player_index {
                    return false
                }
            }
        }
        true
    }
    pub(crate) fn verify_merkle_proof(e: &Env, leaf: &MerkleHash, proof: &MerkleProof, root: &MerkleHash) -> bool {
        let mut current_hash = leaf.clone();
        let mut index = proof.leaf_index;
        for (_, sibling) in proof.siblings.iter().enumerate() {
            // Create a 32-byte array directly for concatenation
            let mut combined_bytes = [0u8; 32];
            // Determine order based on index (even = current is left, odd = current is right)
            if index % 2 == 0 {
                // Current hash goes on the left, sibling on the right
                combined_bytes[0..16].copy_from_slice(&current_hash.to_array());
                combined_bytes[16..32].copy_from_slice(&sibling.to_array());
            } else {
                // Sibling goes on the left, current hash on the right
                combined_bytes[0..16].copy_from_slice(&sibling.to_array());
                combined_bytes[16..32].copy_from_slice(&current_hash.to_array());
            }
            // Hash the combined bytes
            let parent_full = e.crypto().sha256(&Bytes::from_array(e, &combined_bytes));
            // Take first 16 bytes as the new current hash
            let parent_bytes = parent_full.to_array();
            current_hash = MerkleHash::from_array(e, &parent_bytes[0..16].try_into().unwrap());
            // Move up the tree
            index = index / 2;
        }
        let result = current_hash == *root;
        result
    }
    // endregion
    // region questions
    pub(crate) fn get_neighbors(pos: &Pos, is_hex: bool, neighbors: &mut [Pos; 6]) {
        const UNUSED:Pos = Pos {x: -42069, y: -42069};
        // Initialize with sentinel values
        for i in 0..6 {
            neighbors[i] = UNUSED;
        }
        if is_hex {
            // Hex grid has 6 neighbors
            if pos.x % 2 == 0 {
                // Even columns
                neighbors[0] = Pos { x: pos.x, y: pos.y + 1 };      // top
                neighbors[1] = Pos { x: pos.x - 1, y: pos.y + 1 };  // top right
                neighbors[2] = Pos { x: pos.x - 1, y: pos.y };      // bot right
                neighbors[3] = Pos { x: pos.x, y: pos.y - 1 };      // bot
                neighbors[4] = Pos { x: pos.x + 1, y: pos.y };      // bot left
                neighbors[5] = Pos { x: pos.x + 1, y: pos.y + 1 };  // top left
            } else {
                // Odd columns
                neighbors[0] = Pos { x: pos.x, y: pos.y + 1 };      // top
                neighbors[1] = Pos { x: pos.x - 1, y: pos.y };      // top right
                neighbors[2] = Pos { x: pos.x - 1, y: pos.y - 1 };  // bot right
                neighbors[3] = Pos { x: pos.x, y: pos.y - 1 };      // bot
                neighbors[4] = Pos { x: pos.x + 1, y: pos.y - 1 };  // bot left
                neighbors[5] = Pos { x: pos.x + 1, y: pos.y };      // top left
            }
        } else {
            // Square grid has 4 neighbors (orthogonal only)
            neighbors[0] = Pos { x: pos.x, y: pos.y - 1 };      // N
            neighbors[1] = Pos { x: pos.x + 1, y: pos.y };      // E
            neighbors[2] = Pos { x: pos.x, y: pos.y + 1 };      // S
            neighbors[3] = Pos { x: pos.x - 1, y: pos.y };      // W
            // neighbors[4] and neighbors[5] remain as sentinel values
        }
    }
    pub(crate) fn detect_collisions_with_target_pos(e: &Env, game_state: &GameState, pawns_map: &Map<PawnId, (u32, PawnState)>) -> (CollisionDetection, Vec<PawnId>, Vec<PawnId>) {
        let mut has_double_collision = false;
        let mut has_swap_collision = false;
        let mut has_h_collision = false;
        let mut has_g_collision = false;
        let h_move = game_state.moves.get_unchecked(UserIndex::Host.u32());
        let g_move = game_state.moves.get_unchecked(UserIndex::Guest.u32());
        let h_move_proof = h_move.move_proof.get_unchecked(0);
        let g_move_proof = g_move.move_proof.get_unchecked(0);
        let (_, h_pawn) = pawns_map.get_unchecked(h_move_proof.pawn_id);
        let (_, g_pawn) = pawns_map.get_unchecked(g_move_proof.pawn_id);
        let mut h_collision_target: Option<PawnId> = None;
        let mut g_collision_target: Option<PawnId> = None;
        if h_move_proof.target_pos == g_move_proof.target_pos {
            has_double_collision = true;
        }
        else if h_move_proof.target_pos == g_move_proof.start_pos && g_move_proof.target_pos == h_move_proof.start_pos {
            has_swap_collision = true;
        }
        else {
            // Check for collisions with stationary pawns
            for (_, (_, x_pawn)) in pawns_map.iter() {
                if h_move_proof.pawn_id == x_pawn.pawn_id || g_move_proof.pawn_id == x_pawn.pawn_id || !x_pawn.alive {
                    continue;
                }
                // Use target positions from move proofs instead of current pawn positions
                if h_move_proof.target_pos == x_pawn.pos {
                    has_h_collision = true;
                    h_collision_target = Some(x_pawn.pawn_id);
                }
                if g_move_proof.target_pos == x_pawn.pos {
                    has_g_collision = true;
                    g_collision_target = Some(x_pawn.pawn_id);
                }
                if has_h_collision && has_g_collision {
                    break;
                }
            }
        }
        let mut h_needed_rank_proofs: Vec<PawnId> = Vec::new(e);
        let mut g_needed_rank_proofs: Vec<PawnId> = Vec::new(e);
        let h_pawn_involved = has_double_collision || has_swap_collision || has_h_collision;
        let g_pawn_involved = has_double_collision || has_swap_collision || has_g_collision;
        if h_pawn_involved && h_pawn.rank.is_empty() {
            h_needed_rank_proofs.push_back(h_pawn.pawn_id)
        }
        if g_pawn_involved && g_pawn.rank.is_empty() {
            g_needed_rank_proofs.push_back(g_pawn.pawn_id)
        }
        if has_h_collision {
            let (_, hx_pawn) = pawns_map.get_unchecked(h_collision_target.unwrap());
            if hx_pawn.rank.is_empty() {
                g_needed_rank_proofs.push_back(h_collision_target.unwrap());
            }
        }
        if has_g_collision {
            let (_, gx_pawn) = pawns_map.get_unchecked(g_collision_target.unwrap());
            if gx_pawn.rank.is_empty() {
                h_needed_rank_proofs.push_back(g_collision_target.unwrap());
            }
        }
        let collision = CollisionDetection {
            has_double_collision,
            has_g_collision,
            has_swap_collision,
            has_h_collision,
            g_collision_target,
            g_pawn_id: if g_pawn_involved { Some(g_move_proof.pawn_id) } else { None },
            h_collision_target,
            h_pawn_id: if h_pawn_involved { Some(h_move_proof.pawn_id) } else { None },
        };
        (collision, h_needed_rank_proofs, g_needed_rank_proofs)
    }
    pub(crate) fn check_game_over(e: &Env, game_state: &GameState, _lobby_parameters: &LobbyParameters) -> Subphase {
        // game over check happens at the end of turn resolution
        // returns winner. Subphase::None means tie, Subphase::Both means not game over
        // case: game ends when a flag is not alive. if both flags are dead, game ends in a draw
        // find flag
        let mut h_flag_alive = true;
        let mut g_flag_alive = true;
        let pawns_map = Self::create_pawns_map(e, &game_state.pawns);
        for (_, (_, pawn)) in pawns_map.iter() {
            if !pawn.alive {
                // normally this would be risky but all dead pawns should be revealed
                if pawn.rank.get_unchecked(0) == 0 {
                    let (_, owner_index) = Self::decode_pawn_id(pawn.pawn_id);
                    if owner_index == UserIndex::Host {
                        h_flag_alive = false;
                    }
                    else {
                        g_flag_alive = false;
                    }
                }
            }
        }
        match (h_flag_alive, g_flag_alive) {
            (true, false) => return Subphase::Host,
            (false, true) => return Subphase::Guest,
            (false, false) => return Subphase::None,
            _ => (),
        }
        // case: game ends if no legal move can be made (not implemented yet)
        Subphase::Both
    }
    // endregion
    // Data Access Helpers
    pub(crate) fn get_player_index(address: &Address, lobby_info: &LobbyInfo) -> UserIndex {
        // player index is also an identifier encoded into PawnId
        if lobby_info.host_address.contains(address) {
            return UserIndex::Host
        }
        if lobby_info.guest_address.contains(address) {
            return UserIndex::Guest
        }
        panic!()
    }
    pub(crate) fn get_opponent_index(address: &Address, lobby_info: &LobbyInfo) -> UserIndex {
        if lobby_info.host_address.contains(address) {
            return UserIndex::Guest
        }
        if lobby_info.guest_address.contains(address) {
            return UserIndex::Host
        }
        panic!()
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
    pub(crate) fn next_subphase(current_subphase: &Subphase, u_index: UserIndex) -> Result<Subphase, Error> {
        let result = match current_subphase {
            Subphase::Both => Ok(Self::opponent_subphase_from_player_index(u_index)),
            Subphase::Host => if u_index == UserIndex::Host {Ok(Subphase::None)} else {return Err(Error::WrongSubphase)},
            Subphase::Guest => if u_index == UserIndex::Guest {Ok(Subphase::None)} else {return Err(Error::WrongSubphase)},
            Subphase::None => return Err(Error::WrongSubphase),
        };
        result
    }
    pub(crate) fn user_subphase_from_player_index(user_index: UserIndex) -> Subphase {
        if user_index == UserIndex::Host { Subphase::Host } else { Subphase::Guest }
    }
    pub(crate) fn opponent_subphase_from_player_index(user_index: UserIndex) -> Subphase {
        if user_index == UserIndex::Host { Subphase::Guest } else { Subphase::Host }
    }
    pub(crate) fn create_pawns_map(e: &Env, pawns: &Vec<PackedPawn>) -> Map<PawnId, (u32, PawnState)> {
        let mut map = Map::new(e);
        for (index, packed_pawn) in pawns.iter().enumerate() {
            let pawn = Self::unpack_pawn(e, packed_pawn);
            map.set(pawn.pawn_id, (index as u32, pawn));
        }
        map
    }
    // endregion
    // region compression
    pub(crate) fn encode_pawn_id(setup_pos: Pos, user_index: u32) -> u32 {
        let mut id: u32 = 0;
        id |= user_index & 1;                    // Bit 0: user_index (0=host, 1=guest)
        id |= ((setup_pos.x as u32) & 0xF) << 1;       // Bits 1-4: x coordinate (4 bits, range 0-15)
        id |= ((setup_pos.y as u32) & 0xF) << 5;       // Bits 5-8: y coordinate (4 bits, range 0-15)
        id
    }
    pub(crate) fn decode_pawn_id(pawn_id: PawnId) -> (Pos, UserIndex) {
        let user = pawn_id & 1;                      // Bit 0: user_index (0=host, 1=guest)
        let x = ((pawn_id >> 1) & 0xF_u32) as i32;  // Bits 1-4: x coordinate (4 bits, range 0-15)
        let y = ((pawn_id >> 5) & 0xF_u32) as i32;  // Bits 5-8: y coordinate (4 bits, range 0-15)
        let setup_pos = Pos { x, y };
        (setup_pos, UserIndex::from_u32(user))
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
    pub(crate) fn pack_pawn(pawn: PawnState) -> PackedPawn {
        let mut packed: u32 = 0;
        // Pack pawn_id (9 bits at head)
        let pawn_id_packed = pawn.pawn_id & 0x1FF;  // 9 bits: 0x1FF = 511
        packed |= pawn_id_packed << 0;
        // Pack flags (bits 9-11)
        if pawn.alive { packed |= 1 << 9; }
        if pawn.moved { packed |= 1 << 10; }
        if pawn.moved_scout { packed |= 1 << 11; }
        // Pack coordinates (4 bits each, range 0-15)
        packed |= (pawn.pos.x as u32 & 0xF) << 12;
        packed |= (pawn.pos.y as u32 & 0xF) << 16;
        // Pack rank (4 bits)
        let rank = if pawn.rank.is_empty() { 12 } else { pawn.rank.get(0).unwrap() };
        packed |= (rank as u32 & 0xF) << 20;
        // pack zz_revealed (bit 21)
        if pawn.zz_revealed { packed |= 1 << 21; }
        packed
    }
    pub(crate) fn unpack_pawn(e: &Env, packed: PackedPawn) -> PawnState {
        // Extract pawn_id (9 bits at head)
        let pawn_id = packed & 0x1FF;  // 9 bits: 0x1FF = 511
        // Extract flags
        let alive = (packed >> 9) & 1 != 0;
        let moved = (packed >> 10) & 1 != 0;
        let moved_scout = (packed >> 11) & 1 != 0;
        // Extract coordinates (4 bits each, range 0-15)
        let x = ((packed >> 12) & 0xF) as i32;
        let y = ((packed >> 16) & 0xF) as i32;
        // Extract rank
        let rank_val = (packed >> 20) & 0xF;
        // Create rank vector
        let mut rank = Vec::new(e);
        if rank_val != 12 {
            rank.push_back(rank_val);
        }
        let zz_revealed = (packed >> 21) & 1 != 0;
        PawnState {
            alive,
            moved,
            moved_scout,
            pawn_id,
            pos: Pos { x, y },
            rank,
            zz_revealed,
        }
    }
    // endregion
}
// endregion
mod test_utils; // test utilities
mod tests; // organized test modules