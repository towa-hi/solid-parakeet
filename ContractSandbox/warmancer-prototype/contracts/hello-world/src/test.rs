#![cfg(test)]
extern crate std;
use super::*;
use super::test_utils::*;
use soroban_sdk::{Env, Address};
use soroban_sdk::testutils::Address as _;

// region test constants

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

fn create_default_board(env: &Env) -> Board {
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
        });
    }
    Board {
        name: String::from_str(env, "Default Stratego Board"),
        tiles,
        hex: false,
        size: Pos { x: 10, y: 10 },
    }
}

// endregion

// region make_lobby tests

fn create_test_lobby_parameters(env: &Env) -> LobbyParameters {
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

fn create_invalid_board_parameters(env: &Env) -> LobbyParameters {
    let board_hash = BytesN::from_array(env, &[1u8; 32]);
    
    let tiles = Vec::from_array(env, [
        Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0 },
        Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 0 },
        Tile { pos: Pos { x: 0, y: 1 }, passable: true, setup: 1 },
    ]);
    
    let board = Board {
        name: String::from_str(env, "Invalid Board"),
        tiles,
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

fn create_strategic_setup_from_game_state(env: &Env, lobby_id: u32, team: u32) -> (Vec<SetupCommit>, Setup, u64, Vec<HiddenRank>) {
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
    
    // Create rank distribution but organize strategically
    let mut front_ranks = Vec::new(env);  // Movable pieces for front lines
    let mut back_ranks = Vec::new(env);   // Flags and bombs for back row
    
    for (rank, count) in lobby_parameters.max_ranks.iter().enumerate() {
        let rank_u32 = rank as u32;
        for _ in 0..count {
            if rank_u32 == 0 || rank_u32 == 11 {  // Flag or Bomb
                back_ranks.push_back(rank_u32);
            } else {
                front_ranks.push_back(rank_u32);
            }
        }
    }
    
    // Sort team pawns by position - back rows first for flags/bombs
    let mut sorted_pawns = Vec::new(env);
    for pawn in team_pawns.iter() {
        sorted_pawns.push_back(pawn);
    }
    
    // Sort by y-coordinate (back to front for each team)
    // For team 0 (host): y=0,1,2,3 where y=0 is back row
    // For team 1 (guest): y=9,8,7,6 where y=9 is back row
    let mut pawn_vec: std::vec::Vec<PawnState> = std::vec::Vec::new();
    for pawn in sorted_pawns.iter() {
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
    
    // Add randomization to avoid mirror setups between teams
    // Use a different seed for each team to create diverse rank assignments
    let mut rank_seed = (salt + 1) * 12345; // Different seed per team
    
    // Shuffle the front ranks using the seed to create different distributions
    let mut shuffled_front_ranks = std::vec::Vec::new();
    for rank in front_ranks.iter() {
        shuffled_front_ranks.push(rank);
    }
    
    // Simple shuffle using the team-specific seed
    for i in 0..shuffled_front_ranks.len() {
        rank_seed = rank_seed.wrapping_mul(1103515245).wrapping_add(12345); // Linear congruential generator
        let j = (rank_seed as usize) % shuffled_front_ranks.len();
        shuffled_front_ranks.swap(i, j);
    }
    
    // Also shuffle back ranks for more diversity
    let mut shuffled_back_ranks = std::vec::Vec::new();
    for rank in back_ranks.iter() {
        shuffled_back_ranks.push(rank);
    }
    for i in 0..shuffled_back_ranks.len() {
        rank_seed = rank_seed.wrapping_mul(1103515245).wrapping_add(12345);
        let j = (rank_seed as usize) % shuffled_back_ranks.len();
        shuffled_back_ranks.swap(i, j);
    }
    
    for (i, pawn) in pawn_vec.iter().enumerate() {
        let rank = if i < back_count {
            // Assign shuffled flags and bombs to back positions
            if i < shuffled_back_ranks.len() {
                shuffled_back_ranks[i]
            } else {
                11u32 // Fallback to bomb
            }
        } else {
            // Assign shuffled ranks to front positions
            let front_index = i - back_count;
            if front_index < shuffled_front_ranks.len() {
                shuffled_front_ranks[front_index]
            } else {
                // Fallback to rank 4 (Sergeant) if we run out
                4u32
            }
        };
        
        let hidden_rank = HiddenRank {
            pawn_id: pawn.pawn_id,
            rank,
            salt: pawn.pawn_id as u64,
        };
        hidden_ranks.push_back(hidden_rank.clone());
        let serialized_hidden_rank = hidden_rank.clone().to_xdr(env);
        let hidden_rank_hash = env.crypto().sha256(&serialized_hidden_rank).to_bytes();
        
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

fn create_full_stratego_board_parameters(env: &Env) -> LobbyParameters {
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
        host_team: 1,
        max_ranks: Vec::from_array(env, DEFAULT_MAX_RANKS),
        must_fill_all_tiles: true,
        security_mode: true,
    }
}

#[test]
fn test_make_lobby_success() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 1u32;
    
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters.clone(),
    };
    
    setup.client.make_lobby(&host_address, &req);
    
    setup.verify_lobby_info(lobby_id, &host_address, Phase::Lobby);
    setup.verify_user_lobby(&host_address, lobby_id);
    
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_params_key = DataKey::LobbyParameters(lobby_id);
        let stored_params: LobbyParameters = setup.env.storage()
            .temporary()
            .get(&lobby_params_key)
            .expect("Lobby parameters should be stored");
        
        assert_eq!(stored_params.dev_mode, true);
        assert_eq!(stored_params.host_team, 0);
        assert_eq!(stored_params.board.name, String::from_str(&setup.env, "Default Stratego Board"));
    });
}

#[test]
fn test_make_lobby_validation_errors() {
    let setup = TestSetup::new();
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let invalid_lobby_parameters = create_invalid_board_parameters(&setup.env);
    
    // First, create a successful lobby to test "lobby already exists" error
    let host_address_1 = setup.generate_address();
    let req_1 = MakeLobbyReq {
        lobby_id: 1,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address_1, &req_1);
    
    // Test: Lobby already exists
    let host_address_2 = setup.generate_address();
    let req_2 = MakeLobbyReq {
        lobby_id: 1, // Same ID should fail
        parameters: lobby_parameters,
    };
    let result = setup.client.try_make_lobby(&host_address_2, &req_2);
    assert!(result.is_err(), "Should fail: lobby already exists");
    assert!(TestSetup::is_validation_error(&result.unwrap_err().unwrap()));
    
    // Test: Invalid board parameters
    let host_address_3 = setup.generate_address();
    let req_3 = MakeLobbyReq {
        lobby_id: 2,
        parameters: invalid_lobby_parameters,
    };
    let result = setup.client.try_make_lobby(&host_address_3, &req_3);
    assert!(result.is_err(), "Should fail: invalid board");
    assert!(TestSetup::is_validation_error(&result.unwrap_err().unwrap()));
}

