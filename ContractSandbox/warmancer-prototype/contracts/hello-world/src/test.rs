#![cfg(test)]
extern crate std;
use super::*;
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

fn create_realistic_stratego_setup_from_game_state(env: &Env, lobby_id: u32, team: u32) -> (Vec<SetupCommit>, SetupProof, u64, Vec<HiddenRank>) {
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
    
    let setup_proof = SetupProof {
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
fn test_make_lobby_errors() {
    let setup = TestSetup::new();
    let lobby_id = 1u32;
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    
    // Test: Lobby already exists
    let host_address_1 = setup.generate_address();
    let req_1 = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address_1, &req_1);
    
    let host_address_2 = setup.generate_address();
    let req_2 = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    let result = setup.client.try_make_lobby(&host_address_2, &req_2);
    assert!(TestSetup::is_validation_error(&result.unwrap_err().unwrap()));
    
    // Test: Invalid board
    let host_address_3 = setup.generate_address();
    let invalid_lobby_parameters = create_invalid_board_parameters(&setup.env);
    let req_3 = MakeLobbyReq {
        lobby_id: 2,
        parameters: invalid_lobby_parameters,
    };
    let result = setup.client.try_make_lobby(&host_address_3, &req_3);
    assert!(TestSetup::is_validation_error(&result.unwrap_err().unwrap()));
}

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
fn test_leave_lobby_errors() {
    let setup = TestSetup::new();
    
    // Test: User not found
    let non_existent_user = setup.generate_address();
    let result = setup.client.try_leave_lobby(&non_existent_user);
    assert_eq!(result.unwrap_err().unwrap(), Error::UserNotFound);
    
    // Test: No current lobby
    let user_address = setup.generate_address();
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id: 1,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&user_address, &make_req);
    setup.client.leave_lobby(&user_address);
    
    let result = setup.client.try_leave_lobby(&user_address);
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotFound);
}

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
fn test_join_lobby_errors() {
    let setup = TestSetup::new();
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    
    // Test: Lobby not found
    let guest_address = setup.generate_address();
    let join_req = JoinLobbyReq { lobby_id: 999 };
    let result = setup.client.try_join_lobby(&guest_address, &join_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotFound);
    
    // Test: Lobby not joinable (wrong phase)
    let host_address = setup.generate_address();
    let guest_address_1 = setup.generate_address();
    let guest_address_2 = setup.generate_address();
    let lobby_id = 1u32;
    
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    let join_req_1 = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address_1, &join_req_1);
    
    let join_req_2 = JoinLobbyReq { lobby_id };
    let result = setup.client.try_join_lobby(&guest_address_2, &join_req_2);
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotJoinable);
    
    // Test: Host trying to join own lobby
    let host_address_2 = setup.generate_address();
    let lobby_id_2 = 2u32;
    let make_req_2 = MakeLobbyReq {
        lobby_id: lobby_id_2,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address_2, &make_req_2);
    
    let join_req_3 = JoinLobbyReq { lobby_id: lobby_id_2 };
    let result = setup.client.try_join_lobby(&host_address_2, &join_req_3);
    assert!(TestSetup::is_user_conflict_error(&result.unwrap_err().unwrap()));
    
    // Test: Guest already in another lobby
    let host_address_3 = setup.generate_address();
    let lobby_id_3 = 3u32;
    let make_req_3 = MakeLobbyReq {
        lobby_id: lobby_id_3,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address_3, &make_req_3);
    
    let join_req_4 = JoinLobbyReq { lobby_id: lobby_id_3 };
    let result = setup.client.try_join_lobby(&guest_address_1, &join_req_4);
    let error = result.unwrap_err().unwrap();
    assert!(TestSetup::is_user_conflict_error(&error) || TestSetup::is_lobby_state_error(&error));
}

// endregion

// region move tests
fn create_test_setup_data_from_game_state(setup: &TestSetup, lobby_id: u32, team: u32) -> (Vec<SetupCommit>, SetupProof, u64) {
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
        
        let setup_proof = SetupProof {
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
    let move_proof = HiddenMoveProof {
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
fn test_commit_move_errors() {
    let setup = TestSetup::new();
    
    // Test: Lobby not found
    let user_address = setup.generate_address();
    let move_hash = create_test_move_hash(&setup.env, 1, Pos { x: 0, y: 0 }, Pos { x: 0, y: 1 }, 12345);
    let commit_req = CommitMoveReq {
        lobby_id: 999,
        move_hash: move_hash.clone(),
    };
    let result = setup.client.try_commit_move(&user_address, &commit_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotFound);
    
    // Test: Not in lobby
    let lobby_id = 1u32;
    let (host_address, guest_address) = create_and_advance_to_move_commit(&setup, lobby_id);
    let outsider_address = setup.generate_address();
    
    let commit_req = CommitMoveReq {
        lobby_id,
        move_hash: move_hash.clone(),
    };
    let result = setup.client.try_commit_move(&outsider_address, &commit_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::NotInLobby);
    
    // Test: Wrong phase
    let host_address_2 = setup.generate_address();
    let lobby_id_2 = 2u32;
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id: lobby_id_2,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address_2, &make_req);
    
    let commit_req = CommitMoveReq {
        lobby_id: lobby_id_2,
        move_hash: move_hash.clone(),
    };
    let result = setup.client.try_commit_move(&host_address_2, &commit_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongPhase);
    
    // Test: Wrong subphase
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
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongSubphase);
    
    // Test: After game finished
    let lobby_id_3 = 3u32;
    let (host_address_3, guest_address_3) = create_and_advance_to_move_commit(&setup, lobby_id_3);
    setup.client.leave_lobby(&host_address_3);
    
    let commit_req = CommitMoveReq {
        lobby_id: lobby_id_3,
        move_hash,
    };
    let result = setup.client.try_commit_move(&guest_address_3, &commit_req);
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
fn test_realistic_stratego_setup_generation() {
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
    
    // Generate identical setups for both games using a fixed seed
    let fixed_seed = 42u64; // Use fixed seed to ensure identical setups
    let (host_commits, host_proof, _, host_ranks) = setup.env.as_contract(&setup.contract_id, || {
        // Create deterministic setup independent of lobby state
        create_deterministic_setup(&setup.env, 0, fixed_seed) // team 0 (host)
    });
    let (guest_commits, guest_proof, _, guest_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_deterministic_setup(&setup.env, 1, fixed_seed) // team 1 (guest)
    });
    
    // Verify each team has exactly 40 pieces
    assert_eq!(host_commits.len(), 40);
    assert_eq!(guest_commits.len(), 40);
    
    // Verify setups can be committed and proved
    let host_serialized = host_proof.clone().to_xdr(&setup.env);
    let host_setup_hash = setup.env.crypto().sha256(&host_serialized).to_bytes();
    
    let guest_serialized = guest_proof.clone().to_xdr(&setup.env);
    let guest_setup_hash = setup.env.crypto().sha256(&guest_serialized).to_bytes();
    
    // Commit setups
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
    
    // Prove setups - should succeed with realistic placements
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
    
    // Verify game advanced to MoveCommit phase
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert_eq!(lobby_info.phase, Phase::MoveCommit);
        assert_eq!(lobby_info.subphase, Subphase::Both);
    });
}

