#![cfg(test)]
extern crate std;
use super::*;
use soroban_sdk::{Env, Address, Vec, Map};

// region ANSI color codes

// ANSI color codes for terminal output
pub const RESET: &str = "\x1b[0m";
pub const BOLD: &str = "\x1b[1m";
pub const YELLOW: &str = "\x1b[33m";
pub const MAGENTA: &str = "\x1b[35m";
pub const CYAN: &str = "\x1b[36m";
pub const WHITE: &str = "\x1b[37m";
pub const BRIGHT_RED: &str = "\x1b[91m";
pub const BRIGHT_BLUE: &str = "\x1b[94m";

// endregion

// region board visualization

/// Creates a colorized text representation of the current board state
pub fn format_board_with_colors(_env: &Env, snapshot: &SnapshotFull) -> std::string::String {
    format_board_with_colors_and_ranks(_env, snapshot, None, None)
}

pub fn format_board_with_colors_and_ranks(
    _env: &Env, 
    snapshot: &SnapshotFull, 
    host_ranks: Option<&Vec<HiddenRank>>, 
    guest_ranks: Option<&Vec<HiddenRank>>
) -> std::string::String {
    let board = &snapshot.lobby_parameters.board;
    let width = board.size.x;
    let height = board.size.y;
    
    // Create a map of pawn IDs to ranks from the provided hidden ranks
    let mut rank_map: std::collections::HashMap<PawnId, Rank> = std::collections::HashMap::new();
    if let Some(host_hidden_ranks) = host_ranks {
        for hidden_rank in host_hidden_ranks.iter() {
            rank_map.insert(hidden_rank.pawn_id, hidden_rank.rank);
        }
    }
    if let Some(guest_hidden_ranks) = guest_ranks {
        for hidden_rank in guest_hidden_ranks.iter() {
            rank_map.insert(hidden_rank.pawn_id, hidden_rank.rank);
        }
    }
    
    // Determine if we're showing revealed state
    let is_revealed = host_ranks.is_some() || guest_ranks.is_some();
    
    // Create a map of positions to pawns for quick lookup
    let mut pawn_map: std::collections::HashMap<(i32, i32), PawnState> = std::collections::HashMap::new();
    for pawn in snapshot.game_state.pawns.iter() {
        if pawn.alive {
            pawn_map.insert((pawn.pos.x, pawn.pos.y), pawn);
        }
    }
    
    // Create a map of positions to tiles for quick lookup
    let mut tile_map: std::collections::HashMap<(i32, i32), Tile> = std::collections::HashMap::new();
    for packed_tile in board.tiles.iter() {
        let tile = Contract::unpack_tile(packed_tile);
        tile_map.insert((tile.pos.x, tile.pos.y), tile);
    }
    
    let mut result = std::string::String::new();
    
    // Add simplified header with phase and subphase
    let revealed_text = if is_revealed { " (REVEALED)" } else { "" };
    result.push_str(&std::format!("{}=== BOARD STATE {:?} {:?}{} ==={}\n\n", 
                                   BOLD, snapshot.lobby_info.phase, snapshot.lobby_info.subphase, revealed_text, RESET));
    
    // Add column headers with color
    result.push_str(&std::format!("{}   ", BOLD));
    for x in 0..width {
        result.push_str(&std::format!("{:2} ", x));
    }
    result.push_str(&std::format!("{}\n", RESET));
    
    // Print board from top to bottom (y = height-1 to 0)
    for y in (0..height).rev() {
        result.push_str(&std::format!("{}{:2}{} ", BOLD, y, RESET));
        
        for x in 0..width {
            let pos = (x, y);
            
            if let Some(pawn) = pawn_map.get(&pos) {
                // There's a pawn here
                let (_, team) = Contract::decode_pawn_id(&pawn.pawn_id);
                
                // Determine the rank to display and whether it's revealed in game state
                let (display_rank, is_revealed_in_game) = if !pawn.rank.is_empty() {
                    // Rank is revealed in game state - use bright team color
                    (Some(pawn.rank.get(0).unwrap()), true)
                } else if let Some(&hidden_rank) = rank_map.get(&pawn.pawn_id) {
                    // Rank is only known from parameter - use darker team color
                    (Some(hidden_rank), false)
                } else {
                    // No rank available
                    (None, false)
                };
                
                let rank_char = if let Some(rank) = display_rank {
                    match rank {
                        0 => 'F',   // Flag
                        1 => '1',   // Spy
                        2 => '2',   // Scout
                        3 => '3',   // Miner
                        4 => '4',   // Sergeant
                        5 => '5',   // Lieutenant
                        6 => '6',   // Captain
                        7 => '7',   // Major
                        8 => '8',   // Colonel
                        9 => '9',   // General
                        10 => 'G',  // Marshal (General)
                        11 => 'B',  // Bomb
                        _ => '?'
                    }
                } else {
                    '?'  // Unknown rank
                };
                
                // Use different colors and formatting for different teams
                if team == 0 {
                    // Host team 
                    if is_revealed_in_game { 
                        result.push_str(&std::format!("{}{{{}}}{}", 
                                                       BRIGHT_RED, rank_char, RESET)); // Curly braces for revealed
                    } else { 
                        result.push_str(&std::format!("{}[{}]{}", 
                                                       MAGENTA, rank_char, RESET)); // Square brackets for hidden
                    };
                } else {
                    // Guest team  
                    if is_revealed_in_game { 
                        result.push_str(&std::format!("{}{{{}}}{}", 
                                                       BRIGHT_BLUE, rank_char, RESET)); // Curly braces for revealed
                    } else { 
                        result.push_str(&std::format!("{}[{}]{}", 
                                                       CYAN, rank_char, RESET)); // Square brackets for hidden
                    };
                }
            } else if let Some(tile) = tile_map.get(&pos) {
                // No pawn, show tile info with colors
                if !tile.passable {
                    result.push_str(&std::format!("{}~~~{}", WHITE, RESET));  // Water/lake in white
                } else {
                    match tile.setup {
                        0 => result.push_str(&std::format!("{} . {}", MAGENTA, RESET)),     // Host setup area in magenta (hidden color)
                        1 => result.push_str(&std::format!("{} . {}", CYAN, RESET)),        // Guest setup area in cyan (hidden color) using dots
                        2 => result.push_str("   "),                                        // Neutral area - no color
                        _ => result.push_str(&std::format!("{} ? {}", YELLOW, RESET)),      // Unknown in yellow
                    }
                }
            } else {
                result.push_str(&std::format!("{} ? {}", BRIGHT_RED, RESET));  // Should not happen - bright red
            }
        }
        result.push_str(&std::format!(" {}{}{}\n", BOLD, y, RESET));
    }
    
    // Add column footers with color
    result.push_str(&std::format!("{}   ", BOLD));
    for x in 0..width {
        result.push_str(&std::format!("{:2} ", x));
    }
    result.push_str(&std::format!("{}\n", RESET));
    
    result
}

