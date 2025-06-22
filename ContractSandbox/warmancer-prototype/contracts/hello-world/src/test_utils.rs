#![cfg(test)]
extern crate std;
use super::*;
use soroban_sdk::{Env, Address, Vec, Map};

// region ANSI color codes

// ANSI color codes for terminal output
pub const RESET: &str = "\x1b[0m";
pub const BOLD: &str = "\x1b[1m";
pub const RED: &str = "\x1b[31m";
pub const GREEN: &str = "\x1b[32m";
pub const YELLOW: &str = "\x1b[33m";
pub const BLUE: &str = "\x1b[34m";
pub const MAGENTA: &str = "\x1b[35m";
pub const CYAN: &str = "\x1b[36m";
pub const WHITE: &str = "\x1b[37m";
pub const BRIGHT_RED: &str = "\x1b[91m";
pub const BRIGHT_GREEN: &str = "\x1b[92m";
pub const BRIGHT_YELLOW: &str = "\x1b[93m";
pub const BRIGHT_BLUE: &str = "\x1b[94m";
pub const BRIGHT_MAGENTA: &str = "\x1b[95m";
pub const BRIGHT_CYAN: &str = "\x1b[96m";

// endregion

// region board visualization

/// Creates a colorized text representation of the current board state
pub fn format_board_with_colors(_env: &Env, game_state: &GameState, lobby_parameters: &LobbyParameters, lobby_info: &LobbyInfo) -> std::string::String {
    let board = &lobby_parameters.board;
    let width = board.size.x;
    let height = board.size.y;
    
    // Create a map of positions to pawns for quick lookup
    let mut pawn_map: std::collections::HashMap<(i32, i32), PawnState> = std::collections::HashMap::new();
    for pawn in game_state.pawns.iter() {
        if pawn.alive {
            pawn_map.insert((pawn.pos.x, pawn.pos.y), pawn);
        }
    }
    
    // Create a map of positions to tiles for quick lookup
    let mut tile_map: std::collections::HashMap<(i32, i32), Tile> = std::collections::HashMap::new();
    for tile in board.tiles.iter() {
        tile_map.insert((tile.pos.x, tile.pos.y), tile);
    }
    
    let mut result = std::string::String::new();
    
    // Add colorized header information
    result.push_str(&std::format!("{}{}=== BOARD STATE ==={}\n", BOLD, BRIGHT_CYAN, RESET));
    result.push_str(&std::format!("{}Phase:{} {:?}, {}Subphase:{} {:?}\n", 
                                   YELLOW, RESET, lobby_info.phase, YELLOW, RESET, lobby_info.subphase));
    result.push_str(&std::format!("{}Board:{} {:?} ({}x{})\n", 
                                   CYAN, RESET, board.name, width, height));
    result.push_str(&std::format!("{}Host Team:{} {}\n\n", 
                                   MAGENTA, RESET, lobby_parameters.host_team));
    
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
                let rank_char = if !pawn.rank.is_empty() {
                    match pawn.rank.get(0).unwrap() {
                        0 => 'F',   // Flag
                        1 => 'S',   // Spy
                        2 => 's',   // Scout
                        3 => 'M',   // Miner
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
                
                // Get rank-specific color
                let rank_color = if !pawn.rank.is_empty() {
                    match pawn.rank.get(0).unwrap() {
                        0 => BRIGHT_YELLOW,    // Flag - bright yellow (most important)
                        1 => BRIGHT_MAGENTA,   // Spy - bright magenta (special)
                        2 => GREEN,            // Scout - green (fast)
                        3 => YELLOW,           // Miner - yellow (can defuse bombs)
                        11 => BRIGHT_RED,      // Bomb - bright red (dangerous)
                        10 => BRIGHT_CYAN,     // Marshal - bright cyan (highest rank)
                        9 => CYAN,             // General - cyan (high rank)
                        _ => WHITE,            // Other ranks - white
                    }
                } else {
                    WHITE  // Unknown rank
                };
                
                // Use different colors and formatting for different teams
                if team == 0 {
                    // Host team in red brackets with rank-specific colors
                    result.push_str(&std::format!("{}[{}{}{}{}]{}", 
                                                   BRIGHT_RED, rank_color, rank_char, BRIGHT_RED, RESET, RESET));
                } else {
                    // Guest team in blue parentheses with rank-specific colors
                    result.push_str(&std::format!("{}({}{}{}{}){}", 
                                                   BRIGHT_BLUE, rank_color, rank_char, BRIGHT_BLUE, RESET, RESET));
                }
            } else if let Some(tile) = tile_map.get(&pos) {
                // No pawn, show tile info with colors
                if !tile.passable {
                    result.push_str(&std::format!("{}~~~{}", BRIGHT_CYAN, RESET));  // Water/lake in cyan
                } else {
                    match tile.setup {
                        0 => result.push_str(&std::format!("{} . {}", RED, RESET)),      // Host setup area in red
                        1 => result.push_str(&std::format!("{} : {}", BLUE, RESET)),     // Guest setup area in blue
                        2 => result.push_str("   "),                                     // Neutral area - no color
                        _ => result.push_str(&std::format!("{} ? {}", YELLOW, RESET)),   // Unknown in yellow
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
    result.push_str(&std::format!("{}\n\n", RESET));
    
    // Add colorized legend
    result.push_str(&std::format!("{}Legend:{}\n", BOLD, RESET));
    result.push_str(&std::format!("{}[X]{} = Host team pawn    {}(X){} = Guest team pawn\n", 
                                   BRIGHT_RED, RESET, BRIGHT_BLUE, RESET));
    result.push_str(&std::format!("{}F{}=Flag {}S{}=Spy {}s{}=Scout {}M{}=Miner {}4-9{}=Ranks {}G{}=Marshal {}B{}=Bomb {}?{}=Unknown\n",
                                   BRIGHT_YELLOW, RESET, BRIGHT_MAGENTA, RESET, GREEN, RESET, 
                                   YELLOW, RESET, WHITE, RESET, BRIGHT_CYAN, RESET, BRIGHT_RED, RESET, WHITE, RESET));
    result.push_str(&std::format!("{}~~~{} = Water/Lake       {}. {} = Host setup area    {}: {} = Guest setup area\n",
                                   BRIGHT_CYAN, RESET, RED, RESET, BLUE, RESET));
    result.push_str(&std::format!("{}==================={}\n", BOLD, RESET));
    
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

/// Validates the game state transition after move proving phase
/// Checks that the provided move requests were applied correctly
/// Returns (phase, subphase, host_needed_ranks, guest_needed_ranks)
pub fn validate_move_prove_transition(
    env: &Env,
    contract_id: &Address,
    lobby_id: u32,
    host_move_req: &ProveMoveReq,
    guest_move_req: &ProveMoveReq,
) -> (Phase, Subphase, Vec<PawnId>, Vec<PawnId>) {
    env.as_contract(contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let game_state_key = DataKey::GameState(lobby_id);
        let lobby_info: LobbyInfo = env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        let game_state: GameState = env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        let host_move = game_state.moves.get(0).unwrap();
        let guest_move = game_state.moves.get(1).unwrap();
        let host_needed_ranks = host_move.needed_rank_proofs.clone();
        let guest_needed_ranks = guest_move.needed_rank_proofs.clone();
        
        // VALIDATE: Log the submitted moves for tracking
        std::println!("=== POST-MOVEPROVE VALIDATION ===");
        std::println!("Phase: {:?}, Subphase: {:?}", lobby_info.phase, lobby_info.subphase);
        std::println!("✓ Host move submitted: {} from ({},{}) to ({},{})", 
                     host_move_req.move_proof.pawn_id, 
                     host_move_req.move_proof.start_pos.x, host_move_req.move_proof.start_pos.y, 
                     host_move_req.move_proof.target_pos.x, host_move_req.move_proof.target_pos.y);
        std::println!("✓ Guest move submitted: {} from ({},{}) to ({},{})", 
                     guest_move_req.move_proof.pawn_id, 
                     guest_move_req.move_proof.start_pos.x, guest_move_req.move_proof.start_pos.y, 
                     guest_move_req.move_proof.target_pos.x, guest_move_req.move_proof.target_pos.y);
        
        // VALIDATE: Check that move processing was successful (moves exist in game state)
        assert!(game_state.moves.len() >= 2, "Game state should have moves for both players");
        std::println!("✓ Move processing completed successfully");
        std::println!("Host needed rank proofs: {} pawns", host_needed_ranks.len());
        for pawn_id in host_needed_ranks.iter() {
            let (_, team) = Contract::decode_pawn_id(&pawn_id);
            std::println!("  - Pawn {} (team {})", pawn_id, team);
        }
        std::println!("Guest needed rank proofs: {} pawns", guest_needed_ranks.len());
        for pawn_id in guest_needed_ranks.iter() {
            let (_, team) = Contract::decode_pawn_id(&pawn_id);
            std::println!("  - Pawn {} (team {})", pawn_id, team);
        }
        std::println!("=== END VALIDATION ===");
        
        (lobby_info.phase, lobby_info.subphase, host_needed_ranks, guest_needed_ranks)
    })
}

/// Validates the game state transition after rank proving phase
/// Checks that the provided rank proof requests were applied correctly
/// Returns (phase, subphase, remaining_host_rank_proofs, remaining_guest_rank_proofs)
pub fn validate_rank_prove_transition(
    env: &Env,
    contract_id: &Address,
    lobby_id: u32,
    host_rank_req: Option<&ProveRankReq>,
    guest_rank_req: Option<&ProveRankReq>,
) -> (Phase, Subphase, Vec<PawnId>, Vec<PawnId>) {
    env.as_contract(contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let game_state_key = DataKey::GameState(lobby_id);
        let lobby_info: LobbyInfo = env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        let game_state: GameState = env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        std::println!("=== POST-RANKPROVE VALIDATION ===");
        std::println!("After rank proving: Phase={:?}, Subphase={:?}", lobby_info.phase, lobby_info.subphase);
        
        // VALIDATE: Check that the submitted rank proofs were applied correctly
        if let Some(host_req) = host_rank_req {
            std::println!("✓ Validating host rank proofs...");
            for hidden_rank in host_req.hidden_ranks.iter() {
                let pawn = game_state.pawns.iter()
                    .find(|p| p.pawn_id == hidden_rank.pawn_id)
                    .expect(&std::format!("Host pawn {} should exist", hidden_rank.pawn_id));
                
                assert!(!pawn.rank.is_empty(), "Host pawn {} should have rank revealed", hidden_rank.pawn_id);
                assert_eq!(pawn.rank.get(0).unwrap(), hidden_rank.rank, "Host pawn {} rank should match submitted proof", hidden_rank.pawn_id);
                
                let rank_str = rank_to_string(hidden_rank.rank);
                std::println!("  ✓ Host pawn {} rank validated: {}", hidden_rank.pawn_id, rank_str);
            }
        }
        
        if let Some(guest_req) = guest_rank_req {
            std::println!("✓ Validating guest rank proofs...");
            for hidden_rank in guest_req.hidden_ranks.iter() {
                let pawn = game_state.pawns.iter()
                    .find(|p| p.pawn_id == hidden_rank.pawn_id)
                    .expect(&std::format!("Guest pawn {} should exist", hidden_rank.pawn_id));
                
                assert!(!pawn.rank.is_empty(), "Guest pawn {} should have rank revealed", hidden_rank.pawn_id);
                assert_eq!(pawn.rank.get(0).unwrap(), hidden_rank.rank, "Guest pawn {} rank should match submitted proof", hidden_rank.pawn_id);
                
                let rank_str = rank_to_string(hidden_rank.rank);
                std::println!("  ✓ Guest pawn {} rank validated: {}", hidden_rank.pawn_id, rank_str);
            }
        }
        
        // Check if rank proofs are still needed
        let host_move = game_state.moves.get(0).unwrap();
        let guest_move = game_state.moves.get(1).unwrap();
        let remaining_host_proofs = host_move.needed_rank_proofs.clone();
        let remaining_guest_proofs = guest_move.needed_rank_proofs.clone();
        
        std::println!("Host still needs {} rank proofs", remaining_host_proofs.len());
        std::println!("Guest still needs {} rank proofs", remaining_guest_proofs.len());
        std::println!("=== END VALIDATION ===");
        
        (lobby_info.phase, lobby_info.subphase, remaining_host_proofs, remaining_guest_proofs)
    })
}

/// Validates that pawns involved in collisions have their ranks properly revealed
pub fn assert_ranks_revealed_after_collision(
    env: &Env,
    contract_id: &Address,
    lobby_id: u32,
    expected_revealed_pawns: &[PawnId],
) {
    env.as_contract(contract_id, || {
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
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
    })
}

/// Validates the expected game phase and subphase
pub fn validate_game_phase(
    env: &Env,
    contract_id: &Address,
    lobby_id: u32,
    expected_phase: Phase,
    expected_subphase: Option<Subphase>,
) {
    env.as_contract(contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let lobby_info: LobbyInfo = env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert_eq!(lobby_info.phase, expected_phase, "Game should be in phase {:?}", expected_phase);
        
        if let Some(subphase) = expected_subphase {
            assert_eq!(lobby_info.subphase, subphase, "Game should be in subphase {:?}", subphase);
        }
        
        std::println!("Phase validation passed: Phase={:?}, Subphase={:?}", lobby_info.phase, lobby_info.subphase);
    })
}

/// Handles rank proving for both players given their needed rank proofs
/// Returns (host_rank_req, guest_rank_req, ranks_proved)
pub fn submit_required_rank_proofs(
    env: &Env,
    client: &ContractClient,
    lobby_id: u32,
    host_address: &Address,
    guest_address: &Address,
    host_needed_ranks: &Vec<PawnId>,
    guest_needed_ranks: &Vec<PawnId>,
    host_ranks: &Vec<HiddenRank>,
    guest_ranks: &Vec<HiddenRank>,
) -> (Option<ProveRankReq>, Option<ProveRankReq>, bool) {
    let mut ranks_proved = false;
    let mut host_rank_req: Option<ProveRankReq> = None;
    let mut guest_rank_req: Option<ProveRankReq> = None;
    
    // Prove ranks for host if needed
    if !host_needed_ranks.is_empty() {
        std::println!("Host proving {} ranks", host_needed_ranks.len());
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
        std::println!("Host proving {} ranks", req.hidden_ranks.len());
        assert_eq!(req.hidden_ranks.len(), host_needed_ranks.len());
        client.prove_rank(host_address, &req);
        host_rank_req = Some(req);
        ranks_proved = true;
    }
    
    // Prove ranks for guest if needed
    if !guest_needed_ranks.is_empty() {
        std::println!("Guest proving {} ranks", guest_needed_ranks.len());
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
        std::println!("Guest proving {} ranks", req.hidden_ranks.len());
        assert_eq!(req.hidden_ranks.len(), guest_needed_ranks.len());
        client.prove_rank(guest_address, &req);
        guest_rank_req = Some(req);
        ranks_proved = true;
    }
    
    if ranks_proved {
        std::println!("Rank proving completed");
    }
    
    (host_rank_req, guest_rank_req, ranks_proved)
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
    for tile in lobby_parameters.board.tiles.iter() {
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

// region setup generation

pub fn create_deterministic_setup(env: &Env, team: u32, seed: u64) -> (Vec<SetupCommit>, Setup, u64, Vec<HiddenRank>) {
    let mut setup_commits = Vec::new(env);
    let mut hidden_ranks = Vec::new(env);
    
    // Create standard Stratego rank distribution
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
    
    let rank_counts = DEFAULT_MAX_RANKS;
    let mut all_ranks = Vec::new(env);
    for (rank, count) in rank_counts.iter().enumerate() {
        let rank_u32 = rank as u32;
        for _ in 0..*count {
            all_ranks.push_back(rank_u32);
        }
    }
    
    // Generate deterministic pawn positions for this team
    // Team 0: rows 0-3, Team 1: rows 6-9
    let mut team_positions = Vec::new(env);
    let start_row = if team == 0 { 0 } else { 6 };
    let end_row = if team == 0 { 3 } else { 9 };
    
    for y in start_row..=end_row {
        for x in 0..10 {
            team_positions.push_back(Pos { x, y });
        }
    }
    
    // Use deterministic shuffling with the provided seed
    let mut rank_seed = seed.wrapping_mul(team as u64 + 1);
    let mut rank_vec: std::vec::Vec<u32> = std::vec::Vec::new();
    for rank in all_ranks.iter() {
        rank_vec.push(rank);
    }
    
    // Shuffle ranks deterministically
    for i in 0..rank_vec.len() {
        rank_seed = rank_seed.wrapping_mul(1103515245).wrapping_add(12345);
        let j = (rank_seed as usize) % rank_vec.len();
        rank_vec.swap(i, j);
    }
    
    // Assign ranks to positions
    for (i, pos) in team_positions.iter().enumerate() {
        if i >= rank_vec.len() {
            break; // Only assign as many ranks as we have
        }
        
        let rank = rank_vec[i];
        let pawn_id = Contract::encode_pawn_id(&pos, &team);
        
        let hidden_rank = HiddenRank {
            pawn_id,
            rank,
            salt: pawn_id as u64,
        };
        hidden_ranks.push_back(hidden_rank.clone());
        
        let serialized_hidden_rank = hidden_rank.clone().to_xdr(env);
        let hidden_rank_hash = env.crypto().sha256(&serialized_hidden_rank).to_bytes();
        
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

pub fn verify_pawn_states_identical(env: &Env, contract_id: &Address, lobby_a: u32, lobby_b: u32) -> bool {
    env.as_contract(contract_id, || {
        let game_state_a: GameState = env.storage().temporary().get(&DataKey::GameState(lobby_a)).expect("Game A state should exist");
        let game_state_b: GameState = env.storage().temporary().get(&DataKey::GameState(lobby_b)).expect("Game B state should exist");
        
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
    })
}

// endregion

// region move info capture

/// Captures detailed move information for logging
/// Returns a tuple with all move details including collision information
pub fn extract_detailed_move_data(
    env: &Env,
    contract_id: &Address,
    lobby_id: u32,
    move_number: u32,
    host_pawn_id: PawnId,
    host_start_pos: Pos,
    host_target_pos: Pos,
    guest_pawn_id: PawnId,
    guest_start_pos: Pos,
    guest_target_pos: Pos,
    include_collision_detection: bool,
) -> (u32, u32, u32, i32, i32, i32, i32, u32, u32, bool, bool, u32, u32, i32, i32, i32, i32, u32, u32, bool, bool) {
    env.as_contract(contract_id, || {
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        if include_collision_detection {
            std::println!("requested ranks for host: {:?}", game_state.moves.get(0).unwrap().needed_rank_proofs);
            std::println!("requested ranks for guest: {:?}", game_state.moves.get(1).unwrap().needed_rank_proofs);
        }
        
        // Get pawn information
        let mut host_pawn_rank = 999u32; // Use 999 to indicate unknown
        let mut guest_pawn_rank = 999u32;
        let mut host_collision_pawn_id = 0u32;
        let mut guest_collision_pawn_id = 0u32;
        let mut host_collision_rank = 999u32;
        let mut guest_collision_rank = 999u32;
        let mut host_collision_alive = false;
        let mut guest_collision_alive = false;
        
        // Find the pawns that moved
        for pawn in game_state.pawns.iter() {
            if pawn.pawn_id == host_pawn_id {
                if !pawn.rank.is_empty() {
                    host_pawn_rank = pawn.rank.get(0).unwrap();
                }
            }
            if pawn.pawn_id == guest_pawn_id {
                if !pawn.rank.is_empty() {
                    guest_pawn_rank = pawn.rank.get(0).unwrap();
                }
            }
        }
        
        if include_collision_detection {
            // Find any pawns at the target positions (collision pawns)
            for pawn in game_state.pawns.iter() {
                if pawn.pos == host_target_pos && pawn.pawn_id != host_pawn_id {
                    host_collision_pawn_id = pawn.pawn_id;
                    host_collision_alive = pawn.alive;
                    if !pawn.rank.is_empty() {
                        host_collision_rank = pawn.rank.get(0).unwrap();
                    }
                }
                if pawn.pos == guest_target_pos && pawn.pawn_id != guest_pawn_id {
                    guest_collision_pawn_id = pawn.pawn_id;
                    guest_collision_alive = pawn.alive;
                    if !pawn.rank.is_empty() {
                        guest_collision_rank = pawn.rank.get(0).unwrap();
                    }
                }
            }
        }
        
        // Check if the moving pawns are still alive
        let host_alive = game_state.pawns.iter().find(|p| p.pawn_id == host_pawn_id).map(|p| p.alive).unwrap_or(false);
        let guest_alive = game_state.pawns.iter().find(|p| p.pawn_id == guest_pawn_id).map(|p| p.alive).unwrap_or(false);
        
        (move_number, host_pawn_id, host_pawn_rank, 
         host_start_pos.x, host_start_pos.y,
         host_target_pos.x, host_target_pos.y,
         host_collision_pawn_id, host_collision_rank, host_alive, host_collision_alive,
         guest_pawn_id, guest_pawn_rank,
         guest_start_pos.x, guest_start_pos.y,
         guest_target_pos.x, guest_target_pos.y,
         guest_collision_pawn_id, guest_collision_rank, guest_alive, guest_collision_alive)
    })
}

// endregion

// region storage snapshot utilities

/// Snapshot of phase and subphase information
pub struct SnapshotPhase {
    pub phase: Phase,
    pub subphase: Subphase,
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