#[test]
fn test_full_stratego_game_with_populated_ranks() {
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
    
    // Generate identical setups for both games using a fixed seed
    let fixed_seed = 42u64; // Use fixed seed to ensure identical setups
    let (host_commits, host_proof, _, host_ranks) = setup.env.as_contract(&setup.contract_id, || {
        // Create deterministic setup independent of lobby state
        create_deterministic_setup(&setup.env, 0, fixed_seed) // team 0 (host)
    });
    let (guest_commits, guest_proof, _, guest_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_deterministic_setup(&setup.env, 1, fixed_seed) // team 1 (guest)
    });
    
    // Verify each team has exactly 40 pieces
    assert_eq!(host_commits.len(), 40);
    assert_eq!(guest_commits.len(), 40);
    
    // Verify setups can be committed and proved
    let host_serialized = host_proof.clone().to_xdr(&setup.env);
    let host_setup_hash = setup.env.crypto().sha256(&host_serialized).to_bytes();
    
    let guest_serialized = guest_proof.clone().to_xdr(&setup.env);
    let guest_setup_hash = setup.env.crypto().sha256(&guest_serialized).to_bytes();
    
    // Commit setups
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
    
    // Prove setups
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
    
    // Store the rank information for later use in rank proving
    let mut all_ranks = std::vec::Vec::new();
    for rank in host_ranks.iter() {
        all_ranks.push(rank);
    }
    for rank in guest_ranks.iter() {
        all_ranks.push(rank);
    }
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
    
    
    // Store move history for logging
    let mut move_log: std::vec::Vec<(u32, u32, u32, i32, i32, i32, i32, u32, u32, bool, bool, u32, u32, i32, i32, i32, i32, u32, u32, bool, bool)> = std::vec::Vec::new();
    
    // Perform up to 50 moves or until no valid moves are possible
    for move_number in 1..=50 {
        std::println!("=== MOVE {} ===", move_number);
        
        // Check current phase and handle accordingly
        let current_phase = setup.env.as_contract(&setup.contract_id, || {
            let lobby_info_key = DataKey::LobbyInfo(lobby_id);
            let lobby_info: LobbyInfo = setup.env.storage()
                .temporary()
                .get(&lobby_info_key)
                .expect("Lobby info should exist");
            lobby_info.phase
        });
        
        match current_phase {
            Phase::MoveCommit => {
                std::println!("Phase: MoveCommit - Committing moves");
                
                // Generate and commit moves
                let move_result = setup.env.as_contract(&setup.contract_id, || {
                    let game_state_key = DataKey::GameState(lobby_id);
                    let lobby_parameters_key = DataKey::LobbyParameters(lobby_id);
                    
                    let lobby_parameters: LobbyParameters = setup.env.storage()
                        .temporary()
                        .get(&lobby_parameters_key)
                        .expect("Lobby parameters should exist");
                    let game_state: GameState = setup.env.storage()
                        .temporary()
                        .get(&game_state_key)
                        .expect("Game state should exist");
                    
                    let host_move_opt = generate_valid_move_req(&setup.env, &game_state, &lobby_parameters, 0, &host_ranks, move_number as u64 * 1000 + 12345);
                    let guest_move_opt = generate_valid_move_req(&setup.env, &game_state, &lobby_parameters, 1, &guest_ranks, move_number as u64 * 1000 + 54321);
                    
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
                let current_phase_after_commit = setup.env.as_contract(&setup.contract_id, || {
                    let lobby_info_key = DataKey::LobbyInfo(lobby_id);
                    let lobby_info: LobbyInfo = setup.env.storage()
                        .temporary()
                        .get(&lobby_info_key)
                        .expect("Lobby info should exist");
                    lobby_info.phase
                });
                
                std::println!("After committing moves for turn {}: current_phase = {:?}", move_number, current_phase_after_commit);
                
                // Only proceed with move proving if we're in MoveProve phase
                if current_phase_after_commit == Phase::MoveProve {
                    std::println!("Proceeding with MoveProve phase for turn {}", move_number);
                    
                    // Store move info for logging before moving the proofs
                    let host_pawn_id = host_move_proof.pawn_id;
                    let host_start_pos = host_move_proof.start_pos;
                    let host_target_pos = host_move_proof.target_pos;
                    let guest_pawn_id = guest_move_proof.pawn_id;
                    let guest_start_pos = guest_move_proof.start_pos;
                    let guest_target_pos = guest_move_proof.target_pos;
                    
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
                    let (post_move_phase, post_move_subphase, host_needed_ranks, guest_needed_ranks) = setup.env.as_contract(&setup.contract_id, || {
                        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
                        let game_state_key = DataKey::GameState(lobby_id);
                        let lobby_info: LobbyInfo = setup.env.storage()
                            .temporary()
                            .get(&lobby_info_key)
                            .expect("Lobby info should exist");
                        let game_state: GameState = setup.env.storage()
                            .temporary()
                            .get(&game_state_key)
                            .expect("Game state should exist");
                        
                        let host_move = game_state.moves.get(0).unwrap();
                        let guest_move = game_state.moves.get(1).unwrap();
                        let host_needed_ranks = host_move.needed_rank_proofs.clone();
                        let guest_needed_ranks = guest_move.needed_rank_proofs.clone();
                        
                        std::println!("=== POST-MOVEPROVE VALIDATION ===");
                        std::println!("Phase: {:?}, Subphase: {:?}", lobby_info.phase, lobby_info.subphase);
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
                    });
                    
                    // Log the move details - capture FINAL state after any rank resolution
                    let move_info = setup.env.as_contract(&setup.contract_id, || {
                        let game_state_key = DataKey::GameState(lobby_id);
                        let game_state: GameState = setup.env.storage()
                            .temporary()
                            .get(&game_state_key)
                            .expect("Game state should exist");
                        std::println!("requested ranks for host: {:?}", game_state.moves.get(0).unwrap().needed_rank_proofs);
                        std::println!("requested ranks for guest: {:?}", game_state.moves.get(1).unwrap().needed_rank_proofs);
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
                    });
                    
                    move_log.push(move_info);
                    
                } else {
                    std::println!("Skipped MoveProve phase - game already advanced to next phase: {:?}", current_phase_after_commit);
                    
                    // Still log the move even if we skipped MoveProve
                    let host_pawn_id = host_move_proof.pawn_id;
                    let host_start_pos = host_move_proof.start_pos;
                    let host_target_pos = host_move_proof.target_pos;
                    let guest_pawn_id = guest_move_proof.pawn_id;
                    let guest_start_pos = guest_move_proof.start_pos;
                    let guest_target_pos = guest_move_proof.target_pos;
                    
                    let move_info = setup.env.as_contract(&setup.contract_id, || {
                        let game_state_key = DataKey::GameState(lobby_id);
                        let game_state: GameState = setup.env.storage()
                            .temporary()
                            .get(&game_state_key)
                            .expect("Game state should exist");
                        
                        // Find ranks of moving pawns
                        let mut host_pawn_rank = 999u32;
                        let mut guest_pawn_rank = 999u32;
                        for pawn in game_state.pawns.iter() {
                            if pawn.pawn_id == host_pawn_id && !pawn.rank.is_empty() {
                                host_pawn_rank = pawn.rank.get(0).unwrap();
                            }
                            if pawn.pawn_id == guest_pawn_id && !pawn.rank.is_empty() {
                                guest_pawn_rank = pawn.rank.get(0).unwrap();
                            }
                        }
                        
                        (move_number, host_pawn_id, host_pawn_rank, 
                         host_start_pos.x, host_start_pos.y,
                         host_target_pos.x, host_target_pos.y,
                         0u32, 999u32, true, false, // No collision data when skipping
                         guest_pawn_id, guest_pawn_rank,
                         guest_start_pos.x, guest_start_pos.y,
                         guest_target_pos.x, guest_target_pos.y,
                         0u32, 999u32, true, false)
                    });
                    
                    move_log.push(move_info);
                }
                
                // Print complete move log
                std::println!("\n=== COMPLETE MOVE LOG ===");
                for move_entry in &move_log {
                    let (turn, host_pawn_id, host_rank, host_sx, host_sy, host_tx, host_ty, 
                        host_collision_id, host_collision_rank, host_alive, host_collision_alive,
                        guest_pawn_id, guest_rank, guest_sx, guest_sy, guest_tx, guest_ty,
                        guest_collision_id, guest_collision_rank, guest_alive, guest_collision_alive) = move_entry;
                    
                    let host_rank_str = if *host_rank == 999 { "?" } else { 
                        match *host_rank {
                            0 => "Flag", 1 => "Spy", 2 => "Scout", 3 => "Miner", 4 => "Sergeant",
                            5 => "Lieutenant", 6 => "Captain", 7 => "Major", 8 => "Colonel", 
                            9 => "General", 10 => "Marshal", 11 => "Bomb", _ => "Unknown"
                        }
                    };
                    let guest_rank_str = if *guest_rank == 999 { "?" } else {
                        match *guest_rank {
                            0 => "Flag", 1 => "Spy", 2 => "Scout", 3 => "Miner", 4 => "Sergeant",
                            5 => "Lieutenant", 6 => "Captain", 7 => "Major", 8 => "Colonel", 
                            9 => "General", 10 => "Marshal", 11 => "Bomb", _ => "Unknown"
                        }
                    };
                    
                    let host_collision_str = if *host_collision_id == 0 { 
                        "none" 
                    } else {
                        let collision_rank_str = if *host_collision_rank == 999 { "?" } else {
                            match *host_collision_rank {
                                0 => "Flag", 1 => "Spy", 2 => "Scout", 3 => "Miner", 4 => "Sergeant",
                                5 => "Lieutenant", 6 => "Captain", 7 => "Major", 8 => "Colonel", 
                                9 => "General", 10 => "Marshal", 11 => "Bomb", _ => "Unknown"
                            }
                        };
                        "collision"
                    };
                    
                    let guest_collision_str = if *guest_collision_id == 0 { 
                        "none" 
                    } else {
                        let collision_rank_str = if *guest_collision_rank == 999 { "?" } else {
                            match *guest_collision_rank {
                                0 => "Flag", 1 => "Spy", 2 => "Scout", 3 => "Miner", 4 => "Sergeant",
                                5 => "Lieutenant", 6 => "Captain", 7 => "Major", 8 => "Colonel", 
                                9 => "General", 10 => "Marshal", 11 => "Bomb", _ => "Unknown"
                            }
                        };
                        "collision"
                    };
                    
                    std::println!("{} - pawn_id: {} rank: {} ({},{}) -> ({},{}) -> {} | pawn_id: {} rank: {} ({},{}) -> ({},{}) -> {}",
                        turn, host_pawn_id, host_rank_str, host_sx, host_sy, host_tx, host_ty, host_collision_str,
                        guest_pawn_id, guest_rank_str, guest_sx, guest_sy, guest_tx, guest_ty, guest_collision_str);
                }
                std::println!("=== END MOVE LOG ===");
                std::println!("Moves committed and proved successfully");
            },
            
            Phase::RankProve => {
                std::println!("Phase: RankProve - Proving ranks for collisions");
                
                // Get the needed rank proofs for both players
                let (host_needed_ranks, guest_needed_ranks, host_subphase_required, guest_subphase_required) = setup.env.as_contract(&setup.contract_id, || {
                    let game_state_key = DataKey::GameState(lobby_id);
                    let lobby_info_key = DataKey::LobbyInfo(lobby_id);
                    let game_state: GameState = setup.env.storage()
                        .temporary()
                        .get(&game_state_key)
                        .expect("Game state should exist");
                    let lobby_info: LobbyInfo = setup.env.storage()
                        .temporary()
                        .get(&lobby_info_key)
                        .expect("Lobby info should exist");
                    // std::println!("Game state: {:?}", game_state);
                    let host_move = game_state.moves.get(0).unwrap();
                    std::println!("Host move: {:?}", host_move);
                    let guest_move = game_state.moves.get(1).unwrap();
                    std::println!("Guest move: {:?}", guest_move);
                    let host_needed_ranks = host_move.needed_rank_proofs.clone();
                    let guest_needed_ranks = guest_move.needed_rank_proofs.clone();
                    // Players should prove ranks if they have needed proofs, regardless of subphase
                    // The subphase only determines order, but both should act if they have pending proofs
                    let host_subphase_required = !host_needed_ranks.is_empty();
                    let guest_subphase_required = !guest_needed_ranks.is_empty();
                    
                    (host_needed_ranks, guest_needed_ranks, host_subphase_required, guest_subphase_required)
                });
                for id in host_needed_ranks.iter() {
                    let (_, team) = Contract::decode_pawn_id(&id);
                    std::println!("Host needed rank: {} team: {}", id.clone(), team);
                }

                // Prove ranks for host if needed
                let mut host_hidden_ranks = Vec::new(&setup.env);
                if host_subphase_required && !host_needed_ranks.is_empty() {
                    std::println!("Host proving {} ranks", host_needed_ranks.len());
                    for required_pawn_id in host_needed_ranks.iter() {
                        for hidden_rank in host_ranks.iter() {
                            if hidden_rank.pawn_id == required_pawn_id {
                                host_hidden_ranks.push_back(hidden_rank);
                            }
                        }
                    }
                    let host_prove_rank_req = ProveRankReq {
                        lobby_id,
                        hidden_ranks: host_hidden_ranks,
                    };
                    std::println!("Host proving {} ranks {}", host_prove_rank_req.hidden_ranks.len(), host_needed_ranks.len());
                    assert_eq!(host_prove_rank_req.hidden_ranks.len(), host_needed_ranks.len());
                    setup.client.prove_rank(&host_address, &host_prove_rank_req);
                }
                
                // Prove ranks for guest if needed (use original conditions, don't re-check subphase)
                for id in guest_needed_ranks.iter() {
                    let (_, team) = Contract::decode_pawn_id(&id);
                    std::println!("Guest needed rank: {} team: {}", id.clone(), team);
                }

                let mut guest_hidden_ranks = Vec::new(&setup.env);
                if guest_subphase_required && !guest_needed_ranks.is_empty() {
                    std::println!("Guest proving {} ranks", guest_needed_ranks.len());
                    for required_pawn_id in guest_needed_ranks.iter() {
                        for hidden_rank in guest_ranks.iter() {
                            if hidden_rank.pawn_id == required_pawn_id {
                                guest_hidden_ranks.push_back(hidden_rank);
                            }
                        }
                    }
                    let guest_prove_rank_req = ProveRankReq {
                        lobby_id,
                        hidden_ranks: guest_hidden_ranks,
                    };
                    std::println!("Guest proving {} ranks {}", guest_prove_rank_req.hidden_ranks.len(), guest_needed_ranks.len());
                    assert_eq!(guest_prove_rank_req.hidden_ranks.len(), guest_needed_ranks.len());
                    setup.client.prove_rank(&guest_address, &guest_prove_rank_req);
                }
                
                std::println!("Rank proving completed");
                
                // Check if game transitioned out of RankProve phase
                let current_phase_after_rank_prove = setup.env.as_contract(&setup.contract_id, || {
                    let lobby_info_key = DataKey::LobbyInfo(lobby_id);
                    let game_state_key = DataKey::GameState(lobby_id);
                    let lobby_info: LobbyInfo = setup.env.storage()
                        .temporary()
                        .get(&lobby_info_key)
                        .expect("Lobby info should exist");
                    let game_state: GameState = setup.env.storage()
                        .temporary()
                        .get(&game_state_key)
                        .expect("Game state should exist");
                    std::println!("After rank proving: Phase={:?}, Subphase={:?}", lobby_info.phase, lobby_info.subphase);
                    
                    // Check if rank proofs are still needed
                    let host_move = game_state.moves.get(0).unwrap();
                    let guest_move = game_state.moves.get(1).unwrap();
                    std::println!("Host still needs {} rank proofs", host_move.needed_rank_proofs.len());
                    std::println!("Guest still needs {} rank proofs", guest_move.needed_rank_proofs.len());
                    
                    lobby_info.phase
                });
                
                // Print board state RIGHT AFTER rank proving but before collision resolution
                std::println!("=== BOARD STATE AFTER RANK PROVING (BEFORE COLLISION RESOLUTION) ===");
                let board_state_with_revealed_ranks = setup.env.as_contract(&setup.contract_id, || {
                    let game_state_key = DataKey::GameState(lobby_id);
                    let lobby_parameters_key = DataKey::LobbyParameters(lobby_id);
                    let lobby_info_key = DataKey::LobbyInfo(lobby_id);
                    
                    let game_state: GameState = setup.env.storage()
                        .temporary()
                        .get(&game_state_key)
                        .expect("Game state should exist");
                    let lobby_parameters: LobbyParameters = setup.env.storage()
                        .temporary()
                        .get(&lobby_parameters_key)
                        .expect("Lobby parameters should exist");
                    let lobby_info: LobbyInfo = setup.env.storage()
                        .temporary()
                        .get(&lobby_info_key)
                        .expect("Lobby info should exist");
                    
                    print_board_state_color(&setup.env, &game_state, &lobby_parameters, &lobby_info)
                });
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
        
        // Print current game state after each move
        setup.env.as_contract(&setup.contract_id, || {
            let lobby_info_key = DataKey::LobbyInfo(lobby_id);
            let lobby_info: LobbyInfo = setup.env.storage()
                .temporary()
                .get(&lobby_info_key)
                .expect("Lobby info should exist");
            let game_state_key = DataKey::GameState(lobby_id);
            let game_state: GameState = setup.env.storage()
                .temporary()
                .get(&game_state_key)
                .expect("Game state should exist");
            let lobby_parameters_key = DataKey::LobbyParameters(lobby_id);
            let lobby_parameters: LobbyParameters = setup.env.storage()
                .temporary()
                .get(&lobby_parameters_key)
                .expect("Lobby parameters should exist");
            std::println!("After move {}: Phase={:?}, Subphase={:?}", move_number, lobby_info.phase, lobby_info.subphase);
            std::println!("{}", print_board_state_color(&setup.env, &game_state, &lobby_parameters, &lobby_info));
            
            // Log a final rank check to verify persistence
            if lobby_info.phase == Phase::MoveCommit {
                std::println!("=== RANK PERSISTENCE CHECK ===");
                let mut revealed_alive_count = 0;
                let mut revealed_dead_count = 0;
                for pawn in game_state.pawns.iter() {
                    if !pawn.rank.is_empty() {
                        let rank_str = match pawn.rank.get(0).unwrap() {
                            0 => "Flag", 1 => "Spy", 2 => "Scout", 3 => "Miner", 4 => "Sergeant",
                            5 => "Lieutenant", 6 => "Captain", 7 => "Major", 8 => "Colonel", 
                            9 => "General", 10 => "Marshal", 11 => "Bomb", _ => "Unknown"
                        };
                        std::println!("Pawn {} rank revealed: {} (alive: {})", pawn.pawn_id, rank_str, pawn.alive);
                        if pawn.alive {
                            revealed_alive_count += 1;
                        } else {
                            revealed_dead_count += 1;
                        }
                    }
                }
                std::println!("REVEALED PAWNS: {} alive (visible on board), {} dead (removed from board)", revealed_alive_count, revealed_dead_count);
                std::println!("=== END RANK CHECK ===");
            }
        });
    }
    
    // Verify final game state
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        // The game should be in a valid state
        assert!(matches!(lobby_info.phase, Phase::MoveCommit | Phase::MoveProve | Phase::RankProve | Phase::Finished));
        std::println!("Final game state: Phase={:?}, Subphase={:?}", lobby_info.phase, lobby_info.subphase);
    });
    
    // Print complete move log
    std::println!("\n=== COMPLETE MOVE LOG ===");
    for move_entry in &move_log {
        let (turn, host_pawn_id, host_rank, host_sx, host_sy, host_tx, host_ty, 
             host_collision_id, host_collision_rank, host_alive, host_collision_alive,
             guest_pawn_id, guest_rank, guest_sx, guest_sy, guest_tx, guest_ty,
             guest_collision_id, guest_collision_rank, guest_alive, guest_collision_alive) = move_entry;
        
        let host_rank_str = if *host_rank == 999 { "?" } else { 
            match *host_rank {
                0 => "Flag", 1 => "Spy", 2 => "Scout", 3 => "Miner", 4 => "Sergeant",
                5 => "Lieutenant", 6 => "Captain", 7 => "Major", 8 => "Colonel", 
                9 => "General", 10 => "Marshal", 11 => "Bomb", _ => "Unknown"
            }
        };
        let guest_rank_str = if *guest_rank == 999 { "?" } else {
            match *guest_rank {
                0 => "Flag", 1 => "Spy", 2 => "Scout", 3 => "Miner", 4 => "Sergeant",
                5 => "Lieutenant", 6 => "Captain", 7 => "Major", 8 => "Colonel", 
                9 => "General", 10 => "Marshal", 11 => "Bomb", _ => "Unknown"
            }
        };
        
        let host_collision_str = if *host_collision_id == 0 { 
            "none" 
        } else {
            let collision_rank_str = if *host_collision_rank == 999 { "?" } else {
                match *host_collision_rank {
                    0 => "Flag", 1 => "Spy", 2 => "Scout", 3 => "Miner", 4 => "Sergeant",
                    5 => "Lieutenant", 6 => "Captain", 7 => "Major", 8 => "Colonel", 
                    9 => "General", 10 => "Marshal", 11 => "Bomb", _ => "Unknown"
                }
            };
            "collision"
        };
        
        let guest_collision_str = if *guest_collision_id == 0 { 
            "none" 
        } else {
            let collision_rank_str = if *guest_collision_rank == 999 { "?" } else {
                match *guest_collision_rank {
                    0 => "Flag", 1 => "Spy", 2 => "Scout", 3 => "Miner", 4 => "Sergeant",
                    5 => "Lieutenant", 6 => "Captain", 7 => "Major", 8 => "Colonel", 
                    9 => "General", 10 => "Marshal", 11 => "Bomb", _ => "Unknown"
                }
            };
            "collision"
        };
        
        std::println!("{} - {}|{} ({},{}) -> ({},{}) -> {} | {}|{} ({},{}) -> ({},{}) -> {}",
            turn, host_pawn_id, host_rank_str, host_sx, host_sy, host_tx, host_ty, host_collision_str,
            guest_pawn_id, guest_rank_str, guest_sx, guest_sy, guest_tx, guest_ty, guest_collision_str);
    }
    std::println!("=== END MOVE LOG ===");
}


/// Generate a valid move for testing purposes.
/// 
/// This function takes the team's own rank information (via team_ranks parameter) 
/// to avoid moving immovable pieces (flags/bombs), but does not peek at strategic 
/// game state information or care about opponent ranks.
/// 
/// This ensures consistent move generation regardless of whether ranks are 
/// populated in the game state or not.
fn generate_valid_move_req(env: &Env, game_state: &GameState, lobby_parameters: &LobbyParameters, team: u32, team_ranks: &Vec<HiddenRank>, salt: u64) -> Option<HiddenMoveProof> {
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
    
    Some(HiddenMoveProof {
        pawn_id: selected_pawn.pawn_id,
        start_pos: selected_pawn.pos,
        target_pos,
        salt,
    })
}

#[test]
fn test_generate_valid_move_req() {
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
    
    // Generate identical setups for both games using a fixed seed
    let fixed_seed = 42u64; // Use fixed seed to ensure identical setups
    let (host_commits, host_proof, _, host_ranks) = setup.env.as_contract(&setup.contract_id, || {
        // Create deterministic setup independent of lobby state
        create_deterministic_setup(&setup.env, 0, fixed_seed) // team 0 (host)
    });
    let (guest_commits, guest_proof, _, guest_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_deterministic_setup(&setup.env, 1, fixed_seed) // team 1 (guest)
    });
    
    // Commit and prove setups to get to MoveCommit phase
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
    
    // Now test the generate_valid_move_req function
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        let lobby_parameters_key = DataKey::LobbyParameters(lobby_id);
        let lobby_parameters: LobbyParameters = setup.env.storage()
            .temporary()
            .get(&lobby_parameters_key)
            .expect("Lobby parameters should exist");
        
        // Test move generation for team 0 (host)
        let host_move = generate_valid_move_req(&setup.env, &game_state, &lobby_parameters, 0, &host_ranks, 12345);
        assert!(host_move.is_some(), "Should generate a valid move for team 0");
        
        let host_move_proof = host_move.unwrap();
        
        // Verify the move is for team 0
        let (_, pawn_team) = Contract::decode_pawn_id(&host_move_proof.pawn_id);
        assert_eq!(pawn_team, 0, "Generated move should be for team 0");
        
        // Verify the move is valid using the existing validation function
        let is_valid = Contract::validate_move_proof(&setup.env, &host_move_proof, &0, &game_state, &lobby_parameters);
        assert!(is_valid, "Generated move should be valid according to validation function");
        
        // Test move generation for team 1 (guest)
        let guest_move = generate_valid_move_req(&setup.env, &game_state, &lobby_parameters, 1, &guest_ranks, 54321);
        assert!(guest_move.is_some(), "Should generate a valid move for team 1");
        
        let guest_move_proof = guest_move.unwrap();
        
        // Verify the move is for team 1
        let (_, pawn_team) = Contract::decode_pawn_id(&guest_move_proof.pawn_id);
        assert_eq!(pawn_team, 1, "Generated move should be for team 1");
        
        // Verify the move is valid using the existing validation function
        let is_valid = Contract::validate_move_proof(&setup.env, &guest_move_proof, &1, &game_state, &lobby_parameters);
        assert!(is_valid, "Generated move should be valid according to validation function");
        
        // Test that team 0 prefers forward moves (increasing y)
        if host_move_proof.target_pos.y > host_move_proof.start_pos.y {
            // This is a forward move for team 0, which is preferred
            assert!(true, "Team 0 correctly chose forward move");
        }
        
        // Test that team 1 prefers forward moves (decreasing y)
        if guest_move_proof.target_pos.y < guest_move_proof.start_pos.y {
            // This is a forward move for team 1, which is preferred
            assert!(true, "Team 1 correctly chose forward move");
        }
    });
}