// Original test_make_lobby_errors() replaced by test_make_lobby_validation_errors()

// endregion

// region leave_lobby tests

#[test]
fn test_leave_lobby_success_host() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create lobby first
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &req);
    
    // Leave lobby
    setup.client.leave_lobby(&host_address);
    
    // Verify user no longer has a lobby
    setup.verify_user_has_no_lobby(&host_address);
    
    // Verify lobby is marked as finished
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let stored_lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should still exist");
        
        assert_eq!(stored_lobby_info.phase, Phase::Finished);
        assert!(stored_lobby_info.host_address.is_empty());
    });
}

#[test]
fn test_leave_lobby_validation_errors() {
    let setup = TestSetup::new();
    
    // Test: User not found (no state change expected)
    let non_existent_user = setup.generate_address();
    let result = setup.client.try_leave_lobby(&non_existent_user);
    assert!(result.is_err(), "Should fail: user not found");
    assert_eq!(result.unwrap_err().unwrap(), Error::UserNotFound);
    
    // Create a user and lobby, then have them leave to test "no current lobby"
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let user_address = setup.generate_address();
    let make_req = MakeLobbyReq {
        lobby_id: 1,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&user_address, &make_req);
    setup.client.leave_lobby(&user_address); // Successfully leave
    
    // Test: No current lobby (after leaving)
    let result = setup.client.try_leave_lobby(&user_address);
    assert!(result.is_err(), "Should fail: no current lobby");
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotFound);
}

// Original test_leave_lobby_errors() replaced by test_leave_lobby_validation_errors()

// endregion

// region join_lobby tests

#[test]
fn test_join_lobby_success() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);
    
    setup.verify_user_lobby(&guest_address, lobby_id);
    
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let stored_lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert!(stored_lobby_info.guest_address.contains(&guest_address));
        assert_eq!(stored_lobby_info.phase, Phase::SetupCommit);
        assert_eq!(stored_lobby_info.subphase, Subphase::Both);
    });
    
    setup.verify_game_state_created(lobby_id);
}

#[test]
fn test_join_lobby_validation_errors() {
    let setup = TestSetup::new();
    
    // Test: Lobby not found
    let guest_address = setup.generate_address();
    let join_req_nonexistent = JoinLobbyReq { lobby_id: 999 };
    let result = setup.client.try_join_lobby(&guest_address, &join_req_nonexistent);
    assert!(result.is_err(), "Should fail: lobby not found");
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotFound);
    
    // Create a lobby for further tests
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let host_address = setup.generate_address();
    let req = MakeLobbyReq {
        lobby_id: 1,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address, &req);
    
    // Test: Host trying to join own lobby
    let join_req = JoinLobbyReq { lobby_id: 1 };
    let result = setup.client.try_join_lobby(&host_address, &join_req);
    assert!(result.is_err(), "Should fail: host trying to join own lobby");
    assert!(TestSetup::is_user_conflict_error(&result.unwrap_err().unwrap()));
    
    // Successfully join the lobby with a guest
    let guest_address_1 = setup.generate_address();
    setup.client.join_lobby(&guest_address_1, &join_req);
    
    // Test: Lobby not joinable (already has guest)
    let guest_address_2 = setup.generate_address();
    let result = setup.client.try_join_lobby(&guest_address_2, &join_req);
    assert!(result.is_err(), "Should fail: lobby not joinable");
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotJoinable);
    
    // Test: Guest already in another lobby
    let host_address_2 = setup.generate_address();
    let req_2 = MakeLobbyReq {
        lobby_id: 2,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address_2, &req_2);
    
    let join_req_2 = JoinLobbyReq { lobby_id: 2 };
    let result = setup.client.try_join_lobby(&guest_address_1, &join_req_2);
    assert!(result.is_err(), "Should fail: guest already in another lobby");
    let error = result.unwrap_err().unwrap();
    assert!(TestSetup::is_user_conflict_error(&error) || TestSetup::is_lobby_state_error(&error));
}

// Original test_join_lobby_errors() replaced by test_join_lobby_validation_errors()

// endregion

// region move tests
fn create_test_setup_data_from_game_state(setup: &TestSetup, lobby_id: u32, team: u32) -> (Vec<SetupCommit>, Setup, u64) {
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        let mut setup_commits = Vec::new(&setup.env);
        let salt = team as u64; // Setup salt is team index (0 for host, 1 for guest)
        
        // Find all pawns that belong to this team
        for (pawn_index, pawn) in game_state.pawns.iter().enumerate() {
            let (_, pawn_team) = Contract::decode_pawn_id(&pawn.pawn_id);
            if pawn_team == team {
                // Create proper HiddenRank struct and hash it
                let rank = (pawn_index % 12) as u32; // Simple rank assignment for testing
                let hidden_rank = HiddenRank {
                    pawn_id: pawn.pawn_id,
                    rank,
                    salt: pawn.pawn_id as u64, // Hidden rank salt is always pawn_id for testing
                };
                
                // Hash the HiddenRank struct properly
                let serialized_hidden_rank = hidden_rank.to_xdr(&setup.env);
                let hidden_rank_hash = setup.env.crypto().sha256(&serialized_hidden_rank).to_bytes();
                
                let commit = SetupCommit {
                    pawn_id: pawn.pawn_id,
                    hidden_rank_hash,
                };
                setup_commits.push_back(commit);
            }
        }
        
        let setup_proof = Setup {
            setup_commits: setup_commits.clone(),
            salt,
        };
        
        (setup_commits, setup_proof, salt)
    })
}

// Helper function to create and advance lobby through setup to MoveCommit phase
fn create_and_advance_to_move_commit(setup: &TestSetup, lobby_id: u32) -> (Address, Address) {
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    
    // Create and join lobby
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);
    
    // Create setup data for both players
    let (_, host_setup_proof, _) = create_test_setup_data_from_game_state(&setup, lobby_id, 0); // Host is team 0
    let (_, guest_setup_proof, _) = create_test_setup_data_from_game_state(&setup, lobby_id, 1); // Guest is team 1
    
    // Hash both setups
    let host_serialized = host_setup_proof.clone().to_xdr(&setup.env);
    let host_setup_hash = setup.env.crypto().sha256(&host_serialized).to_bytes();
    
    let guest_serialized = guest_setup_proof.clone().to_xdr(&setup.env);
    let guest_setup_hash = setup.env.crypto().sha256(&guest_serialized).to_bytes();
    
    // Commit both setups
    let host_commit_req = CommitSetupReq {
        lobby_id,
        setup_hash: host_setup_hash,
    };
    let guest_commit_req = CommitSetupReq {
        lobby_id,
        setup_hash: guest_setup_hash,
    };
    
    setup.client.commit_setup(&host_address, &host_commit_req);
    setup.client.commit_setup(&guest_address, &guest_commit_req);
    
    // Prove both setups to advance to MoveCommit phase
    let host_prove_req = ProveSetupReq {
        lobby_id,
        setup: host_setup_proof,
    };
    let guest_prove_req = ProveSetupReq {
        lobby_id,
        setup: guest_setup_proof,
    };
    
    setup.client.prove_setup(&host_address, &host_prove_req);
    setup.client.prove_setup(&guest_address, &guest_prove_req);
    
    // Verify we're in MoveCommit phase
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert_eq!(lobby_info.phase, Phase::MoveCommit);
        assert_eq!(lobby_info.subphase, Subphase::Both);
    });
    
    (host_address, guest_address)
}

