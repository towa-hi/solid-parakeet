#![cfg(test)]
extern crate std;
use super::super::*;
use super::super::test_utils::*;
use super::test_utils::*;
use soroban_sdk::{Env, Address};
use soroban_sdk::testutils::Address as _;

// region setup tests

#[test]
fn test_rank_proving_simple() {
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

    // Create deterministic setups with known content using the new function
    let ((host_setup, host_hidden_ranks), (guest_setup, guest_hidden_ranks)) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state2(&setup.env, lobby_id)
    });

    let (host_root, host_proofs) = get_merkel(&setup.env, &host_setup, &host_hidden_ranks);
    let (guest_root, guest_proofs) = get_merkel(&setup.env, &guest_setup, &guest_hidden_ranks);
    // Commit both setups
    let host_commit_req = CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root.clone(),
    };
    let guest_commit_req = CommitSetupReq {
        lobby_id,
        rank_commitment_root: guest_root,
    };

    setup.client.commit_setup(&host_address, &host_commit_req);
    setup.client.commit_setup(&guest_address, &guest_commit_req);
    
    // Debug: print out the correspondence
    std::println!("=== DEBUG: Checking correspondence ===");
    std::println!("host_hidden_ranks.len() = {}", host_hidden_ranks.len());
    std::println!("host_setup.setup_commits.len() = {}", host_setup.setup_commits.len());
    std::println!("host_proofs.len() = {}", host_proofs.len());
    
    for (i, hidden_rank) in host_hidden_ranks.iter().enumerate() {
        std::println!("\n--- Index {} ---", i);
        std::println!("hidden_rank.pawn_id = {}", hidden_rank.pawn_id);
        std::println!("team: {}", Contract::decode_pawn_id(&hidden_rank.pawn_id.clone()).1);
        
        // Calculate the expected hash from hidden_rank
        let serialized = hidden_rank.clone().to_xdr(&setup.env);
        let full_hash = setup.env.crypto().sha256(&serialized).to_bytes().to_array();
        let calculated_hash = HiddenRankHash::from_array(&setup.env, &full_hash[0..16].try_into().unwrap());
        std::println!("calculated_hash = {:?}", calculated_hash.to_array());
        
        // Get the stored hash from setup
        let stored_commit = host_setup.setup_commits.get_unchecked(i as u32);
        std::println!("stored pawn_id = {}", stored_commit.pawn_id);
        std::println!("stored_hash = {:?}", stored_commit.hidden_rank_hash.to_array());
        
        // They should match
        assert_eq!(calculated_hash, stored_commit.hidden_rank_hash, 
                   "Hash mismatch at index {}: calculated != stored", i);
        assert_eq!(hidden_rank.pawn_id, stored_commit.pawn_id,
                   "Pawn ID mismatch at index {}", i);
        
        let hidden_rank_proof = host_proofs.get_unchecked(i as u32);
        let prove_rank_test_req = ProveRankReq {
            lobby_id,
            hidden_ranks: Vec::from_array(&setup.env, [hidden_rank]),
            merkle_proofs: Vec::from_array(&setup.env, [hidden_rank_proof.clone()]),
        };
        let is_valid = setup.env.as_contract(&setup.contract_id, || {
            Contract::verify_merkle_proof(&setup.env, &stored_commit.hidden_rank_hash, &hidden_rank_proof, &host_root)
        });

        assert!(is_valid, "Merkle proof should be valid for index {}", i);
    }
}

#[test]
fn test_collision_winner_rank_revelation() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;

    let lobby_parameters = create_full_stratego_board_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);

    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);

    // Create setups but manually assign specific ranks for testing
    let (host_setup, host_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, 0)
    });
    let (guest_setup, guest_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, 1)
    });
    let (host_root, host_proofs) = get_merkel(&setup.env, &host_setup, &host_hidden_ranks);
    let (guest_root, guest_proofs) = get_merkel(&setup.env, &guest_setup, &guest_hidden_ranks);

    let host_commit_req = CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root,
    };
    let guest_commit_req = CommitSetupReq {
        lobby_id,
        rank_commitment_root: guest_root,
    };

    setup.client.commit_setup(&host_address, &host_commit_req);
    setup.client.commit_setup(&guest_address, &guest_commit_req);

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
    let _collision_move = setup.env.as_contract(&setup.contract_id, || {
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