// endregion

// region move logging

/// Prints a formatted move log from a vector of move entries
pub fn print_move_log(move_log: &std::vec::Vec<(u32, PawnId, Rank, i32, i32, i32, i32, PawnId, Rank, bool, bool, PawnId, Rank, i32, i32, i32, i32, PawnId, Rank, bool, bool)>) {
    std::println!("\n=== COMPLETE MOVE LOG ===");
    for move_entry in move_log {
        let (turn, host_pawn_id, host_rank, host_sx, host_sy, host_tx, host_ty, 
             host_collision_id, host_collision_rank, host_alive, host_collision_alive,
             guest_pawn_id, guest_rank, guest_sx, guest_sy, guest_tx, guest_ty,
             guest_collision_id, guest_collision_rank, guest_alive, guest_collision_alive) = move_entry;
        
        let host_rank_str = rank_to_string(*host_rank);
        let guest_rank_str = rank_to_string(*guest_rank);
        let host_collision_str = if *host_collision_id == 0 { "none" } else { "collision" };
        let guest_collision_str = if *guest_collision_id == 0 { "none" } else { "collision" };
        
        std::println!("{} - {}|{} ({},{}) -> ({},{}) -> {} | {}|{} ({},{}) -> ({},{}) -> {}",
            turn, host_pawn_id, host_rank_str, host_sx, host_sy, host_tx, host_ty, host_collision_str,
            guest_pawn_id, guest_rank_str, guest_sx, guest_sy, guest_tx, guest_ty, guest_collision_str);
    }
    std::println!("=== END MOVE LOG ===");
}

/// Converts a rank number to its string representation
pub fn rank_to_string(rank: Rank) -> &'static str {
    if rank == 999 { 
        "?" 
    } else { 
        match rank {
            0 => "Flag", 1 => "Spy", 2 => "Scout", 3 => "Miner", 4 => "Sergeant",
            5 => "Lieutenant", 6 => "Captain", 7 => "Major", 8 => "Colonel", 
            9 => "General", 10 => "Marshal", 11 => "Bomb", _ => "Unknown"
        }
    }
}

// endregion

// region move validation

/// Validates that pawns involved in collisions have their ranks properly revealed
/// Takes game state as parameter instead of accessing storage
pub fn assert_ranks_revealed_after_collision(
    game_state: &GameState,
    expected_revealed_pawns: &[PawnId],
) {
    std::println!("=== COLLISION RANK REVELATION VALIDATION ===");
    for expected_pawn_id in expected_revealed_pawns {
        let pawn = game_state.pawns.iter()
            .find(|p| p.pawn_id == *expected_pawn_id)
            .expect(&std::format!("Pawn {} should exist", expected_pawn_id));
        
        assert!(!pawn.rank.is_empty(), "Pawn {} should have its rank revealed", expected_pawn_id);
        
        let rank_str = rank_to_string(pawn.rank.get(0).unwrap());
        std::println!("Pawn {} rank revealed: {} (alive: {})", pawn.pawn_id, rank_str, pawn.alive);
    }
    std::println!("=== END VALIDATION ===");
}