// Helper function to create test move hash
fn create_test_move_hash(env: &Env, pawn_id: PawnId, start_pos: Pos, target_pos: Pos, salt: u64) -> HiddenMoveHash {
    let move_proof = HiddenMove {
        pawn_id,
        start_pos,
        target_pos,
        salt,
    };
    let serialized = move_proof.to_xdr(env);
    env.crypto().sha256(&serialized).to_bytes()
}

#[test]
fn test_commit_move_success_host() {
    let setup = TestSetup::new();
    let lobby_id = 1u32;
    
    let (host_address, _guest_address) = create_and_advance_to_move_commit(&setup, lobby_id);
    
    // Create a test move hash
    let pawn_id = Contract::encode_pawn_id(&Pos { x: 0, y: 0 }, &0); // Host's pawn
    let start_pos = Pos { x: 0, y: 0 }; // Starting position
    let target_pos = Pos { x: 0, y: 1 }; // Move up one space
    let move_hash = create_test_move_hash(&setup.env, pawn_id, start_pos, target_pos, 12345);
    
    let commit_req = CommitMoveReq {
        lobby_id,
        move_hash: move_hash.clone(),
    };
    
    setup.client.commit_move(&host_address, &commit_req);
    
    // Verify move was committed and subphase changed
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert_eq!(lobby_info.phase, Phase::MoveCommit);
        assert_eq!(lobby_info.subphase, Subphase::Guest); // Now waiting for guest
        
        // Verify move hash was stored
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        let host_move = game_state.moves.get(0).unwrap();
        assert_eq!(host_move.move_hash.len(), 1);
        assert_eq!(host_move.move_hash.get(0).unwrap(), move_hash);
    });
}

#[test]
fn test_commit_move_success_both_players() {
    let setup = TestSetup::new();
    let lobby_id = 1u32;
    
    let (host_address, guest_address) = create_and_advance_to_move_commit(&setup, lobby_id);
    
    // Create test move hashes for both players
    let host_pawn_id = Contract::encode_pawn_id(&Pos { x: 0, y: 0 }, &0);
    let host_move_hash = create_test_move_hash(&setup.env, host_pawn_id, Pos { x: 0, y: 0 }, Pos { x: 0, y: 1 }, 12345);
    
    let guest_pawn_id = Contract::encode_pawn_id(&Pos { x: 0, y: 3 }, &1);
    let guest_move_hash = create_test_move_hash(&setup.env, guest_pawn_id, Pos { x: 0, y: 3 }, Pos { x: 0, y: 2 }, 54321);
    
    // Host commits first
    let host_commit_req = CommitMoveReq {
        lobby_id,
        move_hash: host_move_hash.clone(),
    };
    setup.client.commit_move(&host_address, &host_commit_req);
    
    // Guest commits second
    let guest_commit_req = CommitMoveReq {
        lobby_id,
        move_hash: guest_move_hash.clone(),
    };
    setup.client.commit_move(&guest_address, &guest_commit_req);
    
    // Verify both moves committed and phase advanced to MoveProve
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert_eq!(lobby_info.phase, Phase::MoveProve);
        assert_eq!(lobby_info.subphase, Subphase::Both); // Both can prove
        
        // Verify both move hashes were stored
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        let host_move = game_state.moves.get(0).unwrap();
        let guest_move = game_state.moves.get(1).unwrap();
        
        assert_eq!(host_move.move_hash.get(0).unwrap(), host_move_hash);
        assert_eq!(guest_move.move_hash.get(0).unwrap(), guest_move_hash);
    });
}

#[test]
fn test_commit_move_validation_errors() {
    let setup = TestSetup::new();
    let move_hash = create_test_move_hash(&setup.env, 1, Pos { x: 0, y: 0 }, Pos { x: 0, y: 1 }, 12345);
    
    // Test: Lobby not found
    let user_address = setup.generate_address();
    let commit_req = CommitMoveReq {
        lobby_id: 999,
        move_hash: move_hash.clone(),
    };
    let result = setup.client.try_commit_move(&user_address, &commit_req);
    assert!(result.is_err(), "Should fail: lobby not found");
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotFound);
    
    // Create a lobby and advance to move commit phase for further tests
    let (lobby_id, host_address, _guest_address) = setup_lobby_for_commit_move(&setup);
    
    // Test: Not in lobby
    let outsider_address = setup.generate_address();
    let commit_req = CommitMoveReq {
        lobby_id,
        move_hash: move_hash.clone(),
    };
    let result = setup.client.try_commit_move(&outsider_address, &commit_req);
    assert!(result.is_err(), "Should fail: not in lobby");
    assert_eq!(result.unwrap_err().unwrap(), Error::NotInLobby);
    
    // Test: Wrong phase (create a new lobby in wrong phase)
    let (wrong_phase_lobby_id, wrong_phase_host, _) = setup_lobby_for_commit_setup(&setup);
    
    let commit_req = CommitMoveReq {
        lobby_id: wrong_phase_lobby_id,
        move_hash: move_hash.clone(),
    };
    let result = setup.client.try_commit_move(&wrong_phase_host, &commit_req);
    assert!(result.is_err(), "Should fail: wrong phase");
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongPhase);
    
    // Test: Wrong subphase (commit one move, then try to commit another)
    let host_pawn_id = Contract::encode_pawn_id(&Pos { x: 0, y: 0 }, &0);
    let host_move_hash = create_test_move_hash(&setup.env, host_pawn_id, Pos { x: 0, y: 0 }, Pos { x: 0, y: 1 }, 12345);
    
    let host_commit_req = CommitMoveReq {
        lobby_id,
        move_hash: host_move_hash,
    };
    setup.client.commit_move(&host_address, &host_commit_req);
    
    let another_move_hash = create_test_move_hash(&setup.env, host_pawn_id, Pos { x: 0, y: 0 }, Pos { x: 1, y: 0 }, 67890);
    let another_commit_req = CommitMoveReq {
        lobby_id,
        move_hash: another_move_hash,
    };
    let result = setup.client.try_commit_move(&host_address, &another_commit_req);
    assert!(result.is_err(), "Should fail: wrong subphase (already committed)");
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongSubphase);
    
    // Test: After game finished
    let (finished_lobby_id, finished_host, finished_guest) = setup_lobby_for_commit_move(&setup);
    setup.client.leave_lobby(&finished_host); // This should finish the game
    
    let commit_req = CommitMoveReq {
        lobby_id: finished_lobby_id,
        move_hash,
    };
    let result = setup.client.try_commit_move(&finished_guest, &commit_req);
    assert!(result.is_err(), "Should fail: game finished");
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongPhase);
}

