#![cfg(test)]
#![allow(unused_variables)]
extern crate std;

use super::super::*;
use super::super::test_utils::*;
use super::test_utils::*;
use soroban_sdk::testutils::Ledger as _;
use soroban_sdk::testutils::storage::Temporary as _;

fn extend_lobby_ttl(setup: &TestSetup, lobby_id: u32) {
    setup.env.as_contract(&setup.contract_id, || {
        setup.env.storage().temporary().extend_ttl(&DataKey::LobbyInfo(lobby_id), 100, 17280);
        setup.env.storage().temporary().extend_ttl(&DataKey::GameState(lobby_id), 100, 17280);
        setup.env.storage().temporary().extend_ttl(&DataKey::LobbyParameters(lobby_id), 100, 17280);
    });
}

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
                    std::println!("{}", format_board_with_colors_and_ranks(&setup.env, &loop_start_snapshot, Some(&host_ranks), Some(&guest_ranks)));
                }
                // Generate moves using current game state from snapshot
                let host_move_opt = generate_valid_move_req(&setup.env, &loop_start_snapshot.pawns_map, &loop_start_snapshot.lobby_parameters, &UserIndex::Host, &host_ranks, move_number as u64 * 1000 + 12345);
                let guest_move_opt = generate_valid_move_req(&setup.env, &loop_start_snapshot.pawns_map, &loop_start_snapshot.lobby_parameters, &UserIndex::Guest, &guest_ranks, move_number as u64 * 1000 + 54321);
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
                    move_hash: host_move_hash,
                    lobby_id,
                };
                let guest_move_req = CommitMoveReq {
                    move_hash: guest_move_hash,
                    lobby_id,
                };

                // Debug: Print move details
                std::println!("Host move: {:?}", host_move_proof);
                std::println!("Guest move: {:?}", guest_move_proof);

                // Commit both moves (state-changing operation)
                setup.client.commit_move(&host_address, &host_move_req);
                setup.client.commit_move(&guest_address, &guest_move_req);

                // Take snapshot after committing moves to check new phase
                let post_commit_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
                let current_phase_after_commit = post_commit_snapshot.phase.clone();

                match current_phase_after_commit {
                    Phase::MoveProve => {
                        std::println!("Phase transitioned to MoveProve after commits - proving moves");

                        let host_prove_move_req = ProveMoveReq {
                            move_proof: host_move_proof,
                            lobby_id,
                        };
                        let guest_prove_move_req = ProveMoveReq {
                            move_proof: guest_move_proof,
                            lobby_id,
                        };

                        // Prove both moves (state-changing operation)
                        setup.client.prove_move(&host_address, &host_prove_move_req);
                        setup.client.prove_move(&guest_address, &guest_prove_move_req);
                    }
                    _ => {
                        std::println!("Unexpected phase after commit: {:?}", current_phase_after_commit);
                    }
                }
            }

            Phase::RankProve => {
                std::println!("Phase: RankProve - Proving ranks");

                // Get needed rank proofs from snapshot at this phase
                let rank_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
                let host_move_state = rank_snapshot.game_state.moves.get(UserIndex::Host.u32()).unwrap();
                let guest_move_state = rank_snapshot.game_state.moves.get(UserIndex::Guest.u32()).unwrap();

                let host_needed_ids = host_move_state.needed_rank_proofs.clone();
                let guest_needed_ids = guest_move_state.needed_rank_proofs.clone();

                std::println!("Host needs to prove {} ranks", host_needed_ids.len());
                std::println!("Guest needs to prove {} ranks", guest_needed_ids.len());

                // Provide host rank proofs if needed
                if !host_needed_ids.is_empty() {
                    let mut host_proof_ranks = Vec::new(&setup.env);
                    let mut host_needed_merkle_proofs = Vec::new(&setup.env);

                    for needed_id in host_needed_ids.iter() {
                        for (i, rank) in host_ranks.iter().enumerate() {
                            if rank.pawn_id == needed_id {
                                host_proof_ranks.push_back(rank.clone());
                                host_needed_merkle_proofs.push_back(host_merkle_proofs.get(i as u32).unwrap());
                                std::println!("Found host pawn {} rank {}", rank.pawn_id, rank.rank);
                                break;
                            }
                        }
                    }

                    // Prove host ranks (state-changing operation)
                    setup.client.prove_rank(&host_address, &ProveRankReq {
                        lobby_id,
                        hidden_ranks: host_proof_ranks,
                        merkle_proofs: host_needed_merkle_proofs,
                    });
                }

                // Provide guest rank proofs if needed
                if !guest_needed_ids.is_empty() {
                    let mut guest_proof_ranks = Vec::new(&setup.env);
                    let mut guest_needed_merkle_proofs = Vec::new(&setup.env);

                    for needed_id in guest_needed_ids.iter() {
                        for (i, rank) in guest_ranks.iter().enumerate() {
                            if rank.pawn_id == needed_id {
                                guest_proof_ranks.push_back(rank.clone());
                                guest_needed_merkle_proofs.push_back(guest_merkle_proofs.get(i as u32).unwrap());
                                std::println!("Found guest pawn {} rank {}", rank.pawn_id, rank.rank);
                                break;
                            }
                        }
                    }

                    // Prove guest ranks (state-changing operation)
                    setup.client.prove_rank(&guest_address, &ProveRankReq {
                        lobby_id,
                        hidden_ranks: guest_proof_ranks,
                        merkle_proofs: guest_needed_merkle_proofs,
                    });
                }
            }

            Phase::Finished => {
                std::println!("Game finished! Winner: {:?}", loop_start_snapshot.lobby_info.subphase);
                break;
            }

            Phase::Aborted => {
                std::println!("Game aborted!");
                break;
            }

            _ => {
                std::println!("Unexpected phase: {:?}", current_phase);
                break;
            }
        }

        // Check if game ended (using fresh snapshot)
        let end_check_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        if end_check_snapshot.phase == Phase::Finished || end_check_snapshot.phase == Phase::Aborted {
            std::println!("Game ended at move {} with phase {:?}", move_number, end_check_snapshot.phase);
            break;
        }
    }

    // Take final snapshot
    let final_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    std::println!("Final game phase: {:?}", final_snapshot.phase);
}