/// Validates the expected game phase and subphase
/// Takes lobby info as parameter instead of accessing storage
pub fn validate_game_phase(
    lobby_info: &LobbyInfo,
    expected_phase: Phase,
    expected_subphase: Option<Subphase>,
) {
    assert_eq!(lobby_info.phase, expected_phase, "Game should be in phase {:?}", expected_phase);
    
    if let Some(subphase) = expected_subphase {
        assert_eq!(lobby_info.subphase, subphase, "Game should be in subphase {:?}", subphase);
    }
    
    std::println!("Phase validation passed: Phase={:?}, Subphase={:?}", lobby_info.phase, lobby_info.subphase);
}

/// Creates ProveRankReq objects for both players given their needed rank proofs
/// Returns (host_rank_req, guest_rank_req)
pub fn create_rank_proof_requests(
    env: &Env,
    lobby_id: u32,
    host_needed_ranks: &Vec<PawnId>,
    guest_needed_ranks: &Vec<PawnId>,
    host_ranks: &Vec<HiddenRank>,
    guest_ranks: &Vec<HiddenRank>,
) -> (Option<ProveRankReq>, Option<ProveRankReq>) {
    let mut host_rank_req: Option<ProveRankReq> = None;
    let mut guest_rank_req: Option<ProveRankReq> = None;
    
    // Create host rank proof request if needed
    if !host_needed_ranks.is_empty() {
        let mut host_hidden_ranks = Vec::new(env);
        for required_pawn_id in host_needed_ranks.iter() {
            for hidden_rank in host_ranks.iter() {
                if hidden_rank.pawn_id == required_pawn_id {
                    host_hidden_ranks.push_back(hidden_rank);
                }
            }
        }
        let req = ProveRankReq {
            lobby_id,
            hidden_ranks: host_hidden_ranks,
        };
        assert_eq!(req.hidden_ranks.len(), host_needed_ranks.len(), "Host should have all required rank proofs");
        host_rank_req = Some(req);
    }
    
    // Create guest rank proof request if needed
    if !guest_needed_ranks.is_empty() {
        let mut guest_hidden_ranks = Vec::new(env);
        for required_pawn_id in guest_needed_ranks.iter() {
            for hidden_rank in guest_ranks.iter() {
                if hidden_rank.pawn_id == required_pawn_id {
                    guest_hidden_ranks.push_back(hidden_rank);
                }
            }
        }
        let req = ProveRankReq {
            lobby_id,
            hidden_ranks: guest_hidden_ranks,
        };
        assert_eq!(req.hidden_ranks.len(), guest_needed_ranks.len(), "Guest should have all required rank proofs");
        guest_rank_req = Some(req);
    }
    
    (host_rank_req, guest_rank_req)
}

// endregion

// region move generation