// endregion

// region setup tests

#[test]
fn test_prove_setup_invalid_pawn_ownership() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);
    
    // Create setup with wrong team pawns for host (team 1 instead of 0)
    let (_, host_setup_proof, _) = create_test_setup_data_from_game_state(&setup, lobby_id, 1); // Wrong team!
    
    // Create valid setup for guest (team 0 - also wrong, but different from host)
    let (_, guest_setup_proof, _) = create_test_setup_data_from_game_state(&setup, lobby_id, 0);
    
    // Hash both setups
    let host_serialized = host_setup_proof.clone().to_xdr(&setup.env);
    let host_setup_hash = setup.env.crypto().sha256(&host_serialized).to_bytes();
    
    let guest_serialized = guest_setup_proof.clone().to_xdr(&setup.env);
    let guest_setup_hash = setup.env.crypto().sha256(&guest_serialized).to_bytes();
    
    // Commit both setups to transition to SetupProve phase
    let host_commit_req = CommitSetupReq {
        lobby_id,
        setup_hash: host_setup_hash,
    };
    let guest_commit_req = CommitSetupReq {
        lobby_id,
        setup_hash: guest_setup_hash,
    };
    
    setup.client.commit_setup(&host_address, &host_commit_req);
    setup.client.commit_setup(&guest_address, &guest_commit_req);
    
    // Verify we're in SetupProve phase with Both subphase
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert_eq!(lobby_info.phase, Phase::SetupProve);
        assert_eq!(lobby_info.subphase, Subphase::Both); // Both players can prove
    });
    
    // Try to prove invalid setup - should end the game with host losing
    let host_prove_req = ProveSetupReq {
        lobby_id,
        setup: host_setup_proof,
    };
    
    setup.client.prove_setup(&host_address, &host_prove_req);
    
    // Verify game ended with guest winning
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert_eq!(lobby_info.phase, Phase::Finished);
        assert_eq!(lobby_info.subphase, Subphase::Guest); // Guest wins
    });
}

// endregion

// region integration tests

#[test]
fn test_full_stratego_board_creation() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create a full Stratego board
    let lobby_parameters = create_full_stratego_board_parameters(&setup.env);
    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters.clone(),
    };
    
    setup.client.make_lobby(&host_address, &req);
    
    // Verify lobby was created with full board
    setup.verify_lobby_info(lobby_id, &host_address, Phase::Lobby);
    
    // Verify board parameters
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_params_key = DataKey::LobbyParameters(lobby_id);
        let stored_params: LobbyParameters = setup.env.storage()
            .temporary()
            .get(&lobby_params_key)
            .expect("Lobby parameters should be stored");
        
        // Verify it's a 10x10 board
        assert_eq!(stored_params.board.size.x, 10);
        assert_eq!(stored_params.board.size.y, 10);
        assert_eq!(stored_params.board.tiles.len(), 100); // 10x10 = 100 tiles
        assert_eq!(stored_params.board.name, String::from_str(&setup.env, "Full Stratego Board"));
        
        // Verify realistic settings
        assert_eq!(stored_params.dev_mode, false);
        assert_eq!(stored_params.host_team, 1);
        assert_eq!(stored_params.must_fill_all_tiles, true);
        assert_eq!(stored_params.security_mode, true);
        
        // Verify Stratego rank distribution (12 different ranks)
        assert_eq!(stored_params.max_ranks.len(), 12);
        
        // Verify piece counts add up to 40 per side
        let total_pieces: u32 = stored_params.max_ranks.iter().sum();
        assert_eq!(total_pieces, 40);
        
        // Count setup tiles for each team
        let mut team0_tiles = 0;
        let mut team1_tiles = 0;
        let mut lake_tiles = 0;
        
        for tile in stored_params.board.tiles.iter() {
            match tile.setup {
                0 => team0_tiles += 1,
                1 => team1_tiles += 1,
                2 => {
                    if !tile.passable {
                        lake_tiles += 1;
                    }
                },
                _ => {}
            }
        }
        
        // Each team should have exactly 40 setup positions
        assert_eq!(team0_tiles, 40);
        assert_eq!(team1_tiles, 40);
        
        // Should have lake tiles (impassable water)
        assert_eq!(lake_tiles, 8); // 2x4 lakes in traditional Stratego
    });
}