#[test]
fn test_split_security_and_non_security_modes() {
    let setup = TestSetup::new();
    let lobby_secure = 499;
    let lobby_insecure = 500;

    let host_secure = setup.generate_address();
    let guest_secure = setup.generate_address();
    let host_insecure = setup.generate_address();
    let guest_insecure = setup.generate_address();

    // Create secure mode lobby (default parameters)
    let secure_params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host_secure, &MakeLobbyReq {
        lobby_id: lobby_secure,
        parameters: secure_params,
    });

    // Create insecure mode lobby
    let mut insecure_params = create_test_lobby_parameters(&setup.env);
    insecure_params.security_mode = false;
    setup.client.make_lobby(&host_insecure, &MakeLobbyReq {
        lobby_id: lobby_insecure,
        parameters: insecure_params,
    });

    // Join lobbies
    setup.client.join_lobby(&guest_secure, &JoinLobbyReq { lobby_id: lobby_secure });
    setup.client.join_lobby(&guest_insecure, &JoinLobbyReq { lobby_id: lobby_insecure });

    // Commit setups for both games
    let (host_setup, host_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_secure, &UserIndex::Host)
    });
    let (guest_setup, guest_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_secure, &UserIndex::Guest)
    });
    let (host_root, host_proofs) = get_merkel(&setup.env, &host_setup, &host_hidden_ranks);
    let (guest_root, guest_proofs) = get_merkel(&setup.env, &guest_setup, &guest_hidden_ranks);

    // Commit setups for secure game
    setup.client.commit_setup(&host_secure, &CommitSetupReq {
        lobby_id: lobby_secure,
        rank_commitment_root: host_root.clone(),
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    setup.client.commit_setup(&guest_secure, &CommitSetupReq {
        lobby_id: lobby_secure,
        rank_commitment_root: guest_root.clone(),
        zz_hidden_ranks: Vec::new(&setup.env),
    });

    // Commit setups for insecure game (must provide hidden_ranks)
    setup.client.commit_setup(&host_insecure, &CommitSetupReq {
        lobby_id: lobby_insecure,
        rank_commitment_root: host_root,
        zz_hidden_ranks: host_hidden_ranks.clone(),
    });
    setup.client.commit_setup(&guest_insecure, &CommitSetupReq {
        lobby_id: lobby_insecure,
        rank_commitment_root: guest_root,
        zz_hidden_ranks: guest_hidden_ranks.clone(),
    });

    // Test different move scenarios
    for move_number in 1..=5 {
        std::println!("=== MOVE {} ===", move_number);

        // Generate valid moves for both games
        let secure_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);
        let insecure_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_insecure);

        let host_move = generate_valid_move_req(&setup.env, &secure_snapshot.pawns_map, &secure_snapshot.lobby_parameters, &UserIndex::Host, &host_hidden_ranks, move_number as u64 * 1000);
        let guest_move = generate_valid_move_req(&setup.env, &secure_snapshot.pawns_map, &secure_snapshot.lobby_parameters, &UserIndex::Guest, &guest_hidden_ranks, move_number as u64 * 1000 + 1);

        if host_move.is_none() || guest_move.is_none() {
            std::println!("No valid moves available");
            break;
        }

        let host_move_proof = host_move.unwrap();
        let guest_move_proof = guest_move.unwrap();

        // Hash moves
        let host_serialized = host_move_proof.clone().to_xdr(&setup.env);
        let host_hash_full = setup.env.crypto().sha256(&host_serialized).to_bytes().to_array();
        let host_hash = HiddenMoveHash::from_array(&setup.env, &host_hash_full[0..16].try_into().unwrap());

        let guest_serialized = guest_move_proof.clone().to_xdr(&setup.env);
        let guest_hash_full = setup.env.crypto().sha256(&guest_serialized).to_bytes().to_array();
        let guest_hash = HiddenMoveHash::from_array(&setup.env, &guest_hash_full[0..16].try_into().unwrap());

        // Secure mode: commit then prove separately
        setup.client.commit_move(&host_secure, &CommitMoveReq {
            lobby_id: lobby_secure,
            move_hash: host_hash.clone(),
        });
        setup.client.commit_move(&guest_secure, &CommitMoveReq {
            lobby_id: lobby_secure,
            move_hash: guest_hash.clone(),
        });

        setup.client.prove_move(&host_secure, &ProveMoveReq {
            lobby_id: lobby_secure,
            move_proof: host_move_proof.clone(),
        });
        setup.client.prove_move(&guest_secure, &ProveMoveReq {
            lobby_id: lobby_secure,
            move_proof: guest_move_proof.clone(),
        });

        // Insecure mode: commit and prove together
        setup.client.commit_move_and_prove_move(&host_insecure, 
            &CommitMoveReq {
                lobby_id: lobby_insecure,
                move_hash: host_hash,
            },
            &ProveMoveReq {
                lobby_id: lobby_insecure,
                move_proof: host_move_proof,
            }
        );
        setup.client.commit_move_and_prove_move(&guest_insecure,
            &CommitMoveReq {
                lobby_id: lobby_insecure,
                move_hash: guest_hash,
            },
            &ProveMoveReq {
                lobby_id: lobby_insecure,
                move_proof: guest_move_proof,
            }
        );

        // Handle rank proofs if needed
        let secure_after = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_secure);
        let insecure_after = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_insecure);

        if secure_after.phase == Phase::RankProve {
            // Handle rank proofs for secure game
            let rank_data = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);
            let host_needed = rank_data.game_state.moves.get(0).unwrap().needed_rank_proofs.clone();
            let guest_needed = rank_data.game_state.moves.get(1).unwrap().needed_rank_proofs.clone();

            if !host_needed.is_empty() {
                let mut host_proof_ranks = Vec::new(&setup.env);
                let mut host_needed_merkle = Vec::new(&setup.env);
                for needed_id in host_needed.iter() {
                    for (i, rank) in host_hidden_ranks.iter().enumerate() {
                        if rank.pawn_id == needed_id {
                            host_proof_ranks.push_back(rank.clone());
                            host_needed_merkle.push_back(host_proofs.get(i as u32).unwrap());
                        }
                    }
                }
                setup.client.prove_rank(&host_secure, &ProveRankReq {
                    lobby_id: lobby_secure,
                    hidden_ranks: host_proof_ranks,
                    merkle_proofs: host_needed_merkle,
                });
            }

            if !guest_needed.is_empty() {
                let mut guest_proof_ranks = Vec::new(&setup.env);
                let mut guest_needed_merkle = Vec::new(&setup.env);
                for needed_id in guest_needed.iter() {
                    for (i, rank) in guest_hidden_ranks.iter().enumerate() {
                        if rank.pawn_id == needed_id {
                            guest_proof_ranks.push_back(rank.clone());
                            guest_needed_merkle.push_back(guest_proofs.get(i as u32).unwrap());
                        }
                    }
                }
                setup.client.prove_rank(&guest_secure, &ProveRankReq {
                    lobby_id: lobby_secure,
                    hidden_ranks: guest_proof_ranks,
                    merkle_proofs: guest_needed_merkle,
                });
            }
        }

        // Check if games ended
        let secure_final = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_secure);
        let insecure_final = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_insecure);

        if secure_final.phase == Phase::Finished || secure_final.phase == Phase::Aborted {
            std::println!("Secure game ended");
        }
        if insecure_final.phase == Phase::Finished || insecure_final.phase == Phase::Aborted {
            std::println!("Insecure game ended");
        }

        // Both games should be in same phase
        assert_eq!(secure_final.phase, insecure_final.phase, "Games diverged at move {}", move_number);
    }
}