/// Generate a valid move for testing purposes.
/// 
/// This function takes the team's own rank information (via team_ranks parameter) 
/// to avoid moving immovable pieces (flags/bombs), but does not peek at strategic 
/// game state information or care about opponent ranks.
/// 
/// This ensures consistent move generation regardless of whether ranks are 
/// populated in the game state or not.
pub fn generate_valid_move_req(env: &Env, game_state: &GameState, lobby_parameters: &LobbyParameters, team: u32, team_ranks: &Vec<HiddenRank>, salt: u64) -> Option<HiddenMove> {
    // Create a map of all tile positions for quick lookup
    let mut tile_map: Map<Pos, Tile> = Map::new(env);
    for packed_tile in lobby_parameters.board.tiles.iter() {
        let tile = Contract::unpack_tile(packed_tile);
        tile_map.set(tile.pos, tile);
    }
    
    // Create a map of pawn ranks for this team from the provided ranks
    let mut rank_map: Map<PawnId, Rank> = Map::new(env);
    for hidden_rank in team_ranks.iter() {
        rank_map.set(hidden_rank.pawn_id, hidden_rank.rank);
    }
    
    // Create a map of all pawn positions for quick lookup
    let mut pawn_position_map: Map<Pos, PawnState> = Map::new(env);
    for pawn in game_state.pawns.iter() {
        if pawn.alive {
            pawn_position_map.set(pawn.pos, pawn);
        }
    }

    
    // Collect all pawns that can make forward moves and all pawns that can make any moves
    let mut forward_movable_pawns = Vec::new(env);
    let mut any_movable_pawns = Vec::new(env);
    
    for pawn in game_state.pawns.iter() {
        let (_, pawn_team) = Contract::decode_pawn_id(&pawn.pawn_id);
        
        // Skip if not our team or not alive
        if pawn_team != team || !pawn.alive {
            continue;
        }
        
        // Skip if pawn is unmovable (flag or bomb) - use provided team ranks, not game state
        if let Some(rank) = rank_map.get(pawn.pawn_id) {
            if rank == 0 || rank == 11 {
                continue;
            }
        }
        
        // Team check is legitimate - ensure we only move our own pieces
        let (_, pawn_team) = Contract::decode_pawn_id(&pawn.pawn_id);
        if pawn_team != team {
            continue; // Skip if pawn doesn't belong to this team
        }
        
        // Get adjacent positions (up, down, left, right)
        let adjacent_positions = Vec::from_array(env, [
            Pos { x: pawn.pos.x, y: pawn.pos.y + 1 }, // up
            Pos { x: pawn.pos.x, y: pawn.pos.y - 1 }, // down
            Pos { x: pawn.pos.x - 1, y: pawn.pos.y }, // left
            Pos { x: pawn.pos.x + 1, y: pawn.pos.y }, // right
        ]);
        
        let mut valid_moves = Vec::new(env);
        let mut forward_moves = Vec::new(env);
        
        for target_pos in adjacent_positions.iter() {
            // Check if position is within board bounds
            if target_pos.x < 0 || target_pos.x >= lobby_parameters.board.size.x ||
               target_pos.y < 0 || target_pos.y >= lobby_parameters.board.size.y {
                continue;
            }
            
            // Check if tile exists and is passable
            if let Some(tile) = tile_map.get(target_pos) {
                if !tile.passable {
                    continue;
                }
            } else {
                continue;
            }
            
            // Check if position is occupied by same team pawn
            if let Some(occupying_pawn) = pawn_position_map.get(target_pos) {
                let (_, occupying_team) = Contract::decode_pawn_id(&occupying_pawn.pawn_id);
                if occupying_team == team {
                    continue; // Skip if occupied by same team
                }
            }
            
            // This is a valid move
            valid_moves.push_back(target_pos);
            
            // Check if this is a "forward" move
            let is_forward = match team {
                0 => target_pos.y > pawn.pos.y, // Team 0 moves up (increasing y)
                1 => target_pos.y < pawn.pos.y, // Team 1 moves down (decreasing y)
                _ => false,
            };
            
            if is_forward {
                forward_moves.push_back(target_pos);
            }
        }
        
        // If this pawn has valid moves, add it to appropriate lists
        if !valid_moves.is_empty() {
            any_movable_pawns.push_back((pawn.clone(), valid_moves.clone()));
            
            if !forward_moves.is_empty() {
                forward_movable_pawns.push_back((pawn.clone(), forward_moves));
            }
        }
    }
    
    // Prioritize forward-moving pawns, but fall back to any movable pawn
    let (selected_pawn, available_moves) = if !forward_movable_pawns.is_empty() {
        // Select a forward-movable pawn pseudorandomly
        let pawn_index = (salt as usize) % forward_movable_pawns.len() as usize;
        forward_movable_pawns.get(pawn_index as u32).unwrap()
    } else if !any_movable_pawns.is_empty() {
        // Select any movable pawn pseudorandomly
        let pawn_index = (salt as usize) % any_movable_pawns.len() as usize;
        any_movable_pawns.get(pawn_index as u32).unwrap()
    } else {
        // No movable pawns found
        return None;
    };
    
    // Select a move for the chosen pawn pseudorandomly
    let move_index = ((salt >> 8) as usize) % available_moves.len() as usize;
    let target_pos = available_moves.get(move_index as u32).unwrap();
    
    // Double-check the move is valid by re-verifying no same-team pawn is at target
    if let Some(occupying_pawn) = pawn_position_map.get(target_pos) {
        let (_, occupying_team) = Contract::decode_pawn_id(&occupying_pawn.pawn_id);
        if occupying_team == team {
            // This shouldn't happen, but if it does, return None instead of an invalid move
            return None;
        }
    }
    
    Some(HiddenMove {
        pawn_id: selected_pawn.pawn_id,
        start_pos: selected_pawn.pos,
        target_pos,
        salt,
    })
}

// endregion

// region board and lobby creation

const DEFAULT_MAX_RANKS: [u32; 12] = [
    1,  // Flag (rank 0)
    1,  // Assassin/Spy (rank 1)
    8,  // Scout (rank 2)
    5,  // Miner (rank 3)
    4,  // Sergeant (rank 4)
    4,  // Lieutenant (rank 5)
    4,  // Captain (rank 6)
    3,  // Major (rank 7)
    2,  // Colonel (rank 8)
    1,  // General (rank 9)
    1,  // Marshal (rank 10)
    6,  // Bomb (rank 11)
];

