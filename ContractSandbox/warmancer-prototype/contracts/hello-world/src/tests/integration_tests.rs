#![cfg(test)]
extern crate std;
use super::super::*;
use super::super::test_utils::*;
use super::test_utils::*;
use soroban_sdk::{Env, Address};
use soroban_sdk::testutils::Address as _;

// region integration tests

#[test]
fn test_full_stratego_game() {
    let setup = TestSetup::new();

    // Use helper function to set up lobby and advance to MoveCommit phase, getting ranks
    let (lobby_id, host_address, guest_address, host_ranks, guest_ranks, host_merkle_proofs, guest_merkle_proofs) = setup_lobby_for_commit_move(&setup, 400);

    // Verify we're in MoveCommit phase (helper function should guarantee this)
    let initial_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(initial_snapshot.phase, Phase::MoveCommit);
    assert_eq!(initial_snapshot.subphase, Subphase::Both);

    // Perform up to 50 moves or until no valid moves are possible
    for move_number in 1..=100 {
        std::println!("=== MOVE {} ===", move_number);

        // Take fresh snapshot at start of each loop iteration
        let loop_start_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
        let current_phase = loop_start_snapshot.lobby_info.phase.clone();

        match current_phase {
            Phase::MoveCommit => {
                std::println!("Phase: MoveCommit - Committing moves");
                {
                    std::println!("=== BOARD STATE AT TURN START ===");
                    let board_display_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
                    let board_state_with_revealed_ranks = format_board_with_colors_and_ranks(&setup.env, &board_display_snapshot, Some(&host_ranks), Some(&guest_ranks));
                    std::println!("{}", board_state_with_revealed_ranks);
                }
                // Generate moves using current game state from snapshot
                let host_move_opt = generate_valid_move_req(&setup.env, &loop_start_snapshot.game_state, &loop_start_snapshot.lobby_parameters, 0, &host_ranks, move_number as u64 * 1000 + 12345);
                let guest_move_opt = generate_valid_move_req(&setup.env, &loop_start_snapshot.game_state, &loop_start_snapshot.lobby_parameters, 1, &guest_ranks, move_number as u64 * 1000 + 54321);

                if host_move_opt.is_none() || guest_move_opt.is_none() {
                    std::println!("No valid moves available for one or both players. Game ends at move {}", move_number);
                    break;
                }

                let host_move_proof = host_move_opt.unwrap();
                let guest_move_proof = guest_move_opt.unwrap();

                let host_move_serialized = host_move_proof.clone().to_xdr(&setup.env);
                let host_move_full_hash = setup.env.crypto().sha256(&host_move_serialized).to_bytes().to_array();
                let host_move_hash = HiddenMoveHash::from_array(&setup.env, &host_move_full_hash[0..16].try_into().unwrap());
                let guest_move_serialized = guest_move_proof.clone().to_xdr(&setup.env);
                let guest_move_full_hash = setup.env.crypto().sha256(&guest_move_serialized).to_bytes().to_array();
                let guest_move_hash = HiddenMoveHash::from_array(&setup.env, &guest_move_full_hash[0..16].try_into().unwrap());

                let host_move_req = CommitMoveReq {
                    lobby_id,
                    move_hash: host_move_hash,
                };
                let guest_move_req = CommitMoveReq {
                    lobby_id,
                    move_hash: guest_move_hash,
                };

                // Commit moves (state-changing operation)
                setup.client.commit_move(&host_address, &host_move_req);

                let guest_prove_move_req = ProveMoveReq {
                    move_proof: guest_move_proof,
                    lobby_id,
                };
                // later player can use commit_move_and_prove_move for convenience
                setup.client.commit_move_and_prove_move(&guest_address, &guest_move_req, &guest_prove_move_req);

                // Take snapshot after committing moves to check new phase
                let post_commit_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
                let current_phase_after_commit = post_commit_snapshot.phase.clone();

                std::println!("After committing moves for turn {}: current_phase = {:?}", move_number, current_phase_after_commit);

                // Only proceed with move proving if we're in MoveProve phase
                if current_phase_after_commit == Phase::MoveProve {
                    std::println!("Proceeding with MoveProve phase for turn {}", move_number);

                    // Prove moves
                    let host_prove_move_req = ProveMoveReq {
                        move_proof: host_move_proof,
                        lobby_id,
                    };

                    // Prove moves (state-changing operation)
                    setup.client.prove_move(&host_address, &host_prove_move_req);

                    // VALIDATE: Check what happened after MoveProve - what rank proofs are needed?
                    {
                        let move_validation_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
                        validate_move_prove_transition(&move_validation_snapshot, &host_prove_move_req, &guest_prove_move_req);
                    }

                } else {
                    std::println!("Skipped MoveProve phase - game already advanced to next phase: {:?}", current_phase_after_commit);
                }

                std::println!("Moves committed and proved successfully");
            },

            Phase::RankProve => {
                std::println!("Phase: RankProve - Proving ranks for collisions");

                // Get the needed rank proofs for both players from snapshot
                let host_move = loop_start_snapshot.game_state.moves.get(0).unwrap();
                std::println!("Host move: {:?}", host_move);
                let guest_move = loop_start_snapshot.game_state.moves.get(1).unwrap();
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

                // Submit rank proofs if there are any needed
                let (host_rank_req, guest_rank_req) = if !host_needed_ranks.is_empty() || !guest_needed_ranks.is_empty() {
                    std::println!("Rank proofs needed - creating and submitting rank proof requests");

                    // Debug: Log pawn 814 if it's in the needed ranks
                    for needed_id in host_needed_ranks.iter() {
                        if needed_id as u32 == 814 {
                            std::println!("DEBUG: Pawn 814 is in host_needed_ranks");
                            // Find pawn 814 in host_ranks
                            for (i, hidden_rank) in host_ranks.iter().enumerate() {
                                if hidden_rank.pawn_id == 814 {
                                    std::println!("DEBUG: Found pawn 814 at index {} in host_ranks", i);
                                    std::println!("DEBUG: Pawn 814 rank: {}", hidden_rank.rank);
                                    break;
                                }
                            }
                        }
                    }

                    // Create rank proof requests
                    let (host_req, guest_req) = create_rank_proof_requests(&setup.env, lobby_id,
                                     &host_needed_ranks, &guest_needed_ranks, &host_ranks, &guest_ranks, &host_merkle_proofs, &guest_merkle_proofs);

                    // Submit rank proofs to the contract (state-changing operation)
                    if let Some(ref host_req) = host_req {
                        std::println!("Host proving {} ranks", host_req.hidden_ranks.len());
                        setup.client.prove_rank(&host_address, host_req);
                    }
                    if let Some(ref guest_req) = guest_req {
                        std::println!("Guest proving {} ranks", guest_req.hidden_ranks.len());
                        setup.client.prove_rank(&guest_address, guest_req);
                    }

                    (host_req, guest_req)
                } else {
                    std::println!("No rank proofs needed - skipping rank proof submission");
                    (None, None)
                };

                // VALIDATE: Check game state after rank proof submission (if any occurred)
                if host_rank_req.is_some() || guest_rank_req.is_some() {
                    let rank_validation_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
                    validate_rank_prove_transition(&rank_validation_snapshot, host_rank_req.as_ref(), guest_rank_req.as_ref());
                }
            },

            Phase::Finished => {
                std::println!("Game finished at move {}", move_number);
                break;
            },

            Phase::Aborted => {
                std::println!("Game aborted at move {} due to invalid move/setup", move_number);
                break;
            },

            _ => {
                panic!("Unexpected phase: {:?}", current_phase);
            }
        }

    }

    // Take final snapshot after all loop iterations complete
    let final_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);

    // Verify final game state
    assert!(matches!(final_snapshot.phase, Phase::MoveCommit | Phase::MoveProve | Phase::RankProve | Phase::Finished | Phase::Aborted));
    std::println!("Final game state: Phase={:?}, Subphase={:?}", final_snapshot.phase, final_snapshot.subphase);

}

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
    let (host_setup, host_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_a, 0)
    });

    let (guest_setup, guest_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_b, 1)
    });
    let (host_root, host_proofs) = get_merkel(&setup.env, &host_setup, &host_hidden_ranks);
    let (guest_root, guest_proofs) = get_merkel(&setup.env, &guest_setup, &guest_hidden_ranks);

    // Apply identical setups to both games
    for lobby_id in [lobby_a, lobby_b] {
        let (host_addr, guest_addr) = if lobby_id == lobby_a { (&host_a, &guest_a) } else { (&host_b, &guest_b) };

        setup.client.commit_setup(host_addr, &CommitSetupReq { lobby_id, rank_commitment_root: host_root.clone(), setup: host_setup.clone() });
        setup.client.commit_setup(guest_addr, &CommitSetupReq { lobby_id, rank_commitment_root: guest_root.clone(), setup: guest_setup.clone() });
    }

    // Populate ranks only in Game B
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_b);
        let mut game_state: GameState = setup.env.storage().temporary().get(&game_state_key).expect("Game state should exist");
        for hidden_rank in host_hidden_ranks.iter().chain(guest_hidden_ranks.iter()) {
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

        // Take snapshot at start of loop to get current game state
        let loop_start_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_a);

        let host_move = generate_valid_move_req(&setup.env, &loop_start_snapshot.game_state, &loop_start_snapshot.lobby_parameters, 0, &host_hidden_ranks, salt_host);
        let guest_move = generate_valid_move_req(&setup.env, &loop_start_snapshot.game_state, &loop_start_snapshot.lobby_parameters, 1, &guest_hidden_ranks, salt_guest);

        if host_move.is_none() || guest_move.is_none() {
            break; // No more valid moves
        }

        let host_move_proof = host_move.unwrap();
        let guest_move_proof = guest_move.unwrap();

        // Execute moves on both games
        for lobby_id in [lobby_a, lobby_b] {
            let (host_addr, guest_addr) = if lobby_id == lobby_a { (&host_a, &guest_a) } else { (&host_b, &guest_b) };

            // Check if we can commit moves using current phase from loop start
            let lobby_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
            let can_commit = lobby_snapshot.phase == Phase::MoveCommit;

            if can_commit {
                let host_move_full_hash = setup.env.crypto().sha256(&host_move_proof.clone().to_xdr(&setup.env)).to_bytes().to_array();
                let host_hash = HiddenMoveHash::from_array(&setup.env, &host_move_full_hash[0..16].try_into().unwrap());
                let guest_move_full_hash = setup.env.crypto().sha256(&guest_move_proof.clone().to_xdr(&setup.env)).to_bytes().to_array();
                let guest_hash = HiddenMoveHash::from_array(&setup.env, &guest_move_full_hash[0..16].try_into().unwrap());

                // Commit moves (state-changing operation)
                setup.client.commit_move(host_addr, &CommitMoveReq { lobby_id, move_hash: host_hash });
                setup.client.commit_move(guest_addr, &CommitMoveReq { lobby_id, move_hash: guest_hash });

                // Check if we should prove moves (take snapshot after commit)
                let post_commit_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
                let should_prove = post_commit_snapshot.phase == Phase::MoveProve;

                if should_prove {
                    // Prove moves (state-changing operation)
                    setup.client.prove_move(host_addr, &ProveMoveReq { lobby_id, move_proof: host_move_proof.clone() });
                    setup.client.prove_move(guest_addr, &ProveMoveReq { lobby_id, move_proof: guest_move_proof.clone() });
                }
            }
        }

        // Handle rank proving if needed (only Game A should need this)
        // Check current phase using snapshot
        let rank_phase_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_a);
        let game_a_needs_ranks = rank_phase_snapshot.phase == Phase::RankProve;

        if game_a_needs_ranks {
            // Get needed ranks from current game state
            let rank_data_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_a);
            let host_move = rank_data_snapshot.game_state.moves.get(0).unwrap();
            let guest_move = rank_data_snapshot.game_state.moves.get(1).unwrap();
            let host_needed = host_move.needed_rank_proofs.clone();
            let guest_needed = guest_move.needed_rank_proofs.clone();

            // Provide needed rank proofs
            if !host_needed.is_empty() {
                let mut host_proof_ranks = Vec::new(&setup.env);
                for needed_id in host_needed.iter() {
                    for rank in host_hidden_ranks.iter() {
                        if rank.pawn_id == needed_id {
                            host_proof_ranks.push_back(rank);
                        }
                    }
                }
                if !host_proof_ranks.is_empty() {
                    // Prove host ranks (state-changing operation)
                    let mut host_needed_merkle_proofs = Vec::new(&setup.env);
                    for needed_id in host_needed.iter() {
                        for (i, rank) in host_hidden_ranks.iter().enumerate() {
                            if rank.pawn_id == needed_id {
                                host_needed_merkle_proofs.push_back(host_proofs.get(i as u32).unwrap());
                            }
                        }
                    }
                    setup.client.prove_rank(&host_a, &ProveRankReq { lobby_id: lobby_a, hidden_ranks: host_proof_ranks, merkle_proofs: host_needed_merkle_proofs });
                }
            }

            if !guest_needed.is_empty() {
                let mut guest_proof_ranks = Vec::new(&setup.env);
                for needed_id in guest_needed.iter() {
                    for rank in guest_hidden_ranks.iter() {
                        if rank.pawn_id == needed_id {
                            guest_proof_ranks.push_back(rank);
                        }
                    }
                }
                if !guest_proof_ranks.is_empty() {
                    // Prove guest ranks (state-changing operation)
                    let mut guest_needed_merkle_proofs = Vec::new(&setup.env);
                    for needed_id in guest_needed.iter() {
                        for (i, rank) in guest_hidden_ranks.iter().enumerate() {
                            if rank.pawn_id == needed_id {
                                guest_needed_merkle_proofs.push_back(guest_proofs.get(i as u32).unwrap());
                            }
                        }
                    }
                    setup.client.prove_rank(&guest_a, &ProveRankReq { lobby_id: lobby_a, hidden_ranks: guest_proof_ranks, merkle_proofs: guest_needed_merkle_proofs });
                }
            }
        }

        // Verify both games have identical states
        let game_state_a = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_a).game_state;
        let game_state_b = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_b).game_state;
        let states_match = verify_pawn_states_identical(&game_state_a, &game_state_b);
        assert!(states_match, "Game states diverged at move {}", move_number);

        // Verify both games are in the same phase (end of loop validation)
        {
            let phase_snapshot_a = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_a);
            let phase_snapshot_b = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_b);
            let (phase_a, phase_b) = (phase_snapshot_a.phase, phase_snapshot_b.phase);

            assert_eq!(phase_a, phase_b, "Game phases diverged at move {}: A={:?}, B={:?}", move_number, phase_a, phase_b);
        }
    }

    // Take final snapshots after comparison loop completes
    let final_snapshot_a = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_a);
    let final_snapshot_b = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_b);

    // Final verification that both games ended in the same state
    assert_eq!(final_snapshot_a.phase, final_snapshot_b.phase, "Final phases should match");
    std::println!("Comparison test completed successfully. Both games in phase: {:?}", final_snapshot_a.phase);
}

// endregion