#[test]
fn test_compare_move_submission_methods() {
    let setup = TestSetup::new();
    
    let lobby_secure = 301;
    let lobby_insecure = 302;
    
    let host_secure = setup.generate_address();
    let guest_secure = setup.generate_address();  
    let host_insecure = setup.generate_address();
    let guest_insecure = setup.generate_address();
    
    // Create secure mode lobby
    let secure_params = create_test_lobby_parameters(&setup.env);
    let make_req_secure = MakeLobbyReq {
        lobby_id: lobby_secure,
        parameters: secure_params,
    };
    setup.client.make_lobby(&host_secure, &make_req_secure);
    
    // Create insecure mode lobby
    let mut insecure_params = create_test_lobby_parameters(&setup.env);
    insecure_params.security_mode = false;
    let make_req_insecure = MakeLobbyReq {
        lobby_id: lobby_insecure,
        parameters: insecure_params,
    };
    setup.client.make_lobby(&host_insecure, &make_req_insecure);
    
    // Join lobbies
    let join_req_secure = JoinLobbyReq {
        lobby_id: lobby_secure,
    };
    setup.client.join_lobby(&guest_secure, &join_req_secure);
    
    let join_req_insecure = JoinLobbyReq {
        lobby_id: lobby_insecure,
    };
    setup.client.join_lobby(&guest_insecure, &join_req_insecure);
    // Generate identical setups using fixed seed
    let (host_setup, host_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_secure, &UserIndex::Host)
    });

    let (guest_setup, guest_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_secure, &UserIndex::Guest)
    });
    for host_hidden_rank in host_hidden_ranks.clone() {
        std::println!("host pawnid: {} rank: {}", host_hidden_rank.pawn_id, host_hidden_rank.rank);
    }
    for guest_hidden_rank in guest_hidden_ranks.clone() {
        std::println!("guest pawnid: {} rank: {}", guest_hidden_rank.pawn_id, guest_hidden_rank.rank);
    }
    let (host_root, host_proofs) = get_merkel(&setup.env, &host_setup, &host_hidden_ranks);
    let (guest_root, guest_proofs) = get_merkel(&setup.env, &guest_setup, &guest_hidden_ranks);
    
    // Commit setups for secure game
    std::println!("host committing setup");
    setup.client.commit_setup(&host_secure, &CommitSetupReq {
        lobby_id: lobby_secure,
        rank_commitment_root: host_root.clone(),
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    std::println!("guest committing setup");
    setup.client.commit_setup(&guest_secure, &CommitSetupReq {
        lobby_id: lobby_secure,
        rank_commitment_root: guest_root.clone(),
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    
    // Commit setups for insecure game (must provide hidden_ranks)
    setup.client.commit_setup(&host_insecure, &CommitSetupReq {
        lobby_id: lobby_insecure,
        rank_commitment_root: host_root,
        zz_hidden_ranks: host_hidden_ranks.clone(), // In insecure mode, provide actual ranks
    });
    setup.client.commit_setup(&guest_insecure, &CommitSetupReq {
        lobby_id: lobby_insecure,
        rank_commitment_root: guest_root,
        zz_hidden_ranks: guest_hidden_ranks.clone(), // In insecure mode, provide actual ranks
    });
    
    // Test different move submission methods
    for move_number in 1..=10 {
        std::println!("=== MOVE {} ===", move_number);
        
        // Generate valid moves (same moves for both games)
        let snapshot_secure = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);
        
        let host_move_opt = generate_valid_move_req(&setup.env, &snapshot_secure.pawns_map, &snapshot_secure.lobby_parameters, &UserIndex::Host, &host_hidden_ranks, move_number as u64 * 1000);
        let guest_move_opt = generate_valid_move_req(&setup.env, &snapshot_secure.pawns_map, &snapshot_secure.lobby_parameters, &UserIndex::Guest, &guest_hidden_ranks, move_number as u64 * 1000 + 1);
        
        if host_move_opt.is_none() || guest_move_opt.is_none() {
            std::println!("No valid moves available");
            break;
        }
        
        let host_move_proof = host_move_opt.unwrap();
        let guest_move_proof = guest_move_opt.unwrap();
        
        // Create move hashes
        let host_serialized = host_move_proof.clone().to_xdr(&setup.env);
        let host_hash_full = setup.env.crypto().sha256(&host_serialized).to_bytes().to_array();
        let host_hash = HiddenMoveHash::from_array(&setup.env, &host_hash_full[0..16].try_into().unwrap());
        
        let guest_serialized = guest_move_proof.clone().to_xdr(&setup.env);
        let guest_hash_full = setup.env.crypto().sha256(&guest_serialized).to_bytes().to_array();
        let guest_hash = HiddenMoveHash::from_array(&setup.env, &guest_hash_full[0..16].try_into().unwrap());
        
        // For secure game, choose submission method based on move number
        match move_number % 3 {
            0 => {
                // Method 1: Both use commit then prove
                std::println!("Secure: Both using commit then prove");
                let host_secure_commit_move_req = CommitMoveReq { lobby_id: lobby_secure, move_hash: host_hash.clone() };
                let guest_secure_commit_move_req = CommitMoveReq { lobby_id: lobby_secure, move_hash: guest_hash.clone() };
                let host_secure_prove_move_req = ProveMoveReq { lobby_id: lobby_secure, move_proof: host_move_proof.clone() };
                let guest_secure_prove_move_req = ProveMoveReq { lobby_id: lobby_secure, move_proof: guest_move_proof.clone() };
                
                setup.client.commit_move(&host_secure, &host_secure_commit_move_req);
                setup.client.commit_move(&guest_secure, &guest_secure_commit_move_req);
                setup.client.prove_move(&host_secure, &host_secure_prove_move_req);
                setup.client.prove_move(&guest_secure, &guest_secure_prove_move_req);
            }
            1 => {
                // Method 2: Host uses commit_and_prove, guest uses separate calls
                std::println!("Secure: Host commit_and_prove, guest separate");
                let host_secure_commit_move_req = CommitMoveReq { lobby_id: lobby_secure, move_hash: host_hash.clone() };
                let guest_secure_commit_move_req = CommitMoveReq { lobby_id: lobby_secure, move_hash: guest_hash.clone() };
                let host_secure_prove_move_req = ProveMoveReq { lobby_id: lobby_secure, move_proof: host_move_proof.clone() };
                let guest_secure_prove_move_req = ProveMoveReq { lobby_id: lobby_secure, move_proof: guest_move_proof.clone() };

                // host commit move first
                std::println!("host commit_move");
                setup.client.commit_move(&host_secure, &host_secure_commit_move_req);
                // guest commit move and prove
                std::println!("guest commit_move_and_prove_move");
                setup.client.commit_move_and_prove_move(&guest_secure, &guest_secure_commit_move_req, &guest_secure_prove_move_req);

                // host check simulation
                std::println!("host simulate_collisions");
                let host_simulation_result = setup.client.simulate_collisions(&host_secure, &host_secure_prove_move_req);
                if !host_simulation_result.needed_rank_proofs.is_empty() {
                    std::println!("Host simulation shows rank proofs needed: {:?}", host_simulation_result.needed_rank_proofs);
                }

                // host prove
                std::println!("host prove_move");
                setup.client.prove_move(&host_secure, &host_secure_prove_move_req);
            }
            2 => {
                std::println!("Secure: Guest commit_and_prove, host separate");
                // Method 3: Guest uses commit_and_prove, host uses separate calls
                let host_secure_commit_move_req = CommitMoveReq { lobby_id: lobby_secure, move_hash: host_hash.clone() };
                let guest_secure_commit_move_req = CommitMoveReq { lobby_id: lobby_secure, move_hash: guest_hash.clone() };
                let host_secure_prove_move_req = ProveMoveReq { lobby_id: lobby_secure, move_proof: host_move_proof.clone() };
                let guest_secure_prove_move_req = ProveMoveReq { lobby_id: lobby_secure, move_proof: guest_move_proof.clone() };
                // latter player can use commit_move_and_prove_move for convenience
                setup.client.commit_move_and_prove_move(&guest_secure, &guest_secure_commit_move_req, &guest_secure_prove_move_req);

                // Take snapshot after committing moves to check new phase
                let post_commit_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_secure);
                let current_phase_after_commit = post_commit_snapshot.phase.clone();

                if current_phase_after_commit == Phase::RankProve {
                    // need rank proofs immediately
                    let rank_data_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);
                    let guest_move = rank_data_snapshot.game_state.moves.get(1).unwrap();
                    let guest_needed = guest_move.needed_rank_proofs.clone();
                    if !guest_needed.is_empty() {
                        let mut guest_proof_ranks = Vec::new(&setup.env);
                        let mut guest_needed_merkle_proofs = Vec::new(&setup.env);
                        for needed_id in guest_needed.iter() {
                            for (i, rank) in guest_hidden_ranks.iter().enumerate() {
                                if rank.pawn_id == needed_id {
                                    guest_proof_ranks.push_back(rank);
                                    guest_needed_merkle_proofs.push_back(guest_proofs.get(i as u32).unwrap());
                                }
                            }
                        }
                        let guest_secure_rank_proof_req = ProveRankReq {
                            hidden_ranks: guest_proof_ranks,
                            lobby_id: lobby_secure,
                            merkle_proofs: guest_needed_merkle_proofs,
                        };
                        std::println!("guest prove_rank");
                        setup.client.prove_rank(&guest_secure, &guest_secure_rank_proof_req);
                    }
                    // now host can commit and prove
                    std::println!("host commit_move_and_prove_move");
                    setup.client.commit_move_and_prove_move(&host_secure, &host_secure_commit_move_req, &host_secure_prove_move_req);
                    let rank_data_snapshot_after_host = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);
                    let host_move = rank_data_snapshot_after_host.game_state.moves.get(0).unwrap();
                    let host_needed = host_move.needed_rank_proofs.clone();
                    // now host needs to prove
                    if !host_needed.is_empty() {
                        let mut host_proof_ranks = Vec::new(&setup.env);
                        let mut host_needed_merkle_proofs = Vec::new(&setup.env);
                        for needed_id in host_needed.iter() {
                            for (i, rank) in host_hidden_ranks.iter().enumerate() {
                                if rank.pawn_id == needed_id {
                                    host_proof_ranks.push_back(rank);
                                    host_needed_merkle_proofs.push_back(host_proofs.get(i as u32).unwrap());
                                }
                            }
                        }
                        let host_secure_rank_proof_req = ProveRankReq {
                            hidden_ranks: host_proof_ranks,
                            lobby_id: lobby_secure,
                            merkle_proofs: host_needed_merkle_proofs,
                        };
                        std::println!("host prove_rank");
                        setup.client.prove_rank(&host_secure, &host_secure_rank_proof_req);
                    }
                } else {
                    // regular move commit and prove
                    setup.client.commit_move(&host_secure, &host_secure_commit_move_req);
                    setup.client.prove_move(&host_secure, &host_secure_prove_move_req);
                }
            }
            _ => unreachable!(),
        }
        
        // Handle rank proofs if needed (for Method 1 and 2)
        // Get the current snapshot to check if we need rank proofs
        let secure_snapshot_after_moves = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);
        if secure_snapshot_after_moves.lobby_info.phase == Phase::RankProve {
            std::println!("Secure game needs rank proofs");
            
            let host_move = secure_snapshot_after_moves.game_state.moves.get(0).unwrap();
            let guest_move = secure_snapshot_after_moves.game_state.moves.get(1).unwrap();
            let host_needed = host_move.needed_rank_proofs.clone();
            let guest_needed = guest_move.needed_rank_proofs.clone();
            
            if !host_needed.is_empty() {
                let mut host_proof_ranks = Vec::new(&setup.env);
                let mut host_needed_merkle_proofs = Vec::new(&setup.env);
                for needed_id in host_needed.iter() {
                    for (i, rank) in host_hidden_ranks.iter().enumerate() {
                        if rank.pawn_id == needed_id {
                            host_proof_ranks.push_back(rank);
                            host_needed_merkle_proofs.push_back(host_proofs.get(i as u32).unwrap());
                        }
                    }
                }
                let host_rank_proof_req = ProveRankReq {
                    lobby_id: lobby_secure,
                    hidden_ranks: host_proof_ranks.clone(),
                    merkle_proofs: host_needed_merkle_proofs.clone(),
                };
                std::println!("Providing host rank proofs: {} ranks", host_proof_ranks.len());
                setup.client.prove_rank(&host_secure, &host_rank_proof_req);
            }
            
            if !guest_needed.is_empty() {
                let mut guest_proof_ranks = Vec::new(&setup.env);
                let mut guest_needed_merkle_proofs = Vec::new(&setup.env);
                for needed_id in guest_needed.iter() {
                    for (i, rank) in guest_hidden_ranks.iter().enumerate() {
                        if rank.pawn_id == needed_id {
                            guest_proof_ranks.push_back(rank);
                            guest_needed_merkle_proofs.push_back(guest_proofs.get(i as u32).unwrap());
                        }
                    }
                }
                let guest_rank_proof_req = ProveRankReq {
                    lobby_id: lobby_secure,
                    hidden_ranks: guest_proof_ranks.clone(),
                    merkle_proofs: guest_needed_merkle_proofs.clone(),
                };
                std::println!("Providing guest rank proofs: {} ranks", guest_proof_ranks.len());
                // Check if guest can use prove_move_and_prove_rank
                let guest_move_proof_for_check = guest_move_proof.clone();
                let can_combine = match move_number % 3 {
                    2 => true, // Method 3 where guest already proved move
                    _ => false,
                };
                
                if can_combine && !guest_needed.is_empty() && guest_proof_ranks.len() > 0 {
                    std::println!("Guest using prove_move_and_prove_rank");
                    setup.client.prove_move_and_prove_rank(&guest_secure, &ProveMoveReq {
                        lobby_id: lobby_secure,
                        move_proof: guest_move_proof_for_check,
                    }, &guest_rank_proof_req);
                } else {
                    setup.client.prove_rank(&guest_secure, &guest_rank_proof_req);
                }
            }
        }
        
        // For insecure game, both always use commit_move_and_prove_move
        // Use same moves for insecure game to ensure identical outcomes
        let host_insecure_commit_move_req = CommitMoveReq { lobby_id: lobby_insecure, move_hash: host_hash };
        let guest_insecure_commit_move_req = CommitMoveReq { lobby_id: lobby_insecure, move_hash: guest_hash };
        let host_insecure_prove_move_req = ProveMoveReq { lobby_id: lobby_insecure, move_proof: host_move_proof };
        let guest_insecure_prove_move_req = ProveMoveReq { lobby_id: lobby_insecure, move_proof: guest_move_proof };
        std::println!("insecure host commit_move_and_prove_move");
        setup.client.commit_move_and_prove_move(&host_insecure, &host_insecure_commit_move_req, &host_insecure_prove_move_req);
        std::println!("insecure guest commit_move_and_prove_move");
        setup.client.commit_move_and_prove_move(&guest_insecure, &guest_insecure_commit_move_req, &guest_insecure_prove_move_req);
        let insecure_turn_end_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_insecure);
        if insecure_turn_end_snapshot.lobby_info.phase == Phase::Finished {
            std::println!("insecure game has ended");
            // Check if secure game also ended
            let secure_check = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_secure);
            if secure_check.phase == Phase::Finished {
                std::println!("Both games ended - comparing winners");
                assert_eq!(secure_check.subphase, insecure_turn_end_snapshot.lobby_info.subphase, "Winners don't match");
            }
            break;
        }
        
        // Verify both games have identical states
        let secure_final = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);
        let insecure_final = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_insecure);
        
        let states_match = verify_pawn_states_identical(&secure_final.pawns_map, &insecure_final.pawns_map);
        assert!(states_match, "Pawn states diverged at move {}", move_number);
        
        assert_eq!(secure_final.lobby_info.phase, insecure_final.lobby_info.phase, 
                   "Phases diverged at move {}: secure={:?}, insecure={:?}", 
                   move_number, secure_final.lobby_info.phase, insecure_final.lobby_info.phase);
    }
}