pub fn create_default_board(env: &Env) -> Board {
    let mut tiles = Vec::new(env);
    
    // Team 0 setup positions (bottom 4 rows, y=0-3)
    // Row 0 (bottom row)
    let tile_data = [
        (0, 0, true, 0), (1, 0, true, 0), (2, 0, true, 0), (3, 0, true, 0), (4, 0, true, 0),
        (5, 0, true, 0), (6, 0, true, 0), (7, 0, true, 0), (8, 0, true, 0), (9, 0, true, 0),
        // Row 1
        (0, 1, true, 0), (1, 1, true, 0), (2, 1, true, 0), (3, 1, true, 0), (4, 1, true, 0),
        (5, 1, true, 0), (6, 1, true, 0), (7, 1, true, 0), (8, 1, true, 0), (9, 1, true, 0),
        // Row 2
        (0, 2, true, 0), (1, 2, true, 0), (2, 2, true, 0), (3, 2, true, 0), (4, 2, true, 0),
        (5, 2, true, 0), (6, 2, true, 0), (7, 2, true, 0), (8, 2, true, 0), (9, 2, true, 0),
        // Row 3
        (0, 3, true, 0), (1, 3, true, 0), (2, 3, true, 0), (3, 3, true, 0), (4, 3, true, 0),
        (5, 3, true, 0), (6, 3, true, 0), (7, 3, true, 0), (8, 3, true, 0), (9, 3, true, 0),
        
        // Neutral middle rows (y=4-5)
        // Row 4 - with lakes
        (0, 4, true, 2), (1, 4, true, 2), (2, 4, false, 2), (3, 4, false, 2), (4, 4, true, 2),
        (5, 4, true, 2), (6, 4, false, 2), (7, 4, false, 2), (8, 4, true, 2), (9, 4, true, 2),
        
        // Row 5 - with lakes
        (0, 5, true, 2), (1, 5, true, 2), (2, 5, false, 2), (3, 5, false, 2), (4, 5, true, 2),
        (5, 5, true, 2), (6, 5, false, 2), (7, 5, false, 2), (8, 5, true, 2), (9, 5, true, 2),
        
        // Team 1 setup positions (top 4 rows, y=6-9)
        // Row 6
        (0, 6, true, 1), (1, 6, true, 1), (2, 6, true, 1), (3, 6, true, 1), (4, 6, true, 1),
        (5, 6, true, 1), (6, 6, true, 1), (7, 6, true, 1), (8, 6, true, 1), (9, 6, true, 1),
        
        // Row 7
        (0, 7, true, 1), (1, 7, true, 1), (2, 7, true, 1), (3, 7, true, 1), (4, 7, true, 1),
        (5, 7, true, 1), (6, 7, true, 1), (7, 7, true, 1), (8, 7, true, 1), (9, 7, true, 1),
        
        // Row 8
        (0, 8, true, 1), (1, 8, true, 1), (2, 8, true, 1), (3, 8, true, 1), (4, 8, true, 1),
        (5, 8, true, 1), (6, 8, true, 1), (7, 8, true, 1), (8, 8, true, 1), (9, 8, true, 1),
        
        // Row 9 (top row)
        (0, 9, true, 1), (1, 9, true, 1), (2, 9, true, 1), (3, 9, true, 1), (4, 9, true, 1),
        (5, 9, true, 1), (6, 9, true, 1), (7, 9, true, 1), (8, 9, true, 1), (9, 9, true, 1),
    ];
    for (x, y, passable, setup) in tile_data.iter() {
        tiles.push_back(Tile {
            pos: Pos { x: *x, y: *y },
            passable: *passable,
            setup: *setup,
            setup_zone: 1,
        });
    }
    let mut packed_tiles = Vec::new(env);
    for tile in tiles.iter() {
        packed_tiles.push_back(Contract::pack_tile(&tile));
    }
    Board {
        name: String::from_str(env, "Default Stratego Board"),
        hex: false,
        size: Pos { x: 10, y: 10 },
        tiles: packed_tiles,
    }
}

pub fn create_test_lobby_parameters(env: &Env) -> LobbyParameters {
    let board_hash = BytesN::from_array(env, &[1u8; 32]);
    let board = create_default_board(env);
    
    LobbyParameters {
        board_hash,
        board,
        dev_mode: true,
        host_team: 0,
        max_ranks: Vec::from_array(env, DEFAULT_MAX_RANKS),
        must_fill_all_tiles: false,
        security_mode: false,
    }
}

pub fn create_invalid_board_parameters(env: &Env) -> LobbyParameters {
    let board_hash = BytesN::from_array(env, &[1u8; 32]);
    
    let tiles = Vec::from_array(env, [
        Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 1 },
        Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 0, setup_zone: 1 },
        Tile { pos: Pos { x: 0, y: 1 }, passable: true, setup: 1, setup_zone: 1 },
    ]);
    let mut packed_tiles = Vec::new(env);
    for tile in tiles.iter() {
        packed_tiles.push_back(Contract::pack_tile(&tile));
    }
    let board = Board {
        name: String::from_str(env, "Invalid Board"),
        tiles: packed_tiles,
        hex: false,
        size: Pos { x: 2, y: 2 }, // Says 2x2 = 4 tiles, but we only have 3
    };
    
    LobbyParameters {
        board_hash,
        board,
        dev_mode: true,
        host_team: 0,
        max_ranks: Vec::from_array(env, [1u32]),
        must_fill_all_tiles: false,
        security_mode: false,
    }
}

pub fn create_full_stratego_board_parameters(env: &Env) -> LobbyParameters {
    let board_hash = BytesN::from_array(env, &[
        0xef, 0x3b, 0x53, 0x2a, 0x3e, 0x48, 0x1f, 0x29, 
        0x10, 0x89, 0x91, 0xac, 0x07, 0xbf, 0xeb, 0xd3, 
        0xbb, 0x0f, 0xc4, 0x41, 0xb2, 0xa7, 0xb9, 0xe4, 
        0x7c, 0x99, 0xc0, 0xe6, 0xff, 0x8c, 0x8f, 0x78
    ]);
    
    let mut board = create_default_board(env);
    board.name = String::from_str(env, "Full Stratego Board");
    
    LobbyParameters {
        board_hash,
        board,
        dev_mode: false,
        host_team: 0,
        max_ranks: Vec::from_array(env, DEFAULT_MAX_RANKS),
        must_fill_all_tiles: true,
        security_mode: true,
    }
}

