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
    pub has_o_collision: bool,
    pub has_swap_collision: bool,
    pub has_u_collision: bool,
    pub o_collision_target: Option<PawnId>,
    pub o_pawn_id: Option<PawnId>,
    pub u_collision_target: Option<PawnId>,
    pub u_pawn_id: Option<PawnId>,
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

#[contractimpl]
impl Contract {
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
            return Err(Error::AlreadyExists)
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
            index: req.lobby_id.clone(),
            guest_address: Vec::new(e),
            host_address: Vec::from_array(e, [address.clone()]),
            phase: Phase::Lobby,
            subphase: Subphase::None,
        };
        user.current_lobby = req.lobby_id.clone();
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
            None => return Err(Error::NotFound),
        };
        if user.current_lobby == 0 {
            return Err(Error::NotFound)
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
            return Err(Error::Unauthorized)
        }
        let (lobby_info, _, lobby_parameters, lobby_info_key, _, _) = Self::get_lobby_data(e, &req.lobby_id, true, false, true)?;
        let mut lobby_info = lobby_info.unwrap();
        let lobby_parameters = lobby_parameters.unwrap();
        let mut lobby_not_joinable = false;
        if lobby_info.phase != Phase::Lobby {
            lobby_not_joinable = true;
        }
        if lobby_info.host_address.is_empty() {
            lobby_not_joinable = true;
        }
        if lobby_info.host_address.contains(&address) {
            lobby_not_joinable = true;
        }
        if !lobby_info.guest_address.is_empty() {
            lobby_not_joinable = true;
        }
        if lobby_not_joinable {
            return Err(Error::LobbyNotJoinable)
        }
        // update
        user.current_lobby = req.lobby_id.clone();
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
                    pawn_id: Self::encode_pawn_id(&tile.pos, &tile.setup),
                    pos: tile.pos.clone(),
                    rank: Vec::new(e),
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
        temporary.set(&DataKey::GameState(req.lobby_id.clone()), &game_state);
        temporary.set(&lobby_info_key, &lobby_info);
        Ok(())
    }
    pub fn commit_setup(e: &Env, address: Address, req: CommitSetupReq) -> Result<(), Error> {
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
        game_state.rank_roots.set(u_index, req.rank_commitment_root);
        if next_subphase == Subphase::None {
            lobby_info.phase = Phase::MoveCommit;
            lobby_info.subphase = Subphase::Both;
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
        let (lobby_info, game_state, _, lobby_info_key, game_state_key, _) = Self::get_lobby_data(e, &req.lobby_id, true, true, false)?;
        let mut lobby_info = lobby_info.unwrap();
        let mut game_state = game_state.unwrap();
        let u_index = Self::get_player_index(&address, &lobby_info)?;
        if lobby_info.phase != Phase::MoveCommit {
            return Err(Error::WrongPhase)
        }
        let next_subphase = Self::next_subphase(&lobby_info.subphase, &u_index)?;
        let mut u_move = game_state.moves.get_unchecked(u_index);
        // update
        u_move.move_hash = req.move_hash.clone();
        if next_subphase == Subphase::None {
            lobby_info.phase = Phase::MoveProve;
            lobby_info.subphase = Subphase::Both;
        }
        else {
            lobby_info.subphase = next_subphase;
        }
        game_state.moves.set(u_index, u_move);
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&game_state_key, &game_state);
        Ok(lobby_info)
    }
    pub fn commit_move_and_prove_move(e: &Env, address: Address, req: CommitMoveReq, req2: ProveMoveReq) -> Result<LobbyInfo, Error> {
        let _ = Self::commit_move(e, address.clone(), req);
        Self::prove_move_internal(e, address, req2)
    }
    pub fn prove_move(e: &Env, address: Address, req: ProveMoveReq) -> Result<LobbyInfo, Error> {
        address.require_auth();
        Self::prove_move_internal(e, address, req)
    }
    pub fn prove_move_and_prove_rank(e: &Env, address: Address, req: ProveMoveReq, req2: ProveRankReq) -> Result<LobbyInfo, Error> {
        address.require_auth();
        _ = Self::prove_move_internal(e, address.clone(), req.clone());
        let result = Self::prove_rank_internal(e, address.clone(), req2);
        result
    }
    pub fn prove_rank(e: &Env, address: Address, req: ProveRankReq) -> Result<LobbyInfo, Error> {
        address.require_auth();
        let result = Self::prove_rank_internal(e, address.clone(), req.clone());
        result
    }
    // endregion
    // region internal
    pub(crate) fn prove_move_internal(e: &Env, address: Address, req: ProveMoveReq) -> Result<LobbyInfo, Error> {
        let temporary = e.storage().temporary();
        let (lobby_info, game_state, lobby_parameters, lobby_info_key, game_state_key, _) = Self::get_lobby_data(e, &req.lobby_id, true, true, true)?;
        let mut lobby_info = lobby_info.unwrap();
        let mut game_state = game_state.unwrap();
        let lobby_parameters = lobby_parameters.unwrap();
        if lobby_info.phase != Phase::MoveProve {
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
                return Err(Error::HashFail)
            }
            let pawns_map = Self::create_pawns_map(e, &game_state.pawns);
            let u_move_valid = Self::validate_move_proof(&req.move_proof, &u_index, &pawns_map, &lobby_parameters);
            if !u_move_valid {
                // immediately abort the game
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
            Self::apply_moves(e, u_index, o_index, &mut game_state);
            // check if rank proofs are needed
            match (game_state.moves.get_unchecked(u_index).needed_rank_proofs.is_empty(), game_state.moves.get_unchecked(o_index).needed_rank_proofs.is_empty()) {
                (true, true) => {
                    //Self::set_history(e, &req.lobby_id, &game_state)?;
                    Self::complete_move_resolution(e, &mut game_state);
                    if let Some(winner) = Self::check_game_over(e, &game_state, &lobby_parameters)? {
                        lobby_info.phase = Phase::Finished;
                        lobby_info.subphase = winner.clone();
                    }
                    else {
                        lobby_info.phase = Phase::MoveCommit;
                        lobby_info.subphase = Subphase::Both;
                        game_state.turn += 1;
                    }
                }
                (true, false) => {
                    lobby_info.phase = Phase::RankProve;
                    lobby_info.subphase = Self::opponent_subphase_from_player_index(&u_index);
                }
                (false, true) => {
                    lobby_info.phase = Phase::RankProve;
                    lobby_info.subphase = Self::user_subphase_from_player_index(&u_index);
                }
                (false, false) => {
                    lobby_info.phase = Phase::RankProve;
                    lobby_info.subphase = Subphase::Both;
                }
            }
        } else {
            lobby_info.subphase = next_subphase;
        }

        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&game_state_key, &game_state);
        Ok(lobby_info)
    }
    pub(crate) fn prove_rank_internal(e: &Env, address: Address, req: ProveRankReq) -> Result<LobbyInfo, Error> {
        let temporary = e.storage().temporary();
        let (lobby_info, game_state, lobby_parameters, lobby_info_key, game_state_key, _) = Self::get_lobby_data(e, &req.lobby_id, true, true, true)?;
        let mut lobby_info = lobby_info.unwrap();
        let mut game_state = game_state.unwrap();
        let lobby_parameters = lobby_parameters.unwrap();
        let u_index = Self::get_player_index(&address, &lobby_info)?;
        if lobby_info.phase != Phase::RankProve {
            return Err(Error::WrongPhase)
        }
        let u_move = game_state.moves.get_unchecked(u_index);
        if u_move.needed_rank_proofs.is_empty() {
            return Err(Error::InvalidArgs)
        }
        if u_move.needed_rank_proofs.len() != req.hidden_ranks.len() {
            return Err(Error::InvalidArgs)
        }
        {
            let pawns_map = Self::create_pawns_map(e, &game_state.pawns);
            let rank_root = game_state.rank_roots.get_unchecked(u_index);
            if !Self::validate_rank_proofs(e, &req.hidden_ranks, &req.merkle_proofs, &pawns_map, &rank_root) {
                // abort the game
                lobby_info.phase = Phase::Aborted;
                lobby_info.subphase = Self::opponent_subphase_from_player_index(&u_index);
                temporary.set(&lobby_info_key, &lobby_info);
                return Ok(lobby_info)
            }
            for hidden_rank in req.hidden_ranks.iter() {
                let (pawn_index, mut pawn) = pawns_map.get_unchecked(hidden_rank.pawn_id);
                pawn.rank = Vec::from_array(e, [hidden_rank.rank.clone()]);
                game_state.pawns.set(pawn_index, Self::pack_pawn(pawn));
            }
        }
        // clear needed_rank_proofs
        {
            let mut u_move = game_state.moves.get_unchecked(u_index);
            u_move.needed_rank_proofs = Vec::new(e);
            game_state.moves.set(u_index, u_move);
        }
        let next_subphase = Self::next_subphase(&lobby_info.subphase, &u_index)?;
        if next_subphase == Subphase::None {
            // Both players have acted, check if we can transition to next phase
            //Self::set_history(e, &req.lobby_id, &game_state)?;
            Self::complete_move_resolution(e, &mut game_state);
            if let Some(winner) = Self::check_game_over(e, &game_state, &lobby_parameters)? {
                lobby_info.phase = Phase::Finished;
                lobby_info.subphase = winner.clone();
            }
            else {
                // Transition to next turn
                lobby_info.phase = Phase::MoveCommit;
                lobby_info.subphase = Subphase::Both;
                game_state.turn += 1;
            }
        } else {
            // Standard case: advance to next player's turn
            lobby_info.subphase = next_subphase;
        }
        temporary.set(&lobby_info_key, &lobby_info);
        temporary.set(&game_state_key, &game_state);
        Ok(lobby_info)
    }
    // endregion
    // region read-only contract simulation
    pub fn simulate_collisions(e: &Env, address: Address, req: ProveMoveReq) -> Result<UserMove, Error> {
        let (lobby_info, game_state, _, _, _, _) = Self::get_lobby_data(e, &req.lobby_id, true, true, false)?;
        let  lobby_info = lobby_info.unwrap();
        let mut game_state = game_state.unwrap();
        let u_index = Self::get_player_index(&address, &lobby_info)?;
        let o_index = Self::get_opponent_index(&address, &lobby_info)?;
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
        let next_subphase = Self::next_subphase(&lobby_info.subphase, &u_index)?;
        if next_subphase != Subphase::None
        {
            return Err(Error::WrongSubphase)
        }
        // we don't bother to validate the move
        let mut u_move = game_state.moves.get_unchecked(u_index);
        u_move.move_proof = Vec::from_array(e, [req.move_proof.clone()]);
        game_state.moves.set(u_index, u_move);
        Self::apply_moves(e, u_index, o_index, &mut game_state);
        Ok(game_state.moves.get_unchecked(u_index))
    }
    // endregion
    // region state mutators
    pub(crate) fn resolve_collision(a_pawn: &mut PawnState, b_pawn: &mut PawnState) -> () {
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
    pub(crate) fn apply_move_to_pawn(move_proof: &HiddenMove, pawn: &mut PawnState) -> () {
        if pawn.pos != move_proof.start_pos {
            pawn.moved = true;
        }
        if Self::is_scout_move(move_proof) {
            pawn.moved_scout = true;
        }
        pawn.pos = move_proof.target_pos.clone();
    }
    pub(crate) fn complete_move_resolution(e: &Env, game_state: &mut GameState) -> () {
        let u_index = 0; // this is weird? why does this work???
        let o_index = 1;
        let u_move = game_state.moves.get_unchecked(u_index);
        let o_move = game_state.moves.get_unchecked(o_index);
        // Now perform collision resolution using the updated game state
        let u_move_proof = u_move.move_proof.get_unchecked(0);
        let o_move_proof = o_move.move_proof.get_unchecked(0);

        // Get the updated pawns from game state for collision resolution
        let pawns_map = Self::create_pawns_map(e, &game_state.pawns);


        // Detect and resolve collisions
        let collision_detection = Self::detect_collisions(&game_state, &pawns_map, u_index, o_index);

        let (u_pawn_index, mut u_pawn) = pawns_map.get_unchecked(u_move_proof.pawn_id.clone());
        let (o_pawn_index, mut o_pawn) = pawns_map.get_unchecked(o_move_proof.pawn_id.clone());

        if collision_detection.has_double_collision {
            Self::resolve_collision(&mut u_pawn, &mut o_pawn);
            game_state.pawns.set(u_pawn_index, Self::pack_pawn(u_pawn));
            game_state.pawns.set(o_pawn_index, Self::pack_pawn(o_pawn));
        }
        else if collision_detection.has_swap_collision {
            Self::resolve_collision(&mut u_pawn, &mut o_pawn);
            game_state.pawns.set(u_pawn_index, Self::pack_pawn(u_pawn));
            game_state.pawns.set(o_pawn_index, Self::pack_pawn(o_pawn));
        }
        else {
            if let Some(u_collision_target) = collision_detection.u_collision_target {
                let (ux_pawn_index, mut ux_pawn) = pawns_map.get_unchecked(u_collision_target);
                Self::resolve_collision(&mut u_pawn, &mut ux_pawn);
                game_state.pawns.set(u_pawn_index, Self::pack_pawn(u_pawn));
                game_state.pawns.set(ux_pawn_index, Self::pack_pawn(ux_pawn));
            }

            if let Some(o_collision_target) = collision_detection.o_collision_target {
                let (ox_pawn_index, mut ox_pawn) = pawns_map.get_unchecked(o_collision_target);
                Self::resolve_collision(&mut o_pawn, &mut ox_pawn);
                game_state.pawns.set(o_pawn_index, Self::pack_pawn(o_pawn));
                game_state.pawns.set(ox_pawn_index, Self::pack_pawn(ox_pawn));
            }
        }
        // Reset moves for next turn
        game_state.moves = Self::create_empty_moves(e);

    }
    pub(crate) fn apply_moves(e: &Env, u_index: u32, o_index: u32, game_state: &mut GameState) -> (Vec<PawnId>, Vec<PawnId>){
        {
            let pawns_map = Self::create_pawns_map(e, &game_state.pawns);
            let u_move_proof = game_state.moves.get_unchecked(u_index).move_proof.get_unchecked(0);
            let o_move_proof = game_state.moves.get_unchecked(o_index).move_proof.get_unchecked(0);
            let (u_pawn_index, mut u_pawn) = pawns_map.get_unchecked(u_move_proof.pawn_id.clone());
            let (o_pawn_index, mut o_pawn) = pawns_map.get_unchecked(o_move_proof.pawn_id.clone());
            Self::apply_move_to_pawn(&u_move_proof, &mut u_pawn);
            Self::apply_move_to_pawn(&o_move_proof, &mut o_pawn);
            game_state.pawns.set(u_pawn_index, Self::pack_pawn(u_pawn));
            game_state.pawns.set(o_pawn_index, Self::pack_pawn(o_pawn));
        }
        // set needed rank proofs
        let updated_pawns_map = Self::create_pawns_map(e, &game_state.pawns);
        let collision_detection = Self::detect_collisions(&game_state, &updated_pawns_map, u_index, o_index);
        let (u_proof_list, o_proof_list) = Self::get_needed_rank_proofs(e, &collision_detection, &updated_pawns_map);
        {
            let mut u_move = game_state.moves.get_unchecked(u_index);
            let mut o_move = game_state.moves.get_unchecked(o_index);
            u_move.needed_rank_proofs = u_proof_list.clone();
            o_move.needed_rank_proofs = o_proof_list.clone();
            game_state.moves.set(u_index, u_move);
            game_state.moves.set(o_index, o_move);
        }
        (u_proof_list.clone(), o_proof_list.clone())
    }
    // endregion
    // region validation
    pub(crate) fn is_scout_move(hidden_move: &HiddenMove) -> bool {
        // TODO: something seems weird about this scout_move is usually wrong
        let dx = hidden_move.target_pos.x - hidden_move.start_pos.x;
        let dy = hidden_move.target_pos.y - hidden_move.start_pos.y;
        if dx.abs() > 1 || dy.abs() > 1 {
            return true
        }
        false
    }
    pub(crate) fn validate_rank_proofs(e: &Env, hidden_ranks: &Vec<HiddenRank>, merkle_proofs: &Vec<MerkleProof>, pawns_map: &Map<PawnId, (u32, PawnState)>, root: &MerkleHash) -> bool {
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
            // Check if pawn exists in game state
            if !pawns_map.contains_key(hidden_rank.pawn_id.clone()) {
                valid_rank_proof = false;
            }
        }
        valid_rank_proof
    }
    pub(crate) fn validate_move_proof(move_proof: &HiddenMove, player_index: &u32, pawns_map: &Map<PawnId, (u32, PawnState)>, lobby_parameters: &LobbyParameters) -> bool {
        // cond: pawn must exist in game state
        let (_, pawn) = match pawns_map.get(move_proof.pawn_id.clone()) {
            Some(tuple) => tuple,
            None => {
                return false;
            }
        };
        // cond: start pos must match
        if move_proof.start_pos != pawn.pos {
            return false
        }

        // cond target pos must be valid - check bounds based on actual board positions
        let min_x = 0;
        let max_x = lobby_parameters.board.size.x;
        let min_y = 0;
        let max_y = lobby_parameters.board.size.y;

        if move_proof.target_pos.x < min_x || move_proof.target_pos.x > max_x ||
           move_proof.target_pos.y < min_y || move_proof.target_pos.y > max_y {
            return false
        }
        // tile must exist
        let mut tile_exists = false;
        for packed_tile in lobby_parameters.board.tiles.iter() {
            let tile = Self::unpack_tile(packed_tile);
            if tile.pos == move_proof.target_pos {
                tile_exists = true;
                if !tile.passable {
                    return false;
                }
            }
        }
        if !tile_exists {
            return false
        }
        // cond: pawn must be alive
        if !pawn.alive {
            return false
        }
        // cond: player is owner
        let (_initial_pos, team) = Self::decode_pawn_id(&move_proof.pawn_id);
        if team != *player_index {
            return false
        }
        // cond: pawn is not unmovable rank (flag or trap)
        if let Some(rank) = pawn.rank.get(0) {
            if rank == 0 {
                return false
            }
            if rank == 11 {
                return false
            }
            // TODO: cond: target pos must be in the set of valid positions depending on rank
        }
        for (_, (_, n_pawn)) in pawns_map.iter() {
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
    pub(crate) fn detect_collisions(game_state: &GameState, pawns_map: &Map<PawnId, (u32, PawnState)>, u_index: u32, o_index: u32, ) -> CollisionDetection {
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

        let (_, u_pawn) = pawns_map.get_unchecked(u_pawn_id);
        let (_, o_pawn) = pawns_map.get_unchecked(o_pawn_id);

        if u_target_pos == o_target_pos {
            has_double_collision = true;
        }
        else if u_target_pos == o_start_pos && o_target_pos == u_start_pos {
            has_swap_collision = true;
        }
        else {
            // Check for collisions with stationary pawns
            for (_, (_, x_pawn)) in pawns_map.iter() {
                if u_pawn.pawn_id == x_pawn.pawn_id || o_pawn.pawn_id == x_pawn.pawn_id || !x_pawn.alive {
                    continue;
                }
                if u_pawn.pos == x_pawn.pos {
                    has_u_collision = true;
                    u_collision_target = Some(x_pawn.pawn_id.clone());
                }
                if o_pawn.pos == x_pawn.pos {
                    has_o_collision = true;
                    o_collision_target = Some(x_pawn.pawn_id.clone());
                }
                if has_u_collision && has_o_collision {
                    break;
                }
            }
        }


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
    pub(crate) fn get_needed_rank_proofs(e: &Env, collision_detection: &CollisionDetection, pawns_map: &Map<PawnId, (u32, PawnState)>) -> (Vec<PawnId>, Vec<PawnId>) {
         let mut u_proof_list: Vec<PawnId> = Vec::new(e);
         let mut o_proof_list: Vec<PawnId> = Vec::new(e);
         if collision_detection.has_double_collision || collision_detection.has_swap_collision {
             // Both pawns involved in double/swap collision need rank proofs if they don't have ranks
             if let Some(u_pawn_id) = &collision_detection.u_pawn_id {
                 let (_, u_pawn) = pawns_map.get_unchecked(*u_pawn_id);
                 if u_pawn.rank.is_empty() {
                     u_proof_list.push_back(*u_pawn_id);
                 }
             }
             if let Some(o_pawn_id) = &collision_detection.o_pawn_id {
                 let (_, o_pawn) = pawns_map.get_unchecked(*o_pawn_id);
                 if o_pawn.rank.is_empty() {
                     o_proof_list.push_back(*o_pawn_id);
                 }
             }
         }
         else {
             // Handle individual collisions with stationary pawns
             if let Some(u_collision_target) = &collision_detection.u_collision_target {
                 if let Some(u_pawn_id) = &collision_detection.u_pawn_id {
                     let (_, u_pawn) = pawns_map.get_unchecked(*u_pawn_id);
                     if u_pawn.rank.is_empty() {
                         u_proof_list.push_back(*u_pawn_id);
                     }
                 }
                 let (_, ux_pawn) = pawns_map.get_unchecked(*u_collision_target);
                 if ux_pawn.rank.is_empty() {
                     o_proof_list.push_back(*u_collision_target);
                 }
             }
             if let Some(o_collision_target) = &collision_detection.o_collision_target {
                 if let Some(o_pawn_id) = &collision_detection.o_pawn_id {
                     let (_, o_pawn) = pawns_map.get_unchecked(*o_pawn_id);
                     if o_pawn.rank.is_empty() {
                         o_proof_list.push_back(*o_pawn_id);
                     }
                 }
                 let (_, ox_pawn) = pawns_map.get_unchecked(*o_collision_target);
                 if ox_pawn.rank.is_empty() {
                     u_proof_list.push_back(*o_collision_target);
                 }
             }
         }
         (u_proof_list, o_proof_list)
     }
    pub(crate) fn check_game_over(e: &Env, game_state: &GameState, _lobby_parameters: &LobbyParameters) -> Result<Option<Subphase>, Error> {
        // game over check happens at the end of turn resolution
        // case: game ends when a flag is not alive. if both flags are dead, game ends in a draw
        // find flag
        let mut h_flag_alive = true;
        let mut g_flag_alive = true;
        let pawns_map = Self::create_pawns_map(e, &game_state.pawns);
        for (_, (_, pawn)) in pawns_map.iter() {
            if !pawn.alive {
                // normally this would be risky but all dead pawns should be revealed
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
    // endregion
    // Data Access Helpers
    pub(crate) fn get_player_index(address: &Address, lobby_info: &LobbyInfo) -> Result<u32, Error> {
        // player index is also an identifier encoded into PawnId
        if lobby_info.host_address.contains(address) {
            return Ok(0)
        }
        if lobby_info.guest_address.contains(address) {
            return Ok(1)
        }
        Err(Error::Unauthorized)
    }
    pub(crate) fn get_opponent_index(address: &Address, lobby_info: &LobbyInfo) -> Result<u32, Error> {
        if lobby_info.host_address.contains(address) {
            return Ok(1)
        }
        if lobby_info.guest_address.contains(address) {
            return Ok(0)
        }
        Err(Error::Unauthorized)
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
    pub(crate) fn get_lobby_data(e: &Env, lobby_id: &LobbyId, need_lobby_info: bool, need_game_state: bool, need_lobby_parameters: bool) -> Result<(Option<LobbyInfo>, Option<GameState>, Option<LobbyParameters>, DataKey, DataKey, DataKey), Error> {
        let temporary = e.storage().temporary();
        let lobby_info_key = DataKey::LobbyInfo(lobby_id.clone());
        let game_state_key = DataKey::GameState(lobby_id.clone());
        let lobby_parameters_key = DataKey::LobbyParameters(lobby_id.clone());
        let lobby_info = if need_lobby_info {
            match temporary.get(&lobby_info_key) {
                Some(info) => Some(info),
                None => return Err(Error::NotFound),
            }
        } else {
            None
        };
        let game_state = if need_game_state {
            match temporary.get(&game_state_key) {
                Some(state) => Some(state),
                None => return Err(Error::NotFound),
            }
        } else {
            None
        };
        let lobby_parameters = if need_lobby_parameters {
            match temporary.get(&lobby_parameters_key) {
                Some(params) => Some(params),
                None => return Err(Error::NotFound),
            }
        } else {
            None
        };
        Ok((lobby_info, game_state, lobby_parameters, lobby_info_key, game_state_key, lobby_parameters_key))
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
    pub(crate) fn encode_pawn_id(pos: &Pos, team: &u32) -> u32 {
        let mut id: u32 = 0;
        // New 9-bit encoding: bit 0=team, bits 1-5=x, bits 6-9=y
        id |= *team & 1;                           // Bit 0: team
        id |= ((pos.x as u32) & 0x1F) << 1;       // Bits 1-5: x coordinate (5 bits)
        id |= ((pos.y as u32) & 0xF) << 6;        // Bits 6-9: y coordinate (4 bits)
        id
    }
    pub(crate) fn decode_pawn_id(pawn_id: &u32) -> (Pos, u32) {
        // New 9-bit encoding: bit 0=team, bits 1-5=x, bits 6-9=y
        let team = pawn_id & 1;                      // Bit 0: team
        let x = ((pawn_id >> 1) & 0x1F_u32) as i32;     // Bits 1-5: x coordinate (5 bits)
        let y = ((pawn_id >> 6) & 0xF_u32) as i32;      // Bits 6-9: y coordinate (4 bits)
        let pos = Pos { x, y };
        (pos, team)
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
        // Pack pawn_id (9 bits at head) using encode_pawn_id function
        let pawn_id_packed = pawn.pawn_id & 0x1FF;
        packed |= pawn_id_packed << 0;
        // Pack flags (bits 9-11)
        if pawn.alive { packed |= 1 << 9; }
        if pawn.moved { packed |= 1 << 10; }
        if pawn.moved_scout { packed |= 1 << 11; }
        // Pack coordinates (5 bits each, range 0-15)
        packed |= (pawn.pos.x as u32 & 0x1F) << 12;
        packed |= (pawn.pos.y as u32 & 0x1F) << 17;
        // Pack rank (4 bits)
        let rank = if pawn.rank.is_empty() { 12 } else { pawn.rank.get(0).unwrap() };
        packed |= (rank as u32 & 0xF) << 22;
        packed
    }
    pub(crate) fn unpack_pawn(e: &Env, packed: PackedPawn) -> PawnState {
        // Extract pawn_id (9 bits at head)
        let pawn_id = packed & 0x1FF;
        // Extract flags
        let alive = (packed >> 9) & 1 != 0;
        let moved = (packed >> 10) & 1 != 0;
        let moved_scout = (packed >> 11) & 1 != 0;
        // Extract coordinates (5 bits each, range 0-15)
        let x = ((packed >> 12) & 0x1F) as i32;
        let y = ((packed >> 17) & 0x1F) as i32;
        // Extract rank
        let rank_val = (packed >> 22) & 0xF;
        // Create rank vector
        let mut rank = Vec::new(e);
        if rank_val != 12 {
            rank.push_back(rank_val);
        }
        PawnState {
            alive,
            moved,
            moved_scout,
            pawn_id,
            pos: Pos { x, y },
            rank,
        }
    }
    // endregion
}
// endregion


mod test_utils; // test utilities
mod tests; // organized test modules