#[test]
fn test_identical_games_different_move_orders() {
    let setup = TestSetup::new();

    let lobby_a = 401;
    let lobby_b = 402;

    let host_a = setup.generate_address();
    let guest_a = setup.generate_address();
    let host_b = setup.generate_address();
    let guest_b = setup.generate_address();

    // Create two identical lobbies
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host_a, &MakeLobbyReq { lobby_id: lobby_a, parameters: lobby_parameters.clone() });
    setup.client.make_lobby(&host_b, &MakeLobbyReq { lobby_id: lobby_b, parameters: lobby_parameters });

    setup.client.join_lobby(&guest_a, &JoinLobbyReq { lobby_id: lobby_a });
    setup.client.join_lobby(&guest_b, &JoinLobbyReq { lobby_id: lobby_b });

    // Generate identical setups using fixed seed
    let (host_setup, host_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_a, &UserIndex::Host)
    });

    let (guest_setup, guest_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_b, &UserIndex::Guest)
    });
    let (host_root, host_proofs) = get_merkel(&setup.env, &host_setup, &host_hidden_ranks);
    let (guest_root, guest_proofs) = get_merkel(&setup.env, &guest_setup, &guest_hidden_ranks);

    // Apply identical setups to both games
    setup.client.commit_setup(&host_a, &CommitSetupReq {
        lobby_id: lobby_a,
        rank_commitment_root: host_root.clone(),
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    setup.client.commit_setup(&guest_a, &CommitSetupReq {
        lobby_id: lobby_a,
        rank_commitment_root: guest_root.clone(),
        zz_hidden_ranks: Vec::new(&setup.env),
    });

    setup.client.commit_setup(&host_b, &CommitSetupReq {
        lobby_id: lobby_b,
        rank_commitment_root: host_root,
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    setup.client.commit_setup(&guest_b, &CommitSetupReq {
        lobby_id: lobby_b,
        rank_commitment_root: guest_root,
        zz_hidden_ranks: Vec::new(&setup.env),
    });

    // Run up to 20 moves
    for move_number in 1..=20 {
        std::println!("=== MOVE {} ===", move_number);

        // Generate moves (same for both games)
        let snapshot_a = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_a);

        if snapshot_a.lobby_info.phase == Phase::Finished || snapshot_a.lobby_info.phase == Phase::Aborted {
            std::println!("Games ended at move {}", move_number);
            break;
        }

        let host_move = generate_valid_move_req(&setup.env, &snapshot_a.pawns_map, &snapshot_a.lobby_parameters, &UserIndex::Host, &host_hidden_ranks, move_number as u64 * 2000);
        let guest_move = generate_valid_move_req(&setup.env, &snapshot_a.pawns_map, &snapshot_a.lobby_parameters, &UserIndex::Guest, &guest_hidden_ranks, move_number as u64 * 2000 + 1);

        if host_move.is_none() || guest_move.is_none() {
            std::println!("No valid moves available");
            break;
        }

        let host_move_proof = host_move.unwrap();
        let guest_move_proof = guest_move.unwrap();

        // Create hashes
        let host_serialized = host_move_proof.clone().to_xdr(&setup.env);
        let host_hash_full = setup.env.crypto().sha256(&host_serialized).to_bytes().to_array();
        let host_hash = HiddenMoveHash::from_array(&setup.env, &host_hash_full[0..16].try_into().unwrap());

        let guest_serialized = guest_move_proof.clone().to_xdr(&setup.env);
        let guest_hash_full = setup.env.crypto().sha256(&guest_serialized).to_bytes().to_array();
        let guest_hash = HiddenMoveHash::from_array(&setup.env, &guest_hash_full[0..16].try_into().unwrap());

        // Game A: Host commits first, then guest
        if move_number % 2 == 1 {
            std::println!("Game A: Host then Guest");
            setup.client.commit_move(&host_a, &CommitMoveReq { lobby_id: lobby_a, move_hash: host_hash.clone() });
            setup.client.commit_move(&guest_a, &CommitMoveReq { lobby_id: lobby_a, move_hash: guest_hash.clone() });
        } else {
            std::println!("Game A: Guest then Host");
            setup.client.commit_move(&guest_a, &CommitMoveReq { lobby_id: lobby_a, move_hash: guest_hash.clone() });
            setup.client.commit_move(&host_a, &CommitMoveReq { lobby_id: lobby_a, move_hash: host_hash.clone() });
        }

        // Game B: Always guest first, then host
        std::println!("Game B: Always Guest then Host");
        setup.client.commit_move(&guest_b, &CommitMoveReq { lobby_id: lobby_b, move_hash: guest_hash });
        setup.client.commit_move(&host_b, &CommitMoveReq { lobby_id: lobby_b, move_hash: host_hash });

        // Check if we should prove moves (take snapshot after commit)
        let post_commit_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_a);
        let should_prove = post_commit_snapshot.phase == Phase::MoveProve;

        if should_prove {
            // Prove moves (state-changing operation)
            setup.client.prove_move(&host_a, &ProveMoveReq { lobby_id: lobby_a, move_proof: host_move_proof.clone() });
            setup.client.prove_move(&guest_a, &ProveMoveReq { lobby_id: lobby_a, move_proof: guest_move_proof.clone() });
            setup.client.prove_move(&host_b, &ProveMoveReq { lobby_id: lobby_b, move_proof: host_move_proof.clone() });
            setup.client.prove_move(&guest_b, &ProveMoveReq { lobby_id: lobby_b, move_proof: guest_move_proof.clone() });
        }

        // Handle rank proving if needed (for both games)
        for (lobby_id, host_addr, guest_addr) in [(lobby_a, &host_a, &guest_a), (lobby_b, &host_b, &guest_b)] {
            // Check current phase using snapshot
            let rank_phase_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
            let game_needs_ranks = rank_phase_snapshot.phase == Phase::RankProve;

            if game_needs_ranks {
                // Get needed ranks from current game state
                let rank_data_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
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
                        setup.client.prove_rank(host_addr, &ProveRankReq { lobby_id, hidden_ranks: host_proof_ranks, merkle_proofs: host_needed_merkle_proofs });
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
                        setup.client.prove_rank(guest_addr, &ProveRankReq { lobby_id, hidden_ranks: guest_proof_ranks, merkle_proofs: guest_needed_merkle_proofs });
                    }
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
        let snapshot_a = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_a);
        let snapshot_b = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_b);
        let states_match = verify_pawn_states_identical(&snapshot_a.pawns_map, &snapshot_b.pawns_map);
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

// region redeem_win tests


#[test]
fn test_redeem_win_simple() {
    let setup = TestSetup::new();
    let lobby_id = 9000;
    
    // Create a simple lobby
    let host = setup.generate_address();
    let guest = setup.generate_address();
    
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    
    // Verify the lobby exists and is in SetupCommit phase
    setup.verify_lobby_info(lobby_id, &host, Phase::SetupCommit);
    
    // Host commits setup, guest doesn't
    let (host_setup, host_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, &UserIndex::Host)
    });
    let (host_root, _) = get_merkel(&setup.env, &host_setup, &host_ranks);
    setup.client.commit_setup(&host, &CommitSetupReq { 
        lobby_id,
        rank_commitment_root: host_root,
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    
    // Now subphase should be Guest - guest needs to act
    
    // Get the current ledger sequence
    let snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    let initial_ledger = snapshot.lobby_info.last_edited_ledger_seq;
    
    // Advance ledger sequence past timeout
    extend_lobby_ttl(&setup, lobby_id);
    setup.env.ledger().with_mut(|l| l.sequence_number = initial_ledger + 100);
    
    // Host can redeem win since guest didn't act
    let result = setup.client.redeem_win(&host, &RedeemWinReq { lobby_id });
    assert_eq!(result.phase, Phase::Aborted);
    assert_eq!(result.subphase, Subphase::None);
}

#[test]
fn test_redeem_win_timeout_boundaries() {
    let setup = TestSetup::new();
    
    // Set up lobby and advance to MoveCommit phase
    let (lobby_id, host, guest, _, _, _, _) = setup_lobby_for_commit_move(&setup, 600);
    
    // Verify the lobby exists
    setup.verify_lobby_info(lobby_id, &host, Phase::MoveCommit);
    
    // Get initial snapshot
    let initial_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    
    // Generate a valid move for host
    let host_move_opt = generate_valid_move_req(
        &setup.env, 
        &initial_snapshot.pawns_map, 
        &initial_snapshot.lobby_parameters, 
        &UserIndex::Host, 
        &Vec::new(&setup.env), // ranks not needed for move generation
        12345
    );
    
    assert!(host_move_opt.is_some(), "Should have valid moves available");
    let host_move = host_move_opt.unwrap();
    
    // Create move hash
    let host_serialized = host_move.to_xdr(&setup.env);
    let host_hash_full = setup.env.crypto().sha256(&host_serialized).to_bytes().to_array();
    let host_hash = HiddenMoveHash::from_array(&setup.env, &host_hash_full[0..16].try_into().unwrap());
    
    // Host commits move
    setup.client.commit_move(&host, &CommitMoveReq { lobby_id, move_hash: host_hash });
    
    // Get the ledger after host commit
    let after_commit = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    let commit_ledger = after_commit.lobby_info.last_edited_ledger_seq;
    assert_eq!(after_commit.lobby_info.subphase, Subphase::Guest);
    
    // Test 1: Try to redeem one ledger too early (should fail)
    extend_lobby_ttl(&setup, lobby_id);
    setup.env.ledger().with_mut(|l| l.sequence_number = commit_ledger + 99);
    let early_result = setup.client.try_redeem_win(&host, &RedeemWinReq { lobby_id });
    assert!(early_result.is_err());
    
    // Test 2: Try to redeem exactly at timeout (should succeed)
    setup.env.ledger().with_mut(|l| l.sequence_number = commit_ledger + 100);
    let exact_result = setup.client.redeem_win(&host, &RedeemWinReq { lobby_id });
    assert_eq!(exact_result.phase, Phase::Finished);
    assert_eq!(exact_result.subphase, Subphase::Host);
    
    // Test 3: Set up another game for "well after timeout" test
    let (lobby_id2, host2, guest2, _, _, _, _) = setup_lobby_for_commit_move(&setup, 601);
    
    // Get valid move and commit
    let snapshot2 = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id2);
    let host_move2 = generate_valid_move_req(
        &setup.env,
        &snapshot2.pawns_map,
        &snapshot2.lobby_parameters,
        &UserIndex::Host,
        &Vec::new(&setup.env),
        12346
    ).unwrap();
    
    let host_serialized2 = host_move2.to_xdr(&setup.env);
    let host_hash_full2 = setup.env.crypto().sha256(&host_serialized2).to_bytes().to_array();
    let host_hash2 = HiddenMoveHash::from_array(&setup.env, &host_hash_full2[0..16].try_into().unwrap());
    
    setup.client.commit_move(&host2, &CommitMoveReq { lobby_id: lobby_id2, move_hash: host_hash2 });
    
    let snapshot2_after = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id2);
    let ledger2 = snapshot2_after.lobby_info.last_edited_ledger_seq;
    
    // Try to redeem well after timeout (should succeed)
    extend_lobby_ttl(&setup, lobby_id2);
    setup.env.ledger().with_mut(|l| l.sequence_number = ledger2 + 1000);
    let late_result = setup.client.redeem_win(&host2, &RedeemWinReq { lobby_id: lobby_id2 });
    assert_eq!(late_result.phase, Phase::Finished);
    assert_eq!(late_result.subphase, Subphase::Host);
}

