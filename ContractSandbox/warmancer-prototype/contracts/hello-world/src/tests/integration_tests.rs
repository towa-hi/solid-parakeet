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
                    move_hashes: Vec::from_array(&setup.env, [host_move_hash]),
                    lobby_id,
                };
                let guest_move_req = CommitMoveReq {
                    move_hashes: Vec::from_array(&setup.env, [guest_move_hash]),
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
                            move_proofs: Vec::from_array(&setup.env, [host_move_proof]),
                            lobby_id,
                        };
                        let guest_prove_move_req = ProveMoveReq {
                            move_proofs: Vec::from_array(&setup.env, [guest_move_proof]),
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
            move_hashes: Vec::from_array(&setup.env, [host_hash.clone()]),
        });
        setup.client.commit_move(&guest_secure, &CommitMoveReq {
            lobby_id: lobby_secure,
            move_hashes: Vec::from_array(&setup.env, [guest_hash.clone()]),
        });

        setup.client.prove_move(&host_secure, &ProveMoveReq {
            lobby_id: lobby_secure,
            move_proofs: Vec::from_array(&setup.env, [host_move_proof.clone()]),
        });
        setup.client.prove_move(&guest_secure, &ProveMoveReq {
            lobby_id: lobby_secure,
            move_proofs: Vec::from_array(&setup.env, [guest_move_proof.clone()]),
        });

        // Insecure mode: commit and prove together
        setup.client.commit_move_and_prove_move(&host_insecure, 
            &CommitMoveReq {
                lobby_id: lobby_insecure,
                move_hashes: Vec::from_array(&setup.env, [host_hash]),
            },
            &ProveMoveReq {
                lobby_id: lobby_insecure,
                move_proofs: Vec::from_array(&setup.env, [host_move_proof]),
            }
        );
        setup.client.commit_move_and_prove_move(&guest_insecure,
            &CommitMoveReq {
                lobby_id: lobby_insecure,
                move_hashes: Vec::from_array(&setup.env, [guest_hash]),
            },
            &ProveMoveReq {
                lobby_id: lobby_insecure,
                move_proofs: Vec::from_array(&setup.env, [guest_move_proof]),
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

fn execute_commit_and_prove_atomic_pattern(
    setup: &TestSetup,
    host: &soroban_sdk::Address,
    guest: &soroban_sdk::Address,
    host_commit_req: &CommitMoveReq,
    guest_commit_req: &CommitMoveReq,
    host_prove_req: &ProveMoveReq,
    guest_prove_req: &ProveMoveReq,
    host_hidden_ranks: &Vec<HiddenRank>,
    guest_hidden_ranks: &Vec<HiddenRank>,
    host_merkle_proofs: &Vec<MerkleProof>,
    guest_merkle_proofs: &Vec<MerkleProof>,
    host_label: &str,
    guest_label: &str,
    labels: &[&str],
) {
    let fn_name = ">execute_commit_and_prove_atomic_pattern()";
    //only secure mode lobbies can do this
    let lobby_id = host_commit_req.lobby_id;
    let pre_turn_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert!(pre_turn_snapshot.lobby_parameters.security_mode);
    assert_eq!(pre_turn_snapshot.lobby_info.phase, Phase::MoveCommit);
    assert_eq!(pre_turn_snapshot.lobby_info.subphase, Subphase::Both);
    // Step 1: Both players commit
    setup.commit_move(host, host_commit_req, &[labels, &[fn_name, host_label]].concat());
    setup.commit_move(guest, guest_commit_req, &[labels, &[fn_name, guest_label]].concat());
    // Step 2: Both players prove
    setup.prove_move(host, host_prove_req, &[fn_name, host_label]);
    setup.prove_move(guest, guest_prove_req, &[fn_name, guest_label]);
    // Step 3: Handle rank proofs if needed
    let post_prove_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    if post_prove_snapshot.lobby_info.phase == Phase::RankProve {
        std::println!("Secure game needs rank proofs");
        
        let host_move = post_prove_snapshot.game_state.moves.get(0).unwrap();
        let guest_move = post_prove_snapshot.game_state.moves.get(1).unwrap();
        let host_needed = host_move.needed_rank_proofs.clone();
        let guest_needed = guest_move.needed_rank_proofs.clone();
        
        if !host_needed.is_empty() {
            let mut host_proof_ranks = Vec::new(&setup.env);
            let mut host_needed_merkle = Vec::new(&setup.env);
            for needed_id in host_needed.iter() {
                for (i, rank) in host_hidden_ranks.iter().enumerate() {
                    if rank.pawn_id == needed_id {
                        host_proof_ranks.push_back(rank);
                        host_needed_merkle.push_back(host_merkle_proofs.get(i as u32).unwrap());
                    }
                }
            }
            let host_rank_proof_req = ProveRankReq {
                lobby_id,
                hidden_ranks: host_proof_ranks.clone(),
                merkle_proofs: host_needed_merkle.clone(),
            };
            setup.prove_rank(host, &host_rank_proof_req, &[labels, &[fn_name, host_label]].concat());
        }
        
        if !guest_needed.is_empty() {
            let mut guest_proof_ranks = Vec::new(&setup.env);
            let mut guest_needed_merkle = Vec::new(&setup.env);
            for needed_id in guest_needed.iter() {
                for (i, rank) in guest_hidden_ranks.iter().enumerate() {
                    if rank.pawn_id == needed_id {
                        guest_proof_ranks.push_back(rank);
                        guest_needed_merkle.push_back(guest_merkle_proofs.get(i as u32).unwrap());
                    }
                }
            }
            let guest_rank_proof_req = ProveRankReq {
                lobby_id,
                hidden_ranks: guest_proof_ranks.clone(),
                merkle_proofs: guest_needed_merkle.clone(),
            };
            setup.prove_rank(guest, &guest_rank_proof_req, &[labels, &[fn_name, guest_label]].concat());
        }
    }
    
    // Verify final state is MoveCommit Both or Finished
    let end_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert!(
        (end_snapshot.phase == Phase::MoveCommit && end_snapshot.subphase == Subphase::Both) ||
        end_snapshot.phase == Phase::Finished,
        "Method ended in unexpected state: {:?} {:?}", end_snapshot.phase, end_snapshot.subphase
    );
}

fn execute_commit_and_prove_batch_pattern(
    setup: &TestSetup,
    first_player: &soroban_sdk::Address,
    second_player: &soroban_sdk::Address,
    first_commit_req: &CommitMoveReq,
    second_commit_req: &CommitMoveReq,
    first_prove_req: &ProveMoveReq,
    second_prove_req: &ProveMoveReq,
    first_hidden_ranks: &Vec<HiddenRank>,
    second_hidden_ranks: &Vec<HiddenRank>,
    first_merkle_proofs: &Vec<MerkleProof>,
    second_merkle_proofs: &Vec<MerkleProof>,
    first_label: &str,
    second_label: &str,
    labels: &[&str],
) {
    let fn_name = ">execute_commit_and_prove_batch_pattern()";
    let lobby_id = first_prove_req.lobby_id;
    // Step 1: First player commits
    setup.commit_move(first_player, first_commit_req, &[fn_name, first_label]);
    
    // Step 2: Second player commits and proves
    setup.commit_move_and_prove_move(second_player, second_commit_req, second_prove_req, &[fn_name, second_label]);
    
    // Step 3: First player checks simulation to decide whether to use prove_move or prove_move_and_prove_rank
    std::println!("{} simulate_collisions", first_label);
    let first_simulation_result = setup.client.simulate_collisions(first_player, first_prove_req);
    let first_needs_rank_proofs = !first_simulation_result.needed_rank_proofs.is_empty();
    
    if first_needs_rank_proofs {
        std::println!("{} simulation shows rank proofs needed: {:?}", first_label, first_simulation_result.needed_rank_proofs);
        
        // Prepare rank proofs for first player
        let mut first_proof_ranks = Vec::new(&setup.env);
        let mut first_needed_merkle = Vec::new(&setup.env);
        for needed_id in first_simulation_result.needed_rank_proofs.iter() {
            for (i, rank) in first_hidden_ranks.iter().enumerate() {
                if rank.pawn_id == needed_id {
                    first_proof_ranks.push_back(rank);
                    first_needed_merkle.push_back(first_merkle_proofs.get(i as u32).unwrap());
                }
            }
        }
        
        let first_rank_proof_req = ProveRankReq {
            lobby_id,
            hidden_ranks: first_proof_ranks,
            merkle_proofs: first_needed_merkle,
        };
        
        // Use prove_move_and_prove_rank
        setup.prove_move_and_prove_rank(first_player, first_prove_req, &first_rank_proof_req, &[fn_name, first_label]);
    } else {
        // Just prove move
        setup.prove_move(first_player, first_prove_req, &[fn_name, first_label]);
    }
    
    // Step 4: Second player reads state to see if they need to prove_rank
    let final_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    if final_snapshot.lobby_info.phase == Phase::RankProve {
        // Get second player's index
        let mut second_index = 1;
        if second_player.clone() == final_snapshot.lobby_info.host_address.get_unchecked(0) {
            second_index = 0;
        }
        let second_move = final_snapshot.game_state.moves.get(second_index).unwrap();
        let second_needed = second_move.needed_rank_proofs.clone();
        
        if !second_needed.is_empty() {
            let mut second_proof_ranks = Vec::new(&setup.env);
            let mut second_needed_merkle = Vec::new(&setup.env);
            for needed_id in second_needed.iter() {
                for (i, rank) in second_hidden_ranks.iter().enumerate() {
                    if rank.pawn_id == needed_id {
                        second_proof_ranks.push_back(rank);
                        second_needed_merkle.push_back(second_merkle_proofs.get(i as u32).unwrap());
                    }
                }
            }
            
            let second_rank_proof_req = ProveRankReq {
                lobby_id,
                hidden_ranks: second_proof_ranks,
                merkle_proofs: second_needed_merkle,
            };
            
            setup.prove_rank(second_player, &second_rank_proof_req, &[fn_name, second_label]);
        }
    }
    
    // Verify final state is MoveCommit Both or Finished
    let end_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert!(
        (end_snapshot.phase == Phase::MoveCommit && end_snapshot.subphase == Subphase::Both) ||
        end_snapshot.phase == Phase::Finished,
        "Method ended in unexpected state: {:?} {:?}", end_snapshot.phase, end_snapshot.subphase
    );
}
fn execute_insecure_batch_pattern(
    setup: &TestSetup,
    first_player: &Address,
    second_player: &Address,
    first_commit_req: &CommitMoveReq,
    second_commit_req: &CommitMoveReq,
    first_prove_req: &ProveMoveReq,
    second_prove_req: &ProveMoveReq,
    first_label: &str,
    second_label: &str,
    labels: &[&str],
) {
    let fn_name = ">execute_insecure_batch_pattern";
    setup.commit_move_and_prove_move(first_player, first_commit_req, first_prove_req, &[fn_name, first_label]);
    setup.commit_move_and_prove_move(second_player, second_commit_req, second_prove_req, &[fn_name, second_label]);

}
#[test]
fn test_compare_move_submission_methods() {
    let fn_name = ">test_compare_move_submission_methods()";
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
    setup.join_lobby(&guest_secure, &join_req_secure, &[fn_name, "-secure guest"]);
    
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
    // for host_hidden_rank in host_hidden_ranks.clone() {
    //     std::println!("host pawnid: {} rank: {}", host_hidden_rank.pawn_id, host_hidden_rank.rank);
    // }
    // for guest_hidden_rank in guest_hidden_ranks.clone() {
    //     std::println!("guest pawnid: {} rank: {}", guest_hidden_rank.pawn_id, guest_hidden_rank.rank);
    // }
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
    for move_number in 1..=100 {
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
        let host_serialized = host_move_proof.clone().to_xdr(&setup.env);
        let host_hash_full = setup.env.crypto().sha256(&host_serialized).to_bytes().to_array();
        let host_hash = HiddenMoveHash::from_array(&setup.env, &host_hash_full[0..16].try_into().unwrap());
        let guest_serialized = guest_move_proof.clone().to_xdr(&setup.env);
        let guest_hash_full = setup.env.crypto().sha256(&guest_serialized).to_bytes().to_array();
        let guest_hash = HiddenMoveHash::from_array(&setup.env, &guest_hash_full[0..16].try_into().unwrap());
        // do secure turn
        {
            // create move reqs
            let host_secure_commit_move_req = CommitMoveReq { lobby_id: lobby_secure, move_hashes: Vec::from_array(&setup.env, [host_hash.clone()]) };
            let guest_secure_commit_move_req = CommitMoveReq { lobby_id: lobby_secure, move_hashes: Vec::from_array(&setup.env, [guest_hash.clone()]) };
            let host_secure_prove_move_req = ProveMoveReq { lobby_id: lobby_secure, move_proofs: Vec::from_array(&setup.env, [host_move_proof.clone()]) };
            let guest_secure_prove_move_req = ProveMoveReq { lobby_id: lobby_secure, move_proofs: Vec::from_array(&setup.env, [guest_move_proof.clone()]) };
            // For secure game, choose submission method based on move number
            match move_number % 3 {
                0 => {
                    // Method 0: Both use commit then prove separately
                    execute_commit_and_prove_atomic_pattern(
                        &setup,
                        &host_secure,
                        &guest_secure,
                        &host_secure_commit_move_req,
                        &guest_secure_commit_move_req,
                        &host_secure_prove_move_req,
                        &guest_secure_prove_move_req,
                        &host_hidden_ranks,
                        &guest_hidden_ranks,
                        &host_proofs,
                        &guest_proofs,
                        "-secure host",
                        "-secure guest",
                        &[fn_name],
                    );
                }
                1 => {
                    // Method 1: Host commits first, guest uses commit_and_prove
                    execute_commit_and_prove_batch_pattern(
                        &setup,
                        &host_secure,
                        &guest_secure,
                        &host_secure_commit_move_req,
                        &guest_secure_commit_move_req,
                        &host_secure_prove_move_req,
                        &guest_secure_prove_move_req,
                        &host_hidden_ranks,
                        &guest_hidden_ranks,
                        &host_proofs,
                        &guest_proofs,
                        "-secure host",
                        "-secure guest",
                        &[fn_name],
                    );
                }
                2 => {
                    // Method 2: Guest commits first, host uses commit_and_prove
                    execute_commit_and_prove_batch_pattern(
                        &setup,
                        &guest_secure,
                        &host_secure,
                        &guest_secure_commit_move_req,
                        &host_secure_commit_move_req,
                        &guest_secure_prove_move_req,
                        &host_secure_prove_move_req,
                        &guest_hidden_ranks,
                        &host_hidden_ranks,
                        &guest_proofs,
                        &host_proofs,
                        "-secure host",
                        "-secure guest",
                        &[fn_name],
                    );
                }
                _ => unreachable!(),
            }
        }
        {
            // For insecure game, both always use commit_move_and_prove_move and use same moves as secure to ensure identical outcomes
            let host_insecure_commit_move_req = CommitMoveReq { lobby_id: lobby_insecure, move_hashes: Vec::from_array(&setup.env, [host_hash]) };
            let guest_insecure_commit_move_req = CommitMoveReq { lobby_id: lobby_insecure, move_hashes: Vec::from_array(&setup.env, [guest_hash]) };
            let host_insecure_prove_move_req = ProveMoveReq { lobby_id: lobby_insecure, move_proofs: Vec::from_array(&setup.env, [host_move_proof]) };
            let guest_insecure_prove_move_req = ProveMoveReq { lobby_id: lobby_insecure, move_proofs: Vec::from_array(&setup.env, [guest_move_proof]) };
            execute_insecure_batch_pattern(
                &setup,
                &host_insecure,
                &guest_insecure,
                &host_insecure_commit_move_req,
                &guest_insecure_commit_move_req,
                &host_insecure_prove_move_req,
                &guest_insecure_prove_move_req,
                "-insecure host",
                "-insecure guest",
                &[fn_name],
            );
        }

        // Verify both games have identical states
        let secure_final = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);
        let insecure_final = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_insecure);
        
        let states_match = verify_pawn_states_identical(&secure_final.pawns_map, &insecure_final.pawns_map);
        assert!(states_match, "Pawn states diverged at move {}", move_number);
        
        assert_eq!(secure_final.lobby_info.phase, insecure_final.lobby_info.phase, 
                   "Phases diverged at move {}: secure={:?}, insecure={:?}", 
                   move_number, secure_final.lobby_info.phase, insecure_final.lobby_info.phase);
        if states_match && secure_final.lobby_info.phase == Phase::Finished {
            std::println!("The game has Finished with the winner {:?}", secure_final.lobby_info.subphase);
            break
        }
    }
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
    setup.client.commit_move(&host, &CommitMoveReq { lobby_id, move_hashes: Vec::from_array(&setup.env, [host_hash]) });
    
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
    
    setup.client.commit_move(&host2, &CommitMoveReq { lobby_id: lobby_id2, move_hashes: Vec::from_array(&setup.env, [host_hash2]) });
    
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
    
    setup.client.commit_move(&host2, &CommitMoveReq { lobby_id: lobby_id2, move_hashes: Vec::from_array(&setup.env, [host_hash]) });
    
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
    
    setup.client.commit_move(&host, &CommitMoveReq { lobby_id, move_hashes: Vec::from_array(&setup.env, [host_hash]) });
    
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
    assert_eq!(after.game_state.moves.get(0).unwrap().move_hashes, before_game_state.moves.get(0).unwrap().move_hashes);
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
    setup.client.commit_move(&host, &CommitMoveReq { lobby_id, move_hashes: Vec::from_array(&setup.env, [host_hash]) });
    
    let after_host = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    let host_commit_ledger = after_host.lobby_info.last_edited_ledger_seq;
    
    // Advance to just before timeout
    extend_lobby_ttl(&setup, lobby_id);
    setup.env.ledger().with_mut(|l| l.sequence_number = host_commit_ledger + 99);
    
    // Guest acts at the last moment
    setup.client.commit_move(&guest, &CommitMoveReq { lobby_id, move_hashes: Vec::from_array(&setup.env, [guest_hash]) });
    
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