#[test]
fn test_full_stratego_game_with_populated_ranks() {
    let setup = TestSetup::new();
    
    // Use helper function to set up lobby and advance to MoveCommit phase
    let (lobby_id, host_address, guest_address) = setup_lobby_for_commit_move(&setup);
    
    // Get rank data for move generation using the same seeds as the helper function
    let (_, _, _, host_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_deterministic_setup(&setup.env, 0, 12345) // team 0, same seed as helper
    });
    let (_, _, _, guest_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_deterministic_setup(&setup.env, 1, 67890) // team 1, same seed as helper
    });
    
    // Verify we're in MoveCommit phase (helper function should guarantee this)
    let initial_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(initial_snapshot.phase, Phase::MoveCommit);
    assert_eq!(initial_snapshot.subphase, Subphase::Both);
    

    let populate = true;
    if populate {
        // Populate all ranks in the game state
        setup.env.as_contract(&setup.contract_id, || {
            let game_state_key = DataKey::GameState(lobby_id);
            let mut game_state: GameState = setup.env.storage()
                .temporary()
                .get(&game_state_key)
                .expect("Game state should exist");
            for hidden_rank in host_ranks.iter().chain(guest_ranks.iter()) {
                let (index, mut pawn) = game_state.pawns.iter().enumerate().find(|(_, p)| p.pawn_id == hidden_rank.pawn_id).unwrap();
                pawn.rank = Vec::from_array(&setup.env, [hidden_rank.rank]);
                game_state.pawns.set(index as u32, pawn);
            }
            setup.env.storage().temporary().set(&game_state_key, &game_state);
        });
    }
    

    
    // Perform up to 50 moves or until no valid moves are possible
    for move_number in 1..=50 {
        std::println!("=== MOVE {} ===", move_number);
        
        // Check current phase and handle accordingly
        let current_phase_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        let current_phase = current_phase_snapshot.phase;
        
        match current_phase {
            Phase::MoveCommit => {
                std::println!("Phase: MoveCommit - Committing moves");
                
                // Generate and commit moves
                let move_result = setup.env.as_contract(&setup.contract_id, || {
                    let move_gen_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
                    
                    let host_move_opt = generate_valid_move_req(&setup.env, &move_gen_snapshot.game_state, &move_gen_snapshot.lobby_parameters, 0, &host_ranks, move_number as u64 * 1000 + 12345);
                    let guest_move_opt = generate_valid_move_req(&setup.env, &move_gen_snapshot.game_state, &move_gen_snapshot.lobby_parameters, 1, &guest_ranks, move_number as u64 * 1000 + 54321);
                    
                    if host_move_opt.is_none() || guest_move_opt.is_none() {
                        return None;
                    }
                    
                    let host_move_proof = host_move_opt.unwrap();
                    let guest_move_proof = guest_move_opt.unwrap();
                    
                    let host_move_serialized = host_move_proof.clone().to_xdr(&setup.env);
                    let host_move_hash = setup.env.crypto().sha256(&host_move_serialized).to_bytes();
                    let guest_move_serialized = guest_move_proof.clone().to_xdr(&setup.env);
                    let guest_move_hash = setup.env.crypto().sha256(&guest_move_serialized).to_bytes();
                    
                    let host_move_req = CommitMoveReq {
                        lobby_id,
                        move_hash: host_move_hash,
                    };
                    let guest_move_req = CommitMoveReq {
                        lobby_id,
                        move_hash: guest_move_hash,
                    };
                    
                    Some((host_move_proof, guest_move_proof, host_move_req, guest_move_req))
                });
                
                if move_result.is_none() {
                    std::println!("No valid moves available for one or both players. Game ends at move {}", move_number);
                    break;
                }
                
                let (host_move_proof, guest_move_proof, host_move_req, guest_move_req) = move_result.unwrap();
                
                setup.client.commit_move(&host_address, &host_move_req);
                setup.client.commit_move(&guest_address, &guest_move_req);
                
                // Check current phase after committing moves
                let post_commit_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
                let current_phase_after_commit = post_commit_snapshot.phase;
                
                std::println!("After committing moves for turn {}: current_phase = {:?}", move_number, current_phase_after_commit);
                
                // Only proceed with move proving if we're in MoveProve phase
                if current_phase_after_commit == Phase::MoveProve {
                    std::println!("Proceeding with MoveProve phase for turn {}", move_number);
                    

                    
                    // Prove moves
                    let host_prove_move_req = ProveMoveReq {
                        move_proof: host_move_proof,
                        lobby_id,
                    };
                    let guest_prove_move_req = ProveMoveReq {
                        move_proof: guest_move_proof,
                        lobby_id,
                    };
                    
                    setup.client.prove_move(&host_address, &host_prove_move_req);
                    setup.client.prove_move(&guest_address, &guest_prove_move_req);
                    
                    // VALIDATE: Check what happened after MoveProve - what rank proofs are needed?
                    validate_move_prove_transition(&setup.env, &setup.contract_id, lobby_id, &host_prove_move_req, &guest_prove_move_req);
                    
                } else {
                    std::println!("Skipped MoveProve phase - game already advanced to next phase: {:?}", current_phase_after_commit);
                }
                
                std::println!("Moves committed and proved successfully");
            },
            
            Phase::RankProve => {
                std::println!("Phase: RankProve - Proving ranks for collisions");
                
                // Get the needed rank proofs for both players
                let rank_proof_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
                let host_move = rank_proof_snapshot.game_state.moves.get(0).unwrap();
                std::println!("Host move: {:?}", host_move);
                let guest_move = rank_proof_snapshot.game_state.moves.get(1).unwrap();
                std::println!("Guest move: {:?}", guest_move);
                let host_needed_ranks = host_move.needed_rank_proofs.clone();
                let guest_needed_ranks = guest_move.needed_rank_proofs.clone();
                // Debug: Print needed ranks for both players
                for id in host_needed_ranks.iter() {
                    let (_, team) = Contract::decode_pawn_id(&id);
                    std::println!("Host needed rank: {} team: {}", id.clone(), team);
                }
                for id in guest_needed_ranks.iter() {
                    let (_, team) = Contract::decode_pawn_id(&id);
                    std::println!("Guest needed rank: {} team: {}", id.clone(), team);
                }

                // Prove ranks using utility function
                let (host_rank_req, guest_rank_req, _) = submit_required_rank_proofs(&setup.env, &setup.client, lobby_id, &host_address, &guest_address, 
                                 &host_needed_ranks, &guest_needed_ranks, &host_ranks, &guest_ranks);
                
                // VALIDATE: Check if game transitioned out of RankProve phase
                validate_rank_prove_transition(&setup.env, &setup.contract_id, lobby_id, host_rank_req.as_ref(), guest_rank_req.as_ref());
                
                // Print board state RIGHT AFTER rank proving but before collision resolution
                std::println!("=== BOARD STATE AFTER RANK PROVING (BEFORE COLLISION RESOLUTION) ===");
                let board_display_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
                let board_state_with_revealed_ranks = format_board_with_colors(&setup.env, &board_display_snapshot.game_state, &board_display_snapshot.lobby_parameters, &board_display_snapshot.lobby_info);
                std::println!("{}", board_state_with_revealed_ranks);
                std::println!("=== END BOARD STATE AFTER RANK PROVING ===");
            },
            
            Phase::Finished => {
                std::println!("Game finished at move {}", move_number);
                break;
            },
            
            _ => {
                panic!("Unexpected phase: {:?}", current_phase);
            }
        }
        

    }
    
    // Verify final game state
    let final_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    
    // The game should be in a valid state
    assert!(matches!(final_snapshot.phase, Phase::MoveCommit | Phase::MoveProve | Phase::RankProve | Phase::Finished));
    std::println!("Final game state: Phase={:?}, Subphase={:?}", final_snapshot.phase, final_snapshot.subphase);
    
}



// generate_valid_move_req function moved to test_utils.rs



// endregion

// region board visualization - moved to test_utils
// format_board_with_colors function moved to test_utils.rs

// endregion

// region utility functions

// Test environment setup helper
struct TestSetup {
    env: Env,
    contract_id: Address,
    client: ContractClient<'static>,
}