#[test]
fn test_redeem_win_phase_specific_behavior() {
    let setup = TestSetup::new();
    
    // Test 1: SetupCommit timeout -> Aborted with no winner
    let (lobby_id1, host1, guest1) = setup_lobby_for_commit_setup(&setup, 700);
    
    // Host commits setup, guest doesn't
    let (host_setup, host_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id1, &UserIndex::Host)
    });
    let (host_root, _) = get_merkel(&setup.env, &host_setup, &host_ranks);
    setup.client.commit_setup(&host1, &CommitSetupReq { 
        lobby_id: lobby_id1,
        rank_commitment_root: host_root,
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    
    let snapshot1 = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id1);
    assert_eq!(snapshot1.lobby_info.subphase, Subphase::Guest);
    
    // Advance ledger and redeem
    extend_lobby_ttl(&setup, lobby_id1);
    setup.env.ledger().with_mut(|l| l.sequence_number = snapshot1.lobby_info.last_edited_ledger_seq + 100);
    let result1 = setup.client.redeem_win(&host1, &RedeemWinReq { lobby_id: lobby_id1 });
    assert_eq!(result1.phase, Phase::Aborted);
    assert_eq!(result1.subphase, Subphase::None);
    
    // Test 2: MoveCommit timeout -> Winner
    let (lobby_id2, host2, guest2, _, _, _, _) = setup_lobby_for_commit_move(&setup, 701);
    
    // Get valid move for host
    let snapshot2 = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id2);
    let host_move = generate_valid_move_req(
        &setup.env,
        &snapshot2.pawns_map,
        &snapshot2.lobby_parameters,
        &UserIndex::Host,
        &Vec::new(&setup.env),
        12347
    ).unwrap();
    
    let host_serialized = host_move.to_xdr(&setup.env);
    let host_hash_full = setup.env.crypto().sha256(&host_serialized).to_bytes().to_array();
    let host_hash = HiddenMoveHash::from_array(&setup.env, &host_hash_full[0..16].try_into().unwrap());
    
    setup.client.commit_move(&host2, &CommitMoveReq { lobby_id: lobby_id2, move_hash: host_hash });
    
    let snapshot2_after = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id2);
    extend_lobby_ttl(&setup, lobby_id2);
    setup.env.ledger().with_mut(|l| l.sequence_number = snapshot2_after.lobby_info.last_edited_ledger_seq + 100);
    let result2 = setup.client.redeem_win(&host2, &RedeemWinReq { lobby_id: lobby_id2 });
    assert_eq!(result2.phase, Phase::Finished);
    assert_eq!(result2.subphase, Subphase::Host);
}