// endregion

// region setup generation

pub fn create_setup_commits_from_game_state(env: &Env, lobby_id: u32, team: u32) -> (Vec<SetupCommit>, Setup, u64, Vec<HiddenRank>) {
    let game_state_key = DataKey::GameState(lobby_id);
    let game_state: GameState = env.storage()
        .temporary()
        .get(&game_state_key)
        .expect("Game state should exist");
    
    let mut setup_commits = Vec::new(env);
    let mut team_pawns = Vec::new(env);
    
    for pawn in game_state.pawns.iter() {
        let (_, pawn_team) = Contract::decode_pawn_id(&pawn.pawn_id);
        if pawn_team == team {
            team_pawns.push_back(pawn);
        }
    }
    
    let lobby_parameters_key = DataKey::LobbyParameters(lobby_id);
    let lobby_parameters: LobbyParameters = env.storage()
        .temporary()
        .get(&lobby_parameters_key)
        .expect("Lobby parameters should exist");
    
    // Create rank distribution - separate flags/bombs from movable pieces
    let mut back_ranks = Vec::new(env);   // Flags and bombs for back row
    let mut front_ranks = Vec::new(env);  // All other movable pieces
    
    for (rank, count) in lobby_parameters.max_ranks.iter().enumerate() {
        let rank_u32 = rank as u32;
        for _ in 0..count {
            if rank_u32 == 0 || rank_u32 == 11 {  // Flag or Bomb - always in back
                back_ranks.push_back(rank_u32);
            } else {
                front_ranks.push_back(rank_u32);
            }
        }
    }
    
    // Sort pawns by position to ensure consistent placement
    // For team 0 (host): y=0,1,2,3 where y=0 is back row
    // For team 1 (guest): y=9,8,7,6 where y=9 is back row
    let mut pawn_vec: std::vec::Vec<PawnState> = std::vec::Vec::new();
    for pawn in team_pawns.iter() {
        pawn_vec.push(pawn.clone());
    }
    
    if team == 0 {
        // Host team: sort by y ascending (0,1,2,3) so back row (y=0) comes first
        pawn_vec.sort_by_key(|pawn| pawn.pos.y);
    } else {
        // Guest team: sort by y descending (9,8,7,6) so back row (y=9) comes first  
        pawn_vec.sort_by_key(|pawn| -pawn.pos.y);
    }
    
    let salt = team as u64;
    let mut hidden_ranks = Vec::new(env);
    
    // Assign ranks: flags/bombs to back positions, others to front
    let back_count = back_ranks.len() as usize;
    
    // No randomization - use ranks in order for consistent testing
    for (i, pawn) in pawn_vec.iter().enumerate() {
        let rank = if i < back_count {
            // Assign flags and bombs to back positions (first positions in sorted order)
            back_ranks.get(i as u32).unwrap_or(11u32) // Fallback to bomb if somehow out of bounds
        } else {
            // Assign movable ranks to front positions
            let front_index = i - back_count;
            front_ranks.get(front_index as u32).unwrap_or(4u32) // Fallback to rank 4 (Sergeant)
        };
        
        let hidden_rank = HiddenRank {
            pawn_id: pawn.pawn_id,
            rank,
            salt: pawn.pawn_id as u64,
        };
        hidden_ranks.push_back(hidden_rank.clone());
        let serialized_hidden_rank = hidden_rank.clone().to_xdr(env);
        let full_hash = env.crypto().sha256(&serialized_hidden_rank).to_bytes().to_array();
        let hidden_rank_hash = HiddenRankHash::from_array(env, &full_hash[0..16].try_into().unwrap());
        
        let commit = SetupCommit {
            pawn_id: pawn.pawn_id,
            hidden_rank_hash,
        };
        setup_commits.push_back(commit);
    }
    
    let setup_proof = Setup {
        setup_commits: setup_commits.clone(),
        salt,
    };
    
    (setup_commits, setup_proof, salt, hidden_ranks)
}