impl TestSetup {
    fn new() -> Self {
        let env = Env::default();
        env.mock_all_auths();
        let contract_id = env.register(Contract, ());
        let client = ContractClient::new(&env, &contract_id);
        
        Self {
            env,
            contract_id,
            client,
        }
    }
    
    // Helper to check if error indicates user/lobby conflict
    fn is_user_conflict_error(error: &Error) -> bool {
        matches!(error, 
            Error::GuestAlreadyInLobby | 
            Error::HostAlreadyInLobby | 
            Error::JoinerIsHost |
            Error::NotInLobby
        )
    }
    
    // Helper to check if error indicates lobby state issue
    fn is_lobby_state_error(error: &Error) -> bool {
        matches!(error, 
            Error::LobbyNotJoinable | 
            Error::WrongPhase | 
            Error::LobbyNotFound |
            Error::LobbyHasNoHost
        )
    }
    
    // Helper to check if error indicates validation failure
    fn is_validation_error(error: &Error) -> bool {
        matches!(error, 
            Error::InvalidBoard | 
            Error::InvalidArgs |
            Error::LobbyAlreadyExists
        )
    }
    
    fn generate_address(&self) -> Address {
        Address::generate(&self.env)
    }
    
    fn verify_lobby_info(&self, lobby_id: u32, expected_host: &Address, expected_phase: Phase) {
        self.env.as_contract(&self.contract_id, || {
            let lobby_info_key = DataKey::LobbyInfo(lobby_id);
            let stored_lobby_info: LobbyInfo = self.env.storage()
                .temporary()
                .get(&lobby_info_key)
                .expect("Lobby info should be stored");
            
            assert_eq!(stored_lobby_info.index, lobby_id);
            assert!(stored_lobby_info.host_address.contains(expected_host));
            assert_eq!(stored_lobby_info.phase, expected_phase);
        });
    }
    
    fn verify_user_lobby(&self, user_address: &Address, expected_lobby_id: u32) {
        self.env.as_contract(&self.contract_id, || {
            let user_key = DataKey::User(user_address.clone());
            let stored_user: User = self.env.storage()
                .persistent()
                .get(&user_key)
                .expect("User should be stored");
            
            assert_eq!(stored_user.current_lobby.get(0).unwrap(), expected_lobby_id);
        });
    }
    
    fn verify_user_has_no_lobby(&self, user_address: &Address) {
        self.env.as_contract(&self.contract_id, || {
            let user_key = DataKey::User(user_address.clone());
            let stored_user: User = self.env.storage()
                .persistent()
                .get(&user_key)
                .expect("User should be stored");
            
            assert!(stored_user.current_lobby.is_empty());
        });
    }

    fn verify_game_state_created(&self, lobby_id: u32) {
        self.env.as_contract(&self.contract_id, || {
            let game_state_key = DataKey::GameState(lobby_id);
            let stored_game_state: GameState = self.env.storage()
                .temporary()
                .get(&game_state_key)
                .expect("Game state should be created");
            
            assert!(!stored_game_state.pawns.is_empty());
            assert_eq!(stored_game_state.setups.len(), 2);
        });
    }
}

// endregion



#[test]
fn test_collision_winner_rank_revelation() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create full Stratego board and join
    let lobby_parameters = create_full_stratego_board_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);
    
    // Create setups but manually assign specific ranks for testing
    let (host_commits, host_proof, _, _) = setup.env.as_contract(&setup.contract_id, || {
        create_strategic_setup_from_game_state(&setup.env, lobby_id, 0)
    });
    let (guest_commits, guest_proof, _, _) = setup.env.as_contract(&setup.contract_id, || {
        create_strategic_setup_from_game_state(&setup.env, lobby_id, 1)
    });
    
    // Commit and prove setups
    let host_serialized = host_proof.clone().to_xdr(&setup.env);
    let host_setup_hash = setup.env.crypto().sha256(&host_serialized).to_bytes();
    
    let guest_serialized = guest_proof.clone().to_xdr(&setup.env);
    let guest_setup_hash = setup.env.crypto().sha256(&guest_serialized).to_bytes();
    
    let host_commit_req = CommitSetupReq {
        lobby_id,
        setup_hash: host_setup_hash,
    };
    let guest_commit_req = CommitSetupReq {
        lobby_id,
        setup_hash: guest_setup_hash,
    };
    
    setup.client.commit_setup(&host_address, &host_commit_req);
    setup.client.commit_setup(&guest_address, &guest_commit_req);
    
    let host_prove_req = ProveSetupReq {
        lobby_id,
        setup: host_proof,
    };
    let guest_prove_req = ProveSetupReq {
        lobby_id,
        setup: guest_proof,
    };
    
    setup.client.prove_setup(&host_address, &host_prove_req);
    setup.client.prove_setup(&guest_address, &guest_prove_req);
    
    // Now manually set up a controlled collision scenario
    // Set specific ranks to ensure we have a clear winner
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_id);
        let mut game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        // Find a host pawn on front line (team 0) and a guest pawn on front line (team 1)
        let mut host_front_pawn_id = None;
        let mut guest_front_pawn_id = None;
        
        for pawn in game_state.pawns.iter() {
            let (_, team) = Contract::decode_pawn_id(&pawn.pawn_id);
            if team == 0 && pawn.pos.y == 3 && host_front_pawn_id.is_none() {
                host_front_pawn_id = Some(pawn.pawn_id);
            }
            if team == 1 && pawn.pos.y == 6 && guest_front_pawn_id.is_none() {
                guest_front_pawn_id = Some(pawn.pawn_id);
            }
        }
        
        let host_pawn_id = host_front_pawn_id.expect("Should find host front pawn");
        let guest_pawn_id = guest_front_pawn_id.expect("Should find guest front pawn");
        
        // Set host pawn to Scout (rank 2) and guest pawn to Marshal (rank 10)
        // When Scout attacks Marshal, Marshal should win and stay revealed
        for (index, pawn) in game_state.pawns.iter().enumerate() {
            if pawn.pawn_id == host_pawn_id {
                let mut updated_pawn = pawn.clone();
                updated_pawn.rank = Vec::from_array(&setup.env, [2u32]); // Scout
                game_state.pawns.set(index as u32, updated_pawn);
                std::println!("Set host pawn {} to Scout (rank 2) at position ({},{})", 
                             host_pawn_id, pawn.pos.x, pawn.pos.y);
            }
            if pawn.pawn_id == guest_pawn_id {
                let mut updated_pawn = pawn.clone();
                updated_pawn.rank = Vec::from_array(&setup.env, [10u32]); // Marshal
                game_state.pawns.set(index as u32, updated_pawn);
                std::println!("Set guest pawn {} to Marshal (rank 10) at position ({},{})", 
                             guest_pawn_id, pawn.pos.x, pawn.pos.y);
            }
        }
        
        setup.env.storage().temporary().set(&game_state_key, &game_state);
        
        (host_pawn_id, guest_pawn_id)
    });
    
    // Create and execute a move where the Scout attacks the Marshal
    let collision_move = setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        // Find the Marshal's position to attack it
        let mut marshal_pos = None;
        let mut scout_pos = None;
        
        for pawn in game_state.pawns.iter() {
            if !pawn.rank.is_empty() {
                if pawn.rank.get(0).unwrap() == 10 { // Marshal
                    marshal_pos = Some(pawn.pos);
                }
                if pawn.rank.get(0).unwrap() == 2 { // Scout
                    scout_pos = Some(pawn.pos);
                }
            }
        }
        
        let marshal_pos = marshal_pos.expect("Should find Marshal");
        let scout_pos = scout_pos.expect("Should find Scout");
        
        std::println!("Scout at ({},{}) will attack Marshal at ({},{})", 
                     scout_pos.x, scout_pos.y, marshal_pos.x, marshal_pos.y);
        
        // Create a valid path for Scout to reach Marshal
        let target_pos = Pos { x: marshal_pos.x, y: marshal_pos.y - 1 }; // Move one space toward Marshal
        
        (scout_pos, target_pos)
    });
    
    std::println!("Test demonstrates that collision winners should survive with revealed ranks!");
}