#[test]
fn test_redeem_win_state_changes() {
    let setup = TestSetup::new();
    
    let (lobby_id, host, guest, _, _, _, _) = setup_lobby_for_commit_move(&setup, 900);
    
    // Get valid move for host
    let snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    let host_move = generate_valid_move_req(
        &setup.env,
        &snapshot.pawns_map,
        &snapshot.lobby_parameters,
        &UserIndex::Host,
        &Vec::new(&setup.env),
        12348
    ).unwrap();
    
    let host_serialized = host_move.to_xdr(&setup.env);
    let host_hash_full = setup.env.crypto().sha256(&host_serialized).to_bytes().to_array();
    let host_hash = HiddenMoveHash::from_array(&setup.env, &host_hash_full[0..16].try_into().unwrap());
    
    setup.client.commit_move(&host, &CommitMoveReq { lobby_id, move_hash: host_hash });
    
    // Capture states before redeem_win
    let before_lobby = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    let before_game_state = before_lobby.game_state.clone();
    let before_ledger = before_lobby.lobby_info.last_edited_ledger_seq;
    
    // Advance ledger and redeem
    extend_lobby_ttl(&setup, lobby_id);
    setup.env.ledger().with_mut(|l| l.sequence_number = before_ledger + 100);
    let current_ledger = setup.env.ledger().sequence();
    let result = setup.client.redeem_win(&host, &RedeemWinReq { lobby_id });
    
    // Verify state changes
    let after = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    
    // GameState should be unchanged
    assert_eq!(after.game_state.moves.get(0).unwrap().move_hash, before_game_state.moves.get(0).unwrap().move_hash);
    assert_eq!(after.game_state.turn, before_game_state.turn);
    assert_eq!(after.game_state.rank_roots, before_game_state.rank_roots);
    
    // LobbyInfo should be updated
    assert_eq!(after.lobby_info.phase, Phase::Finished);
    assert_eq!(after.lobby_info.subphase, Subphase::Host);
    assert_eq!(after.lobby_info.last_edited_ledger_seq, current_ledger);
    
    // Second call should fail with WrongPhase
    let second_result = setup.client.try_redeem_win(&guest, &RedeemWinReq { lobby_id });
    assert!(second_result.is_err());
}