pub fn create_deterministic_setup(env: &Env, team: u32, seed: u64) -> (Vec<SetupCommit>, Setup, u64, Vec<HiddenRank>) {
    let mut setup_commits = Vec::new(env);
    let mut hidden_ranks = Vec::new(env);
    
    // Create rank distribution - separate flags/bombs from movable pieces
    let rank_counts = DEFAULT_MAX_RANKS;
    let mut back_ranks = Vec::new(env);   // Flags and bombs for back row
    let mut front_ranks = Vec::new(env);  // All other movable pieces
    
    for (rank, count) in rank_counts.iter().enumerate() {
        let rank_u32 = rank as u32;
        for _ in 0..*count {
            if rank_u32 == 0 || rank_u32 == 11 {  // Flag or Bomb - always in back
                back_ranks.push_back(rank_u32);
            } else {
                front_ranks.push_back(rank_u32);
            }
        }
    }
    
    // Generate pawn positions for this team, sorted back-to-front
    // Team 0: rows 0-3, Team 1: rows 6-9
    let mut team_positions = Vec::new(env);
    let start_row = if team == 0 { 0 } else { 6 };
    let end_row = if team == 0 { 3 } else { 9 };
    
    // Sort positions so back row comes first
    if team == 0 {
        // Host team: sort by y ascending (0,1,2,3) so back row (y=0) comes first
        for y in start_row..=end_row {
            for x in 0..10 {
                team_positions.push_back(Pos { x, y });
            }
        }
    } else {
        // Guest team: sort by y descending (9,8,7,6) so back row (y=9) comes first
        for y in (start_row..=end_row).rev() {
            for x in 0..10 {
                team_positions.push_back(Pos { x, y });
            }
        }
    }
    
    let back_count = back_ranks.len() as usize;
    
    // No randomization - assign ranks deterministically with flags/bombs in back
    for (i, pos) in team_positions.iter().enumerate() {
        if i >= (back_ranks.len() + front_ranks.len()) as usize {
            break; // Only assign as many ranks as we have
        }
        
        let rank = if i < back_count {
            // Assign flags and bombs to back positions (first positions in sorted order)
            back_ranks.get(i as u32).unwrap_or(11u32) // Fallback to bomb if somehow out of bounds
        } else {
            // Assign movable ranks to front positions
            let front_index = i - back_count;
            front_ranks.get(front_index as u32).unwrap_or(4u32) // Fallback to rank 4 (Sergeant)
        };
        
        let pawn_id = Contract::encode_pawn_id(&pos, &team);
        
        let hidden_rank = HiddenRank {
            pawn_id,
            rank,
            salt: pawn_id as u64,
        };
        hidden_ranks.push_back(hidden_rank.clone());
        
        let serialized_hidden_rank = hidden_rank.clone().to_xdr(env);
        let full_hash = env.crypto().sha256(&serialized_hidden_rank).to_bytes().to_array();
        let hidden_rank_hash = HiddenRankHash::from_array(env, &full_hash[0..16].try_into().unwrap());
        
        let commit = SetupCommit {
            pawn_id,
            hidden_rank_hash,
        };
        setup_commits.push_back(commit);
    }
    
    let setup_proof = Setup {
        setup_commits: setup_commits.clone(),
        salt: team as u64,
    };
    
    (setup_commits, setup_proof, team as u64, hidden_ranks)
}

// endregion

// region game comparison