// endregion

#[test]
fn test_compare_populated_vs_unpopulated_games() {
    let setup = TestSetup::new();
    
    // Create two identical games: one unpopulated, one populated
    let host_a = setup.generate_address();    // Game A (unpopulated)
    let guest_a = setup.generate_address();
    let host_b = setup.generate_address();    // Game B (populated) 
    let guest_b = setup.generate_address();
    
    let lobby_a = 1u32;  // Unpopulated game
    let lobby_b = 2u32;  // Populated game
    
    // Setup both games with identical parameters and setups
    let lobby_parameters = create_full_stratego_board_parameters(&setup.env);
    
    setup.client.make_lobby(&host_a, &MakeLobbyReq { lobby_id: lobby_a, parameters: lobby_parameters.clone() });
    setup.client.join_lobby(&guest_a, &JoinLobbyReq { lobby_id: lobby_a });
    
    setup.client.make_lobby(&host_b, &MakeLobbyReq { lobby_id: lobby_b, parameters: lobby_parameters });
    setup.client.join_lobby(&guest_b, &JoinLobbyReq { lobby_id: lobby_b });
    
    // Generate identical setups using fixed seed
    let fixed_seed = 42u64;
    let (_, host_proof, _, host_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_deterministic_setup(&setup.env, 0, fixed_seed)
    });
    let (_, guest_proof, _, guest_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_deterministic_setup(&setup.env, 1, fixed_seed)
    });
    
    // Apply identical setups to both games
    for lobby_id in [lobby_a, lobby_b] {
        let (host_addr, guest_addr) = if lobby_id == lobby_a { (&host_a, &guest_a) } else { (&host_b, &guest_b) };
        
        let host_hash = setup.env.crypto().sha256(&host_proof.clone().to_xdr(&setup.env)).to_bytes();
        let guest_hash = setup.env.crypto().sha256(&guest_proof.clone().to_xdr(&setup.env)).to_bytes();
        
        setup.client.commit_setup(host_addr, &CommitSetupReq { lobby_id, setup_hash: host_hash });
        setup.client.commit_setup(guest_addr, &CommitSetupReq { lobby_id, setup_hash: guest_hash });
        setup.client.prove_setup(host_addr, &ProveSetupReq { lobby_id, setup: host_proof.clone() });
        setup.client.prove_setup(guest_addr, &ProveSetupReq { lobby_id, setup: guest_proof.clone() });
    }
    
    // Populate ranks only in Game B
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_b);
        let mut game_state: GameState = setup.env.storage().temporary().get(&game_state_key).expect("Game state should exist");
        for hidden_rank in host_ranks.iter().chain(guest_ranks.iter()) {
            let (index, mut pawn) = game_state.pawns.iter().enumerate().find(|(_, p)| p.pawn_id == hidden_rank.pawn_id).unwrap();
            pawn.rank = Vec::from_array(&setup.env, [hidden_rank.rank]);
            game_state.pawns.set(index as u32, pawn);
        }
        setup.env.storage().temporary().set(&game_state_key, &game_state);
    });
    
    // Execute identical moves and verify game states remain consistent
    for move_number in 1..=10 {
        // Generate identical moves for both games
        let salt_host = move_number as u64 * 1000 + 12345;
        let salt_guest = move_number as u64 * 1000 + 54321;
        
        let (host_move, guest_move) = setup.env.as_contract(&setup.contract_id, || {
            let game_state_key = DataKey::GameState(lobby_a);
            let lobby_parameters_key = DataKey::LobbyParameters(lobby_a);
            let game_state: GameState = setup.env.storage().temporary().get(&game_state_key).expect("Game state should exist");
            let lobby_parameters: LobbyParameters = setup.env.storage().temporary().get(&lobby_parameters_key).expect("Lobby parameters should exist");
            
            let host_move = generate_valid_move_req(&setup.env, &game_state, &lobby_parameters, 0, &host_ranks, salt_host);
            let guest_move = generate_valid_move_req(&setup.env, &game_state, &lobby_parameters, 1, &guest_ranks, salt_guest);
            (host_move, guest_move)
        });
        
        if host_move.is_none() || guest_move.is_none() {
            break; // No more valid moves
        }
        
        let host_move_proof = host_move.unwrap();
        let guest_move_proof = guest_move.unwrap();
        
        // Execute moves on both games
        for lobby_id in [lobby_a, lobby_b] {
            let (host_addr, guest_addr) = if lobby_id == lobby_a { (&host_a, &guest_a) } else { (&host_b, &guest_b) };
            
            // Check if we can commit moves
            let can_commit = setup.env.as_contract(&setup.contract_id, || {
                let lobby_info_key = DataKey::LobbyInfo(lobby_id);
                let lobby_info: LobbyInfo = setup.env.storage().temporary().get(&lobby_info_key).expect("Lobby info should exist");
                lobby_info.phase == Phase::MoveCommit
            });
            
            if can_commit {
                let host_hash = setup.env.crypto().sha256(&host_move_proof.clone().to_xdr(&setup.env)).to_bytes();
                let guest_hash = setup.env.crypto().sha256(&guest_move_proof.clone().to_xdr(&setup.env)).to_bytes();
                
                setup.client.commit_move(host_addr, &CommitMoveReq { lobby_id, move_hash: host_hash });
                setup.client.commit_move(guest_addr, &CommitMoveReq { lobby_id, move_hash: guest_hash });
                
                // Check if we should prove moves
                let should_prove = setup.env.as_contract(&setup.contract_id, || {
                    let lobby_info_key = DataKey::LobbyInfo(lobby_id);
                    let lobby_info: LobbyInfo = setup.env.storage().temporary().get(&lobby_info_key).expect("Lobby info should exist");
                    lobby_info.phase == Phase::MoveProve
                });
                
                if should_prove {
                    setup.client.prove_move(host_addr, &ProveMoveReq { lobby_id, move_proof: host_move_proof.clone() });
                    setup.client.prove_move(guest_addr, &ProveMoveReq { lobby_id, move_proof: guest_move_proof.clone() });
                }
            }
        }
        
        // Handle rank proving if needed (only Game A should need this)
        let game_a_needs_ranks = setup.env.as_contract(&setup.contract_id, || {
            let lobby_info_key = DataKey::LobbyInfo(lobby_a);
            let lobby_info: LobbyInfo = setup.env.storage().temporary().get(&lobby_info_key).expect("Lobby info should exist");
            lobby_info.phase == Phase::RankProve
        });
        
        if game_a_needs_ranks {
            let (host_needed, guest_needed) = setup.env.as_contract(&setup.contract_id, || {
                let game_state_key = DataKey::GameState(lobby_a);
                let game_state: GameState = setup.env.storage().temporary().get(&game_state_key).expect("Game state should exist");
                let host_move = game_state.moves.get(0).unwrap();
                let guest_move = game_state.moves.get(1).unwrap();
                (host_move.needed_rank_proofs.clone(), guest_move.needed_rank_proofs.clone())
            });
            
            // Provide needed rank proofs
            if !host_needed.is_empty() {
                let mut host_proof_ranks = Vec::new(&setup.env);
                for needed_id in host_needed.iter() {
                    for rank in host_ranks.iter() {
                        if rank.pawn_id == needed_id {
                            host_proof_ranks.push_back(rank);
                        }
                    }
                }
                if !host_proof_ranks.is_empty() {
                    setup.client.prove_rank(&host_a, &ProveRankReq { lobby_id: lobby_a, hidden_ranks: host_proof_ranks });
                }
            }
            
            if !guest_needed.is_empty() {
                let mut guest_proof_ranks = Vec::new(&setup.env);
                for needed_id in guest_needed.iter() {
                    for rank in guest_ranks.iter() {
                        if rank.pawn_id == needed_id {
                            guest_proof_ranks.push_back(rank);
                        }
                    }
                }
                if !guest_proof_ranks.is_empty() {
                    setup.client.prove_rank(&guest_a, &ProveRankReq { lobby_id: lobby_a, hidden_ranks: guest_proof_ranks });
                }
            }
        }
        
        // Verify both games have identical states
        let states_match = verify_pawn_states_identical(&setup.env, &setup.contract_id, lobby_a, lobby_b);
        assert!(states_match, "Game states diverged at move {}", move_number);
        
        // Verify both games are in the same phase
        let phase_snapshot_a = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_a);
        let phase_snapshot_b = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_b);
        let (phase_a, phase_b) = (phase_snapshot_a.phase, phase_snapshot_b.phase);
        
        assert_eq!(phase_a, phase_b, "Game phases diverged at move {}: A={:?}, B={:?}", move_number, phase_a, phase_b);
    }
}