#[test]
fn test_action_invalidates_timeout_claim() {
    let setup = TestSetup::new();
    
    let (lobby_id, host, guest, _, _, _, _) = setup_lobby_for_commit_move(&setup, 1000);
    
    // Get valid moves for both players
    let snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    let host_move = generate_valid_move_req(
        &setup.env,
        &snapshot.pawns_map,
        &snapshot.lobby_parameters,
        &UserIndex::Host,
        &Vec::new(&setup.env),
        12349
    ).unwrap();
    
    let guest_move = generate_valid_move_req(
        &setup.env,
        &snapshot.pawns_map,
        &snapshot.lobby_parameters,
        &UserIndex::Guest,
        &Vec::new(&setup.env),
        12350
    ).unwrap();
    
    // Create hashes
    let host_serialized = host_move.to_xdr(&setup.env);
    let host_hash_full = setup.env.crypto().sha256(&host_serialized).to_bytes().to_array();
    let host_hash = HiddenMoveHash::from_array(&setup.env, &host_hash_full[0..16].try_into().unwrap());
    
    let guest_serialized = guest_move.to_xdr(&setup.env);
    let guest_hash_full = setup.env.crypto().sha256(&guest_serialized).to_bytes().to_array();
    let guest_hash = HiddenMoveHash::from_array(&setup.env, &guest_hash_full[0..16].try_into().unwrap());
    
    // Host commits move
    setup.client.commit_move(&host, &CommitMoveReq { lobby_id, move_hash: host_hash });
    
    let after_host = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    let host_commit_ledger = after_host.lobby_info.last_edited_ledger_seq;
    
    // Advance to just before timeout
    extend_lobby_ttl(&setup, lobby_id);
    setup.env.ledger().with_mut(|l| l.sequence_number = host_commit_ledger + 99);
    
    // Guest acts at the last moment
    setup.client.commit_move(&guest, &CommitMoveReq { lobby_id, move_hash: guest_hash });
    
    // Now advance past original timeout
    setup.env.ledger().with_mut(|l| l.sequence_number = host_commit_ledger + 101);
    
    // Host's redeem_win should fail because guest acted and updated last_edited_ledger_seq
    let result = setup.client.try_redeem_win(&host, &RedeemWinReq { lobby_id });
    assert!(result.is_err());
    
    // Verify game is still in progress
    let final_state = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(final_state.phase, Phase::MoveProve);
    assert_eq!(final_state.subphase, Subphase::Both);
}

// endregion
// endregion