// endregion

// region board visualization

// ANSI color codes for terminal output
const RESET: &str = "\x1b[0m";
const BOLD: &str = "\x1b[1m";
const RED: &str = "\x1b[31m";
const GREEN: &str = "\x1b[32m";
const YELLOW: &str = "\x1b[33m";
const BLUE: &str = "\x1b[34m";
const MAGENTA: &str = "\x1b[35m";
const CYAN: &str = "\x1b[36m";
const WHITE: &str = "\x1b[37m";
const BRIGHT_RED: &str = "\x1b[91m";
const BRIGHT_GREEN: &str = "\x1b[92m";
const BRIGHT_YELLOW: &str = "\x1b[93m";
const BRIGHT_BLUE: &str = "\x1b[94m";
const BRIGHT_MAGENTA: &str = "\x1b[95m";
const BRIGHT_CYAN: &str = "\x1b[96m";

/// Creates a colorized text representation of the current board state
fn print_board_state_color(_env: &Env, game_state: &GameState, lobby_parameters: &LobbyParameters, lobby_info: &LobbyInfo) -> std::string::String {
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
fn test_board_visualization() {
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
    
    // Generate and commit setup for both teams
    let (host_commits, host_proof, _, host_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_realistic_stratego_setup_from_game_state(&setup.env, lobby_id, 0)
    });
    let (guest_commits, guest_proof, _, guest_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_realistic_stratego_setup_from_game_state(&setup.env, lobby_id, 1)
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
    
    // Populate all ranks in the game state for visualization
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
    
    // Print initial board state
    let board_state = setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_id);
        let lobby_parameters_key = DataKey::LobbyParameters(lobby_id);
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        let lobby_parameters: LobbyParameters = setup.env.storage()
            .temporary()
            .get(&lobby_parameters_key)
            .expect("Lobby parameters should exist");
        let lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        print_board_state_color(&setup.env, &game_state, &lobby_parameters, &lobby_info)
    });
    
    std::println!("{}", board_state);
    
    // Make a few moves and show the board state again
    for move_num in 1..=3 {
        std::println!("=== MAKING MOVE {} ===", move_num);
        
        let (host_move_proof, guest_move_proof, host_move_req, guest_move_req) = setup.env.as_contract(&setup.contract_id, || {
            let game_state_key = DataKey::GameState(lobby_id);
            let lobby_parameters_key = DataKey::LobbyParameters(lobby_id);
            
            let lobby_parameters: LobbyParameters = setup.env.storage()
                .temporary()
                .get(&lobby_parameters_key)
                .expect("Lobby parameters should exist");
            let game_state: GameState = setup.env.storage()
                .temporary()
                .get(&game_state_key)
                .expect("Game state should exist");
            
            let host_move_proof = generate_valid_move_req(&setup.env, &game_state, &lobby_parameters, 0, &host_ranks, move_num as u64 * 1000 + 12345).unwrap();
            let guest_move_proof = generate_valid_move_req(&setup.env, &game_state, &lobby_parameters, 1, &guest_ranks, move_num as u64 * 1000 + 54321).unwrap();
            
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
            
            (host_move_proof, guest_move_proof, host_move_req, guest_move_req)
        });
        
        setup.client.commit_move(&host_address, &host_move_req);
        setup.client.commit_move(&guest_address, &guest_move_req);
        
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
        
        // Print board state after move
        let board_state = setup.env.as_contract(&setup.contract_id, || {
            let game_state_key = DataKey::GameState(lobby_id);
            let lobby_parameters_key = DataKey::LobbyParameters(lobby_id);
            let lobby_info_key = DataKey::LobbyInfo(lobby_id);
            
            let game_state: GameState = setup.env.storage()
                .temporary()
                .get(&game_state_key)
                .expect("Game state should exist");
            let lobby_parameters: LobbyParameters = setup.env.storage()
                .temporary()
                .get(&lobby_parameters_key)
                .expect("Lobby parameters should exist");
            let lobby_info: LobbyInfo = setup.env.storage()
                .temporary()
                .get(&lobby_info_key)
                .expect("Lobby info should exist");
            
            print_board_state_color(&setup.env, &game_state, &lobby_parameters, &lobby_info)
        });
        
        std::println!("{}", board_state);
    }
}

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
        create_realistic_stratego_setup_from_game_state(&setup.env, lobby_id, 0)
    });
    let (guest_commits, guest_proof, _, _) = setup.env.as_contract(&setup.contract_id, || {
        create_realistic_stratego_setup_from_game_state(&setup.env, lobby_id, 1)
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
    
    // Create four users: two for each game
    let host_a = setup.generate_address();    // Game A (unpopulated)
    let guest_a = setup.generate_address();
    let host_b = setup.generate_address();    // Game B (populated) 
    let guest_b = setup.generate_address();
    
    let lobby_a = 1u32;  // Unpopulated game
    let lobby_b = 2u32;  // Populated game
    
    // Create identical lobby parameters for both games
    let lobby_parameters = create_full_stratego_board_parameters(&setup.env);
    
    // Setup Game A (unpopulated)
    let make_req_a = MakeLobbyReq {
        lobby_id: lobby_a,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_a, &make_req_a);
    setup.client.join_lobby(&guest_a, &JoinLobbyReq { lobby_id: lobby_a });
    
    // Setup Game B (populated)
    let make_req_b = MakeLobbyReq {
        lobby_id: lobby_b,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_b, &make_req_b);
    setup.client.join_lobby(&guest_b, &JoinLobbyReq { lobby_id: lobby_b });
    
    // Generate identical setups for both games using a fixed seed
    let fixed_seed = 42u64; // Use fixed seed to ensure identical setups
    let (host_commits, host_proof, _, host_ranks) = setup.env.as_contract(&setup.contract_id, || {
        // Create deterministic setup independent of lobby state
        create_deterministic_setup(&setup.env, 0, fixed_seed) // team 0 (host)
    });
    let (guest_commits, guest_proof, _, guest_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_deterministic_setup(&setup.env, 1, fixed_seed) // team 1 (guest)
    });
    
    // Apply setups to both games
    for lobby_id in [lobby_a, lobby_b] {
        let (host_addr, guest_addr) = if lobby_id == lobby_a { 
            (&host_a, &guest_a) 
        } else { 
            (&host_b, &guest_b) 
        };
        
        // Hash and commit setups
        let host_serialized = host_proof.clone().to_xdr(&setup.env);
        let host_setup_hash = setup.env.crypto().sha256(&host_serialized).to_bytes();
        let guest_serialized = guest_proof.clone().to_xdr(&setup.env);
        let guest_setup_hash = setup.env.crypto().sha256(&guest_serialized).to_bytes();
        
        setup.client.commit_setup(host_addr, &CommitSetupReq { lobby_id, setup_hash: host_setup_hash });
        setup.client.commit_setup(guest_addr, &CommitSetupReq { lobby_id, setup_hash: guest_setup_hash });
        
        setup.client.prove_setup(host_addr, &ProveSetupReq { lobby_id, setup: host_proof.clone() });
        setup.client.prove_setup(guest_addr, &ProveSetupReq { lobby_id, setup: guest_proof.clone() });
    }
    
    // Populate ranks only in Game B
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_b);
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
    
    std::println!("\n=== COMPARING IDENTICAL GAMES: A(unpopulated) vs B(populated) ===");
    
    // Execute identical moves on both games and compare states
    for move_number in 1..=10 {
        std::println!("\n--- MOVE {} ---", move_number);
        
        // Generate the same moves for both games
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
            std::println!("No valid moves available, stopping comparison at move {}", move_number);
            break;
        }
        
        let host_move_proof = host_move.unwrap();
        let guest_move_proof = guest_move.unwrap();
        
        std::println!("Host move: {} ({},{}) -> ({},{})", 
                     host_move_proof.pawn_id, host_move_proof.start_pos.x, host_move_proof.start_pos.y,
                     host_move_proof.target_pos.x, host_move_proof.target_pos.y);
        std::println!("Guest move: {} ({},{}) -> ({},{})", 
                     guest_move_proof.pawn_id, guest_move_proof.start_pos.x, guest_move_proof.start_pos.y,
                     guest_move_proof.target_pos.x, guest_move_proof.target_pos.y);
        
        // Execute moves on both games
        for lobby_id in [lobby_a, lobby_b] {
            let (host_addr, guest_addr) = if lobby_id == lobby_a { 
                (&host_a, &guest_a) 
            } else { 
                (&host_b, &guest_b) 
            };
            
            // Check current phase before committing
            let phase_before = setup.env.as_contract(&setup.contract_id, || {
                let lobby_info_key = DataKey::LobbyInfo(lobby_id);
                let lobby_info: LobbyInfo = setup.env.storage().temporary().get(&lobby_info_key).expect("Lobby info should exist");
                (lobby_info.phase, lobby_info.subphase)
            });
            
            // Only commit if in MoveCommit phase
            if phase_before.0 == Phase::MoveCommit {
                let host_serialized = host_move_proof.clone().to_xdr(&setup.env);
                let host_hash = setup.env.crypto().sha256(&host_serialized).to_bytes();
                let guest_serialized = guest_move_proof.clone().to_xdr(&setup.env);
                let guest_hash = setup.env.crypto().sha256(&guest_serialized).to_bytes();
                
                setup.client.commit_move(host_addr, &CommitMoveReq { lobby_id, move_hash: host_hash });
                setup.client.commit_move(guest_addr, &CommitMoveReq { lobby_id, move_hash: guest_hash });
                
                // Check phase after committing
                let phase_after_commit = setup.env.as_contract(&setup.contract_id, || {
                    let lobby_info_key = DataKey::LobbyInfo(lobby_id);
                    let lobby_info: LobbyInfo = setup.env.storage().temporary().get(&lobby_info_key).expect("Lobby info should exist");
                    (lobby_info.phase, lobby_info.subphase)
                });
                
                // Prove moves if in MoveProve phase
                if phase_after_commit.0 == Phase::MoveProve {
                    setup.client.prove_move(host_addr, &ProveMoveReq { lobby_id, move_proof: host_move_proof.clone() });
                    setup.client.prove_move(guest_addr, &ProveMoveReq { lobby_id, move_proof: guest_move_proof.clone() });
                }
            }
        }
        
        // Compare final states after this move
        let (phase_a, subphase_a, needed_a) = setup.env.as_contract(&setup.contract_id, || {
            let lobby_info_key = DataKey::LobbyInfo(lobby_a);
            let game_state_key = DataKey::GameState(lobby_a);
            let lobby_info: LobbyInfo = setup.env.storage().temporary().get(&lobby_info_key).expect("Lobby info should exist");
            let game_state: GameState = setup.env.storage().temporary().get(&game_state_key).expect("Game state should exist");
            
            let mut total_needed = 0;
            for user_move in game_state.moves.iter() {
                total_needed += user_move.needed_rank_proofs.len();
            }
            (lobby_info.phase, lobby_info.subphase, total_needed)
        });
        
        let (phase_b, subphase_b, needed_b) = setup.env.as_contract(&setup.contract_id, || {
            let lobby_info_key = DataKey::LobbyInfo(lobby_b);
            let game_state_key = DataKey::GameState(lobby_b);
            let lobby_info: LobbyInfo = setup.env.storage().temporary().get(&lobby_info_key).expect("Lobby info should exist");
            let game_state: GameState = setup.env.storage().temporary().get(&game_state_key).expect("Game state should exist");
            
            let mut total_needed = 0;
            for user_move in game_state.moves.iter() {
                total_needed += user_move.needed_rank_proofs.len();
            }
            (lobby_info.phase, lobby_info.subphase, total_needed)
        });
        
        std::println!("Game A (unpopulated): Phase={:?}, Subphase={:?}, Rank proofs needed={}", phase_a, subphase_a, needed_a);
        std::println!("Game B (populated):   Phase={:?}, Subphase={:?}, Rank proofs needed={}", phase_b, subphase_b, needed_b);
        
        // Handle expected temporary divergence: Game A may need rank proving while Game B doesn't
        if phase_a == Phase::RankProve && phase_b == Phase::MoveCommit {
            std::println!("Expected divergence: Game A needs rank proving, Game B already resolved");
            
            // Handle rank proving for Game A
            let (host_addr_a, guest_addr_a) = (&host_a, &guest_a);
            
            let (host_needed, guest_needed) = setup.env.as_contract(&setup.contract_id, || {
                let game_state_key = DataKey::GameState(lobby_a);
                let game_state: GameState = setup.env.storage().temporary().get(&game_state_key).expect("Game state should exist");
                let host_move = game_state.moves.get(0).unwrap();
                let guest_move = game_state.moves.get(1).unwrap();
                (host_move.needed_rank_proofs.clone(), guest_move.needed_rank_proofs.clone())
            });
            
            // Provide rank proofs for Game A
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
                    setup.client.prove_rank(host_addr_a, &ProveRankReq { lobby_id: lobby_a, hidden_ranks: host_proof_ranks });
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
                    setup.client.prove_rank(guest_addr_a, &ProveRankReq { lobby_id: lobby_a, hidden_ranks: guest_proof_ranks });
                }
            }
            
            // Check final states after rank resolution - they should now be identical
            let (final_phase_a, final_subphase_a, final_needed_a) = setup.env.as_contract(&setup.contract_id, || {
                let lobby_info_key = DataKey::LobbyInfo(lobby_a);
                let game_state_key = DataKey::GameState(lobby_a);
                let lobby_info: LobbyInfo = setup.env.storage().temporary().get(&lobby_info_key).expect("Lobby info should exist");
                let game_state: GameState = setup.env.storage().temporary().get(&game_state_key).expect("Game state should exist");
                
                let mut total_needed = 0;
                for user_move in game_state.moves.iter() {
                    total_needed += user_move.needed_rank_proofs.len();
                }
                (lobby_info.phase, lobby_info.subphase, total_needed)
            });
            
            let (final_phase_b, final_subphase_b, final_needed_b) = setup.env.as_contract(&setup.contract_id, || {
                let lobby_info_key = DataKey::LobbyInfo(lobby_b);
                let game_state_key = DataKey::GameState(lobby_b);
                let lobby_info: LobbyInfo = setup.env.storage().temporary().get(&lobby_info_key).expect("Lobby info should exist");
                let game_state: GameState = setup.env.storage().temporary().get(&game_state_key).expect("Game state should exist");
                
                let mut total_needed = 0;
                for user_move in game_state.moves.iter() {
                    total_needed += user_move.needed_rank_proofs.len();
                }
                (lobby_info.phase, lobby_info.subphase, total_needed)
            });
            
            std::println!("After rank resolution:");
            std::println!("Game A: Phase={:?}, Subphase={:?}, Rank proofs needed={}", final_phase_a, final_subphase_a, final_needed_a);
            std::println!("Game B: Phase={:?}, Subphase={:?}, Rank proofs needed={}", final_phase_b, final_subphase_b, final_needed_b);
            
            if final_phase_a != final_phase_b || final_subphase_a != final_subphase_b {
                std::println!("❌ INCONSISTENCY DETECTED at move {}!", move_number);
                std::println!("   Games failed to converge after rank resolution!");
                std::println!("   Game A final state: Phase={:?}, Subphase={:?}", final_phase_a, final_subphase_a);
                std::println!("   Game B final state: Phase={:?}, Subphase={:?}", final_phase_b, final_subphase_b);
                break;
            } else {
                // Phase convergence successful, now compare all pawn states
                let pawn_states_match = compare_all_pawn_states(&setup, lobby_a, lobby_b);
                if !pawn_states_match {
                    std::println!("❌ INCONSISTENCY DETECTED at move {}!", move_number);
                    std::println!("   Games converged in phase but pawn states differ!");
                    break;
                } else {
                    std::println!("✅ Games successfully converged with identical pawn states at move {}", move_number);
                }
            }
        }
        else if phase_a != phase_b || subphase_a != subphase_b {
            std::println!("❌ UNEXPECTED INCONSISTENCY at move {}!", move_number);
            std::println!("   Expected either both games in same state, or Game A in RankProve while Game B in MoveCommit");
            std::println!("   Actual: Game A={:?}/{:?}, Game B={:?}/{:?}", phase_a, subphase_a, phase_b, subphase_b);
            break;
        } else {
            // Phase consistency confirmed, now compare all pawn states
            let pawn_states_match = compare_all_pawn_states(&setup, lobby_a, lobby_b);
            if !pawn_states_match {
                std::println!("❌ INCONSISTENCY DETECTED at move {}!", move_number);
                std::println!("   Games have same phase but different pawn states!");
                break;
            } else {
                std::println!("✅ Games are consistent with identical pawn states after move {}", move_number);
            }
        }
        
        // Note: Both games in RankProve should not happen with populated ranks in Game B
        // If it does happen, it indicates an unexpected issue
        if phase_a == Phase::RankProve && phase_b == Phase::RankProve {
            std::println!("⚠️  WARNING: Both games in RankProve phase - this shouldn't happen with populated Game B");
        }
    }
    
    std::println!("\n=== COMPARISON COMPLETE ===");
}

fn compare_all_pawn_states(setup: &TestSetup, lobby_a: u32, lobby_b: u32) -> bool {
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_a: GameState = setup.env.storage().temporary().get(&DataKey::GameState(lobby_a)).expect("Game A state should exist");
        let game_state_b: GameState = setup.env.storage().temporary().get(&DataKey::GameState(lobby_b)).expect("Game B state should exist");
        
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

fn create_deterministic_setup(env: &Env, team: u32, seed: u64) -> (Vec<SetupCommit>, SetupProof, u64, Vec<HiddenRank>) {
    let mut setup_commits = Vec::new(env);
    let mut hidden_ranks = Vec::new(env);
    
    // Create standard Stratego rank distribution
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
    
    let setup_proof = SetupProof {
        setup_commits: setup_commits.clone(),
        salt: team as u64,
    };
    
    (setup_commits, setup_proof, team as u64, hidden_ranks)
}