// verify_pawn_states_identical function moved to test_utils.rs

// create_deterministic_setup function moved to test_utils.rs

// Replaced by function-specific validation tests

// Legacy individual tests have been replaced by consolidated tests

// Replaced by function-specific validation tests

// All validation error tests have been consolidated

// endregion

// region validation test helpers

/// Sets up a lobby ready for setup commit phase testing
/// Returns (lobby_id, host_address, guest_address)
fn setup_lobby_for_commit_setup(setup: &TestSetup) -> (u32, Address, Address) {
    let lobby_parameters = create_full_stratego_board_parameters(&setup.env);
    let lobby_id = generate_unique_lobby_id();
    
    // Create lobby
    let host_address = setup.generate_address();
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    // Join lobby
    let guest_address = setup.generate_address();
    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);
    
    (lobby_id, host_address, guest_address)
}

/// Sets up a lobby and advances it to move commit phase (first turn) for validation testing
/// Returns (lobby_id, host_address, guest_address)
fn setup_lobby_for_commit_move(setup: &TestSetup) -> (u32, Address, Address) {
    let (lobby_id, host_address, guest_address) = setup_lobby_for_commit_setup(setup);
    
    // Advance through setup phase
    advance_through_setup_phase(setup, lobby_id, &host_address, &guest_address);
    
    (lobby_id, host_address, guest_address)
}

/// Helper to advance a lobby through the complete setup phase to move commit
fn advance_through_setup_phase(setup: &TestSetup, lobby_id: u32, host_address: &Address, guest_address: &Address) {
    // Create deterministic setups for both players
    let (_, host_setup_proof, _, _) = setup.env.as_contract(&setup.contract_id, || {
        create_deterministic_setup(&setup.env, 0, 12345) // team 0, salt 12345
    });
    
    let (_, guest_setup_proof, _, _) = setup.env.as_contract(&setup.contract_id, || {
        create_deterministic_setup(&setup.env, 1, 67890) // team 1, salt 67890
    });
    
    // Hash both setups
    let host_serialized = host_setup_proof.clone().to_xdr(&setup.env);
    let host_setup_hash = setup.env.crypto().sha256(&host_serialized).to_bytes();
    
    let guest_serialized = guest_setup_proof.clone().to_xdr(&setup.env);
    let guest_setup_hash = setup.env.crypto().sha256(&guest_serialized).to_bytes();
    
    // Commit setups
    let host_commit_req = CommitSetupReq {
        lobby_id,
        setup_hash: host_setup_hash,
    };
    setup.client.commit_setup(host_address, &host_commit_req);
    
    let guest_commit_req = CommitSetupReq {
        lobby_id,
        setup_hash: guest_setup_hash,
    };
    setup.client.commit_setup(guest_address, &guest_commit_req);
    
    // Prove setups
    let host_prove_req = ProveSetupReq {
        lobby_id,
        setup: host_setup_proof,
    };
    setup.client.prove_setup(host_address, &host_prove_req);
    
    let guest_prove_req = ProveSetupReq {
        lobby_id,
        setup: guest_setup_proof,
    };
    setup.client.prove_setup(guest_address, &guest_prove_req);
}

/// Generate a unique lobby ID to avoid conflicts across tests
fn generate_unique_lobby_id() -> u32 {
    use std::sync::atomic::{AtomicU32, Ordering};
    static COUNTER: AtomicU32 = AtomicU32::new(1);
    COUNTER.fetch_add(1, Ordering::SeqCst)
}

// endregion