/// Compares all pawn states between two games to verify they're identical
/// This is used to validate that populated vs unpopulated games converge to the same state
/// Takes the specific game states it needs rather than accessing storage
pub fn verify_pawn_states_identical(game_state_a: &GameState, game_state_b: &GameState) -> bool {
    // First check: same number of pawns
        if game_state_a.pawns.len() != game_state_b.pawns.len() {
            std::println!("   ❌ Different number of pawns: A={}, B={}", game_state_a.pawns.len(), game_state_b.pawns.len());
            return false;
        }
        
        // Create maps for quick lookup by pawn_id
        let mut pawns_a = std::collections::HashMap::new();
        let mut pawns_b = std::collections::HashMap::new();
        
        for pawn in game_state_a.pawns.iter() {
            pawns_a.insert(pawn.pawn_id, pawn);
        }
        
        for pawn in game_state_b.pawns.iter() {
            pawns_b.insert(pawn.pawn_id, pawn);
        }
        
        let mut differences_found = false;
        
        // Compare each pawn
        for (pawn_id, pawn_a) in pawns_a.iter() {
            match pawns_b.get(pawn_id) {
                Some(pawn_b) => {
                    // Compare position
                    if pawn_a.pos.x != pawn_b.pos.x || pawn_a.pos.y != pawn_b.pos.y {
                        let (_, team) = Contract::decode_pawn_id(&pawn_id);
                        std::println!("   ❌ Pawn {} (team {}) position differs: A=({},{}) vs B=({},{})", 
                                     pawn_id, team, pawn_a.pos.x, pawn_a.pos.y, pawn_b.pos.x, pawn_b.pos.y);
                        differences_found = true;
                    }
                    
                    // Compare alive status
                    if pawn_a.alive != pawn_b.alive {
                        let (_, team) = Contract::decode_pawn_id(&pawn_id);
                        std::println!("   ❌ Pawn {} (team {}) alive status differs: A={} vs B={}", 
                                     pawn_id, team, pawn_a.alive, pawn_b.alive);
                        differences_found = true;
                    }
                    
                    // Compare revealed ranks - only flag mismatches in ranks that should be revealed through gameplay
                    // Game A (unpopulated) starts with all ranks hidden, Game B (populated) starts with all ranks visible
                    // We only care about ranks that have been revealed through collision resolution in Game A
                    let rank_a = if pawn_a.rank.is_empty() { None } else { Some(pawn_a.rank.get(0).unwrap()) };
                    let rank_b = if pawn_b.rank.is_empty() { None } else { Some(pawn_b.rank.get(0).unwrap()) };
                    
                    // Only compare ranks if Game A has a revealed rank (meaning it was revealed through gameplay)
                    // If Game A has no rank but Game B does, that's expected (B is pre-populated)
                    if rank_a.is_some() && rank_a != rank_b {
                        let (_, team) = Contract::decode_pawn_id(&pawn_id);
                        std::println!("   ❌ Pawn {} (team {}) revealed rank differs: A={:?} vs B={:?}", 
                                     pawn_id, team, rank_a, rank_b);
                        differences_found = true;
                    }
                    
                    // Compare moved status
                    if pawn_a.moved != pawn_b.moved {
                        let (_, team) = Contract::decode_pawn_id(&pawn_id);
                        std::println!("   ❌ Pawn {} (team {}) moved status differs: A={} vs B={}", 
                                     pawn_id, team, pawn_a.moved, pawn_b.moved);
                        differences_found = true;
                    }
                    
                    // Compare moved_scout status
                    if pawn_a.moved_scout != pawn_b.moved_scout {
                        let (_, team) = Contract::decode_pawn_id(&pawn_id);
                        std::println!("   ❌ Pawn {} (team {}) moved_scout status differs: A={} vs B={}", 
                                     pawn_id, team, pawn_a.moved_scout, pawn_b.moved_scout);
                        differences_found = true;
                    }
                },
                None => {
                    let (_, team) = Contract::decode_pawn_id(&pawn_id);
                    std::println!("   ❌ Pawn {} (team {}) exists in Game A but not in Game B", pawn_id, team);
                    differences_found = true;
                }
            }
        }
        
        // Check for pawns that exist in B but not in A
        for (pawn_id, _) in pawns_b.iter() {
            if !pawns_a.contains_key(pawn_id) {
                let (_, team) = Contract::decode_pawn_id(&pawn_id);
                std::println!("   ❌ Pawn {} (team {}) exists in Game B but not in Game A", pawn_id, team);
                differences_found = true;
            }
        }
        
        if !differences_found {
            // Count some statistics for verification
            let mut alive_count_a = 0;
            let mut dead_count_a = 0;
            let mut revealed_count_a = 0;
            let mut revealed_count_b = 0;
            
            for pawn in game_state_a.pawns.iter() {
                if pawn.alive {
                    alive_count_a += 1;
                } else {
                    dead_count_a += 1;
                }
                if !pawn.rank.is_empty() {
                    revealed_count_a += 1;
                }
            }
            
            for pawn in game_state_b.pawns.iter() {
                if !pawn.rank.is_empty() {
                    revealed_count_b += 1;
                }
            }
            
            std::println!("   ✅ All pawn states identical: {} alive, {} dead, {} ranks revealed in A, {} total ranks in B", 
                         alive_count_a, dead_count_a, revealed_count_a, revealed_count_b);
        }
        
    !differences_found
}

// endregion

// region storage snapshot utilities

/// Snapshot of phase and subphase information
pub struct SnapshotPhase {
    pub phase: Phase,
    pub subphase: Subphase,
}

/// Snapshot of lobby parameters and info (without game state)
pub struct SnapshotLobby {
    pub lobby_parameters: LobbyParameters,
    pub lobby_info: LobbyInfo,
}

/// Full snapshot of lobby state including parameters, info, and game state
pub struct SnapshotFull {
    pub lobby_parameters: LobbyParameters,
    pub lobby_info: LobbyInfo,
    pub game_state: GameState,
}

/// Extract phase and subphase from storage for a given lobby
pub fn extract_phase_snapshot(env: &Env, contract_id: &Address, lobby_id: u32) -> SnapshotPhase {
    env.as_contract(contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let lobby_info: LobbyInfo = env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        SnapshotPhase {
            phase: lobby_info.phase,
            subphase: lobby_info.subphase,
        }
    })
}

/// Extract lobby parameters and info from storage (without game state)
pub fn extract_lobby_snapshot(env: &Env, contract_id: &Address, lobby_id: u32) -> SnapshotLobby {
    env.as_contract(contract_id, || {
        let lobby_parameters_key = DataKey::LobbyParameters(lobby_id);
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        
        let lobby_parameters: LobbyParameters = env.storage()
            .temporary()
            .get(&lobby_parameters_key)
            .expect("Lobby parameters should exist");
        let lobby_info: LobbyInfo = env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        SnapshotLobby {
            lobby_parameters,
            lobby_info,
        }
    })
}

/// Extract full lobby state from storage for a given lobby
pub fn extract_full_snapshot(env: &Env, contract_id: &Address, lobby_id: u32) -> SnapshotFull {
    env.as_contract(contract_id, || {
        let lobby_parameters_key = DataKey::LobbyParameters(lobby_id);
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let game_state_key = DataKey::GameState(lobby_id);
        
        let lobby_parameters: LobbyParameters = env.storage()
            .temporary()
            .get(&lobby_parameters_key)
            .expect("Lobby parameters should exist");
        let lobby_info: LobbyInfo = env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        let game_state: GameState = env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        SnapshotFull {
            lobby_parameters,
            lobby_info,
            game_state,
        }
    })
}

// endregion 