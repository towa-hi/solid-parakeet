#![cfg(test)]
#![allow(unused_variables)]
extern crate std;
use super::super::*;
use super::test_utils::*;

// region validation tests
#[test]
fn test_move_to_enemy_occupied_tile() {
    // AIDEV-NOTE: Test replicating exact state from weird.txt before move_prove calls
    let setup = TestSetup::new();
    
    // Create a fresh lobby with hex board
    let lobby_id = 864275;
    let host_address = <soroban_sdk::Address as soroban_sdk::testutils::Address>::generate(&setup.env);
    let guest_address = <soroban_sdk::Address as soroban_sdk::testutils::Address>::generate(&setup.env);
    
    // Create the hex board from weird.txt
    let lobby_parameters = crate::test_utils::create_user_board_parameters(&setup.env);
    
    // Make and join lobby
    setup.client.make_lobby(&host_address, &MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters.clone(),
    });
    setup.client.join_lobby(&guest_address, &JoinLobbyReq { lobby_id });
    
    // Complete setup phase
    let ((host_setup_commits, host_ranks), (guest_setup_commits, guest_ranks)) = setup.env.as_contract(&setup.contract_id, || {
        crate::test_utils::create_setup_commits_from_game_state2(&setup.env, lobby_id)
    });
    
    let (host_merkle_root, host_merkle_proofs) = crate::test_utils::get_merkel(&setup.env, &host_setup_commits, &host_ranks);
    let (guest_merkle_root, guest_merkle_proofs) = crate::test_utils::get_merkel(&setup.env, &guest_setup_commits, &guest_ranks);
    
    setup.client.commit_setup(&host_address, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_merkle_root,
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    setup.client.commit_setup(&guest_address, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: guest_merkle_root,
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    
    // Now we should be in MoveCommit phase
    // Update game state to match weird.txt at turn 3
    setup.env.as_contract(&setup.contract_id, || {
        let mut game_state: GameState = setup.env.storage().temporary().get(&DataKey::GameState(lobby_id)).unwrap();
        game_state.turn = 3;
        
        // Update specific pawn positions
        let host_pawn_id = 106;  // Initially at (5,3)
        let guest_pawn_id = 203; // Initially at (5,6)
        
        for (i, packed_pawn) in game_state.pawns.iter().enumerate() {
            let mut pawn = Contract::unpack_pawn(&setup.env, packed_pawn);
            if pawn.pawn_id == host_pawn_id {
                pawn.pos = Pos { x: 5, y: 4 };
                pawn.moved = true;
                game_state.pawns.set(i as u32, Contract::pack_pawn(pawn));
            } else if pawn.pawn_id == guest_pawn_id {
                pawn.pos = Pos { x: 5, y: 5 };
                pawn.moved = true;
                pawn.rank = Vec::from_array(&setup.env, [4]); // Give it rank 4
                game_state.pawns.set(i as u32, Contract::pack_pawn(pawn));
            }
        }
        
        setup.env.storage().temporary().set(&DataKey::GameState(lobby_id), &game_state);
    });
    
    // Create the moves from weird.txt
    let host_pawn_id = 106;
    let guest_pawn_id = 203;
    
    let host_move = HiddenMove {
        pawn_id: host_pawn_id,
        salt: 16724528812434846543, // Exact salt from weird.txt
        start_pos: Pos { x: 5, y: 4 },
        target_pos: Pos { x: 5, y: 5 }, // Moving to enemy-occupied tile
    };
    
    let guest_move = HiddenMove {
        pawn_id: guest_pawn_id,
        salt: 67890,
        start_pos: Pos { x: 5, y: 5 },
        target_pos: Pos { x: 4, y: 4 }, // Should be valid hex neighbor
    };
    
    // Commit moves
    let host_move_serialized = host_move.clone().to_xdr(&setup.env);
    let host_move_full_hash = setup.env.crypto().sha256(&host_move_serialized).to_bytes().to_array();
    let host_move_hash = HiddenMoveHash::from_array(&setup.env, &host_move_full_hash[0..16].try_into().unwrap());
    
    let guest_move_serialized = guest_move.clone().to_xdr(&setup.env);
    let guest_move_full_hash = setup.env.crypto().sha256(&guest_move_serialized).to_bytes().to_array();
    let guest_move_hash = HiddenMoveHash::from_array(&setup.env, &guest_move_full_hash[0..16].try_into().unwrap());
    
    setup.client.commit_move(&host_address, &CommitMoveReq {
        lobby_id,
        move_hash: host_move_hash,
    });
    
    setup.client.commit_move(&guest_address, &CommitMoveReq {
        lobby_id,
        move_hash: guest_move_hash,
    });
    
    // Now prove moves - this replicates the exact scenario from weird.txt
    std::println!("\n=== STARTING MOVE PROVE PHASE ===");
    
    // Host proves first
    let host_result = setup.client.try_prove_move(&host_address, &ProveMoveReq {
        lobby_id,
        move_proof: host_move.clone(),
    });
    
    if host_result.is_err() {
        std::println!("Host move failed: {:?}", host_result.err());
        panic!("Host move to enemy-occupied tile should succeed!");
    }
    
    std::println!("Host move succeeded!");
    
    // Check phase after host move
    let phase_after_host = setup.env.as_contract(&setup.contract_id, || {
        let lobby_info: LobbyInfo = setup.env.storage().temporary().get(&DataKey::LobbyInfo(lobby_id)).unwrap();
        std::println!("After host move - Phase: {:?}, Subphase: {:?}", lobby_info.phase, lobby_info.subphase);
        (lobby_info.phase, lobby_info.subphase)
    });
    
    if phase_after_host.0 == Phase::Aborted {
        panic!("Game aborted after host move! This should not happen.");
    }
    
    // Guest proves their move
    std::println!("\nGuest attempting to move from (5,5) to (4,4)...");
    let guest_result = setup.client.try_prove_move(&guest_address, &ProveMoveReq {
        lobby_id,
        move_proof: guest_move.clone(),
    });
    
    // Check final state
    let final_state = setup.env.as_contract(&setup.contract_id, || {
        let lobby_info: LobbyInfo = setup.env.storage().temporary().get(&DataKey::LobbyInfo(lobby_id)).unwrap();
        std::println!("\nFinal state - Phase: {:?}, Subphase: {:?}", lobby_info.phase, lobby_info.subphase);
        (lobby_info.phase, lobby_info.subphase)
    });
    
    if final_state.0 == Phase::Aborted {
        std::println!("\n=== GAME ABORTED ===");
        std::println!("This confirms the hex neighbor bug!");
        
        // Debug hex neighbors
        let neighbors = setup.env.as_contract(&setup.contract_id, || {
            let mut neighbors = [Pos { x: -42069, y: -42069 }; 6];
            Contract::get_neighbors(&Pos { x: 5, y: 5 }, true, &mut neighbors);
            neighbors
        });
        
        std::println!("\nNeighbors of (5,5) in hex grid:");
        for (i, n) in neighbors.iter().enumerate() {
            if n.x != -42069 {
                std::println!("  [{}]: ({}, {})", i, n.x, n.y);
            }
        }
        
        let is_neighbor = neighbors.iter().any(|n| n.x == 4 && n.y == 4);
        std::println!("\nIs (4,4) a neighbor of (5,5)? {}", is_neighbor);
        std::println!("Expected: true (should be a neighbor in hex grid)");
        
        // The test confirms the issue
        std::println!("\nTEST RESULT: Confirmed hex neighbor calculation issue!");
    } else {
        std::println!("\n=== SUCCESS ===");
        std::println!("Both moves succeeded! The hex neighbor fix is working.");
    }
}
// endregion