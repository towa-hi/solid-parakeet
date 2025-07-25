#![cfg(test)]
#![allow(unused_variables)]
extern crate std;

use super::super::*;
use super::super::test_utils::*;
use super::test_utils::*;

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
                    lobby_id,
                    move_hash: host_move_hash,
                };
                let guest_move_req = CommitMoveReq {
                    lobby_id,
                    move_hash: guest_move_hash,
                };

                // host commits move first
                setup.client.commit_move(&host_address, &host_move_req);

                let guest_prove_move_req = ProveMoveReq {
                    move_proof: guest_move_proof,
                    lobby_id,
                };
                // latter player can use commit_move_and_prove_move for convenience
                setup.client.commit_move_and_prove_move(&guest_address, &guest_move_req, &guest_prove_move_req);

                // Take snapshot after committing moves to check new phase
                let post_commit_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
                let current_phase_after_commit = post_commit_snapshot.phase.clone();

                std::println!("After committing moves for turn {}: current_phase = {:?}", move_number, current_phase_after_commit);

                // Only proceed with move proving if we're in MoveProve phase
                if current_phase_after_commit == Phase::MoveProve {
                    std::println!("Proceeding with MoveProve phase for turn {}", move_number);

                    // player who went first (host) has to prove move but does a sim to check if he should batch it with prove rank
                    let host_prove_move_req = ProveMoveReq {
                        move_proof: host_move_proof,
                        lobby_id,
                    };
                    // simulation
                    let host_simulation_result = setup.client.simulate_collisions(&host_address, &host_prove_move_req);
                    if !host_simulation_result.needed_rank_proofs.is_empty() {
                        let mut host_hidden_ranks_temp = Vec::new(&setup.env);
                        let mut host_merkle_proofs_temp = Vec::new(&setup.env);
                        for pawn_id in host_simulation_result.needed_rank_proofs.iter() {
                            for (i, hidden_rank) in host_ranks.iter().enumerate() {
                                if hidden_rank.pawn_id == pawn_id {
                                    host_hidden_ranks_temp.push_back(hidden_rank.clone());
                                    host_merkle_proofs_temp.push_back(host_merkle_proofs.get_unchecked(i as u32));
                                }
                            }
                        }
                        let f_host_rank_proof_req = ProveRankReq {
                            hidden_ranks: host_hidden_ranks_temp,
                            lobby_id,
                            merkle_proofs: host_merkle_proofs_temp,
                        };
                        setup.client.prove_move_and_prove_rank(&host_address, &host_prove_move_req, &f_host_rank_proof_req);
                    }
                    else {
                        // submit just prove move if the simulation says you didn't collide
                        setup.client.prove_move(&host_address, &host_prove_move_req);
                    }

                    {
                        let after_host_move_prove_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);

                        // if guest needs to submit a rank proof
                        if after_host_move_prove_snapshot.lobby_info.phase == Phase::RankProve && after_host_move_prove_snapshot.lobby_info.subphase == Subphase::Guest
                        {
                            let mut guest_hidden_ranks_temp = Vec::new(&setup.env);
                            let mut guest_merkle_proofs_temp = Vec::new(&setup.env);
                            for pawn_id in after_host_move_prove_snapshot.game_state.moves.get_unchecked(1).needed_rank_proofs.iter() {
                                for (i, hidden_rank) in guest_ranks.iter().enumerate() {
                                    if hidden_rank.pawn_id == pawn_id {
                                        guest_hidden_ranks_temp.push_back(hidden_rank.clone());
                                        guest_merkle_proofs_temp.push_back(guest_merkle_proofs.get_unchecked(i as u32));
                                        break;
                                    }
                                }
                            }
                            let guest_prove_rank_req = ProveRankReq {
                                hidden_ranks: guest_hidden_ranks_temp,
                                lobby_id,
                                merkle_proofs: guest_merkle_proofs_temp,
                            };
                            setup.client.prove_rank(&guest_address, &guest_prove_rank_req);
                            let after_rank_prove_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
                        }
                        else {
                            validate_move_prove_transition(&after_host_move_prove_snapshot, &host_prove_move_req, &guest_prove_move_req);

                        }
                    }

                } else {
                    std::println!("Skipped MoveProve phase - game already advanced to next phase: {:?}", current_phase_after_commit);
                }

                std::println!("Moves committed and proved successfully");
            },

            Phase::RankProve => {
                // std::println!("Phase: RankProve - Proving ranks for collisions");
                //
                // // Get the needed rank proofs for both players from snapshot
                // let host_move = loop_start_snapshot.game_state.moves.get(0).unwrap();
                // std::println!("Host move: {:?}", host_move);
                // let guest_move = loop_start_snapshot.game_state.moves.get(1).unwrap();
                // std::println!("Guest move: {:?}", guest_move);
                // let host_needed_ranks = host_move.needed_rank_proofs.clone();
                // let guest_needed_ranks = guest_move.needed_rank_proofs.clone();
                // // Debug: Print needed ranks for both players
                // for id in host_needed_ranks.iter() {
                //     let (_, team) = Contract::decode_pawn_id(&id);
                //     std::println!("Host needed rank: {} team: {}", id.clone(), team);
                // }
                // for id in guest_needed_ranks.iter() {
                //     let (_, team) = Contract::decode_pawn_id(&id);
                //     std::println!("Guest needed rank: {} team: {}", id.clone(), team);
                // }
                //
                // // Submit rank proofs if there are any needed
                // let (host_rank_req, guest_rank_req) = if !host_needed_ranks.is_empty() || !guest_needed_ranks.is_empty() {
                //     std::println!("Rank proofs needed - creating and submitting rank proof requests");
                //
                //     // Create rank proof requests
                //     let (host_req, guest_req) = create_rank_proof_requests(&setup.env, lobby_id,
                //                      &host_needed_ranks, &guest_needed_ranks, &host_ranks, &guest_ranks, &host_merkle_proofs, &guest_merkle_proofs);
                //
                //     // Submit rank proofs to the contract (state-changing operation)
                //     if let Some(ref host_req) = host_req {
                //         std::println!("Host proving {} ranks", host_req.hidden_ranks.len());
                //         setup.client.prove_rank(&host_address, host_req);
                //     }
                //     if let Some(ref guest_req) = guest_req {
                //         std::println!("Guest proving {} ranks", guest_req.hidden_ranks.len());
                //         setup.client.prove_rank(&guest_address, guest_req);
                //     }
                //
                //     (host_req, guest_req)
                // } else {
                //     std::println!("No rank proofs needed - skipping rank proof submission");
                //     (None, None)
                // };
                //
                // // VALIDATE: Check game state after rank proof submission (if any occurred)
                // if host_rank_req.is_some() || guest_rank_req.is_some() {
                //     let rank_validation_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
                //     validate_rank_prove_transition(&rank_validation_snapshot, host_rank_req.as_ref(), guest_rank_req.as_ref());
                // }
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
fn test_compare_security_games() {
    let setup = TestSetup::new();
    let host_secure = setup.generate_address();
    let guest_secure = setup.generate_address();
    let host_insecure = setup.generate_address();
    let guest_insecure = setup.generate_address();

    let lobby_secure = 1u32;
    let lobby_insecure = 2u32;

    let lobby_parameters_secure = create_full_stratego_board_parameters(&setup.env);
    let mut lobby_parameters_insecure = lobby_parameters_secure.clone();
    // secure
    lobby_parameters_insecure.security_mode = false;
    let make_req = MakeLobbyReq {
        lobby_id: lobby_secure,
        parameters: lobby_parameters_secure,
    };
    setup.client.make_lobby(&host_secure, &make_req);
    let join_req = JoinLobbyReq {
        lobby_id: lobby_secure,
    };
    setup.client.join_lobby(&guest_secure, &join_req);
    // insecure
    let make_req_insecure = MakeLobbyReq {
        lobby_id: lobby_insecure,
        parameters: lobby_parameters_insecure,
    };
    setup.client.make_lobby(&host_insecure, &make_req_insecure);
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

    let (host_root, host_proofs) = get_merkel(&setup.env, &host_setup, &host_hidden_ranks.clone());
    let (guest_root, guest_proofs) = get_merkel(&setup.env, &guest_setup, &guest_hidden_ranks.clone());

    // Apply setups to both games
    std::println!("host committing setup");
    setup.client.commit_setup(&host_secure, &CommitSetupReq { lobby_id: lobby_secure, rank_commitment_root: host_root.clone() , zz_hidden_ranks: Vec::new(&setup.env),});
    std::println!("guest committing setup");
    setup.client.commit_setup(&guest_secure, &CommitSetupReq { lobby_id: lobby_secure, rank_commitment_root: guest_root.clone(), zz_hidden_ranks: Vec::new(&setup.env),});

    setup.client.commit_setup(&host_insecure, &CommitSetupReq { lobby_id: lobby_insecure, rank_commitment_root: host_root.clone() , zz_hidden_ranks: host_hidden_ranks.clone(),});
    setup.client.commit_setup(&guest_insecure, &CommitSetupReq { lobby_id: lobby_insecure, rank_commitment_root: guest_root.clone() , zz_hidden_ranks: guest_hidden_ranks.clone(),});

    // Execute identical moves and verify game states remain consistent
    for move_number in 1..=100 {
        std::println!("Move number: {}", move_number);
        // Generate identical moves for both games
        let salt_host = move_number as u64 * 1000 + 12345;
        let salt_guest = move_number as u64 * 1000 + 54321;

        // Take snapshot at start of loop to get current game state
        let secure_loop_start_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);

        // shared - generate moves based on secure game state (which should match insecure at this point)
        let host_move = generate_valid_move_req(&setup.env, &secure_loop_start_snapshot.pawns_map, &secure_loop_start_snapshot.lobby_parameters, &UserIndex::Host, &host_hidden_ranks, salt_host);
        let guest_move = generate_valid_move_req(&setup.env, &secure_loop_start_snapshot.pawns_map, &secure_loop_start_snapshot.lobby_parameters, &UserIndex::Guest, &guest_hidden_ranks, salt_guest);
        if host_move.is_none() || guest_move.is_none() {
            std::println!("no more valid moves game ended");
            break; // No more valid moves
        }
        
        // Debug: Check if games have same state before move
        if move_number <= 30 {
            let insecure_loop_start_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_insecure);
            let mut differences = 0;
            for (pawn_id, (_, secure_pawn)) in secure_loop_start_snapshot.pawns_map.iter() {
                if let Some((_, insecure_pawn)) = insecure_loop_start_snapshot.pawns_map.get(pawn_id) {
                    if secure_pawn.pos != insecure_pawn.pos || secure_pawn.alive != insecure_pawn.alive {
                        std::println!("DIVERGENCE at move {} start: Pawn {} - secure pos {:?} alive {}, insecure pos {:?} alive {}", 
                            move_number, pawn_id, secure_pawn.pos, secure_pawn.alive, insecure_pawn.pos, insecure_pawn.alive);
                        differences += 1;
                    }
                }
            }
            if differences > 0 {
                std::println!("Found {} differences at start of move {}", differences, move_number);
            }
        }
        let host_move_proof = host_move.unwrap();
        let guest_move_proof = guest_move.unwrap();
        let host_move_full_hash = setup.env.crypto().sha256(&host_move_proof.clone().to_xdr(&setup.env)).to_bytes().to_array();
        let host_hash = HiddenMoveHash::from_array(&setup.env, &host_move_full_hash[0..16].try_into().unwrap());
        let guest_move_full_hash = setup.env.crypto().sha256(&guest_move_proof.clone().to_xdr(&setup.env)).to_bytes().to_array();
        let guest_hash = HiddenMoveHash::from_array(&setup.env, &guest_move_full_hash[0..16].try_into().unwrap());


        let host_moved_pawn_rank = host_hidden_ranks.iter().find(|item| item.pawn_id == host_move_proof.pawn_id).unwrap().rank;
        let guest_moved_pawn_rank = guest_hidden_ranks.iter().find(|item| item.pawn_id == guest_move_proof.pawn_id).unwrap().rank;
        std::println!("host will move pawnid {} from {:?} to {:?} with rank {}", 
            host_move_proof.pawn_id, 
            secure_loop_start_snapshot.pawns_map.get(host_move_proof.pawn_id).map(|(_, p)| p.pos),
            host_move_proof.target_pos, 
            host_moved_pawn_rank);
        std::println!("guest will move pawnid {} from {:?} to {:?} with rank {}", 
            guest_move_proof.pawn_id,
            secure_loop_start_snapshot.pawns_map.get(guest_move_proof.pawn_id).map(|(_, p)| p.pos),
            guest_move_proof.target_pos,
            guest_moved_pawn_rank);
        
        // Check what's at the target positions
        if move_number == 28 {
            for (id, (_, pawn)) in secure_loop_start_snapshot.pawns_map.iter() {
                if pawn.pos == host_move_proof.target_pos && pawn.alive {
                    std::println!("  At host target {:?}: pawn {} (rank unknown in secure)", pawn.pos, id);
                }
                if pawn.pos == guest_move_proof.target_pos && pawn.alive {
                    std::println!("  At guest target {:?}: pawn {} (rank unknown in secure)", pawn.pos, id);
                }
            }
        }
        
        assert!(![0, 11].contains(&host_moved_pawn_rank));
        assert!(![0, 11].contains(&guest_moved_pawn_rank));
        // do secure mode first
        {
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
                // need to provide rank proofs
                let mut host_hidden_ranks_temp = Vec::new(&setup.env);
                let mut host_merkle_proofs_temp = Vec::new(&setup.env);
                for pawn_id in host_simulation_result.needed_rank_proofs.iter() {
                    std::println!("host must prove PawnID: {}", pawn_id);
                    for (i, hidden_rank) in host_hidden_ranks.iter().enumerate() {
                        if hidden_rank.pawn_id == pawn_id {
                            host_hidden_ranks_temp.push_back(hidden_rank.clone());
                            host_merkle_proofs_temp.push_back(host_proofs.get_unchecked(i as u32));
                            std::println!("host found pawn PawnID: {} rank: {}", hidden_rank.pawn_id, hidden_rank.rank);
                        }
                    }
                }
                assert!(!host_hidden_ranks_temp.is_empty());
                assert!(!host_merkle_proofs_temp.is_empty());
                let host_secure_rank_proof_req = ProveRankReq {
                    hidden_ranks: host_hidden_ranks_temp,
                    lobby_id: lobby_secure,
                    merkle_proofs: host_merkle_proofs_temp,
                };
                std::println!("host prove_move_and_prove_rank");
                setup.client.prove_move_and_prove_rank(&host_secure, &host_secure_prove_move_req, &host_secure_rank_proof_req);
                let host_prove_move_and_prove_rank_after_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);
            } else {
                // submit just prove move if the simulation says you didn't collide
                std::println!("host prove_move");
                setup.client.prove_move(&host_secure, &host_secure_prove_move_req);
            }
            let host_prove_move_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);
            if host_prove_move_snapshot.lobby_info.phase == Phase::RankProve && host_prove_move_snapshot.lobby_info.subphase == Subphase::Guest {
                let mut guest_hidden_ranks_temp = Vec::new(&setup.env);
                let mut guest_merkle_proofs_temp = Vec::new(&setup.env);
                for pawn_id in host_prove_move_snapshot.game_state.moves.get_unchecked(UserIndex::Guest.u32()).needed_rank_proofs.iter() {
                    std::println!("guest must prove PawnID: {}", pawn_id);
                    for (i, hidden_rank) in guest_hidden_ranks.iter().enumerate() {
                        if hidden_rank.pawn_id == pawn_id {
                            guest_hidden_ranks_temp.push_back(hidden_rank.clone());
                            guest_merkle_proofs_temp.push_back(guest_proofs.get_unchecked(i as u32));
                            std::println!("guest found pawn PawnID: {} rank: {}", hidden_rank.pawn_id, hidden_rank.rank);
                        }
                    }
                }
                let guest_secure_rank_proof_req = ProveRankReq {
                    hidden_ranks: guest_hidden_ranks_temp,
                    lobby_id: lobby_secure,
                    merkle_proofs: guest_merkle_proofs_temp,
                };
                std::println!("guest prove_rank");
                setup.client.prove_rank(&guest_secure, &guest_secure_rank_proof_req);
            }
            let secure_turn_end_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);
            let secure_ended = secure_turn_end_snapshot.lobby_info.phase == Phase::Finished;
            if secure_ended {
                std::println!("secure game has ended at move {}", move_number);
            } else {
                assert!(secure_turn_end_snapshot.lobby_info.phase == Phase::MoveCommit && secure_turn_end_snapshot.lobby_info.subphase == Subphase::Both);
            }
        }
        {
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
                if secure_check.phase != Phase::Finished {
                    panic!("Games ended at different times! Secure: {:?}, Insecure: Finished", secure_check.phase);
                }
                break;
            }
            else {
                assert!(insecure_turn_end_snapshot.lobby_info.phase == Phase::MoveCommit && insecure_turn_end_snapshot.lobby_info.subphase == Subphase::Both);
            }
        }
        
        // Check for divergence at end of turn
        {
            let secure_end_turn = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);
            let insecure_end_turn = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_insecure);
            
            // Special check for move 28 to debug throne issue
            if move_number == 28 {
                std::println!("=== END OF MOVE 28 DEBUG ===");
                // Check throne status
                if let Some((_, secure_throne)) = secure_end_turn.pawns_map.get(193) {
                    std::println!("Secure throne (193): alive={}, rank={:?}, revealed={}", 
                        secure_throne.alive, secure_throne.rank, secure_throne.zz_revealed);
                }
                if let Some((_, insecure_throne)) = insecure_end_turn.pawns_map.get(193) {
                    std::println!("Insecure throne (193): alive={}, rank={:?}, revealed={}", 
                        insecure_throne.alive, insecure_throne.rank, insecure_throne.zz_revealed);
                }
            }
            
            for (pawn_id, (_, secure_pawn)) in secure_end_turn.pawns_map.iter() {
                if let Some((_, insecure_pawn)) = insecure_end_turn.pawns_map.get(pawn_id) {
                    if secure_pawn.pos != insecure_pawn.pos || secure_pawn.alive != insecure_pawn.alive {
                        std::println!("DIVERGENCE at end of move {}: Pawn {} - secure pos {:?} alive {}, insecure pos {:?} alive {}", 
                            move_number, pawn_id, secure_pawn.pos, secure_pawn.alive, insecure_pawn.pos, insecure_pawn.alive);
                        panic!("Games diverged!");
                    }
                }
            }
        }
    }
    let secure_end_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_secure);
    let insecure_end_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_insecure);

    // Both games should have the same outcome when given the same moves
    std::println!("Secure game phase: {:?}", secure_end_snapshot.lobby_info.phase);
    std::println!("Insecure game phase: {:?}", insecure_end_snapshot.lobby_info.phase);
    
    // Both games should be in the same phase
    assert_eq!(secure_end_snapshot.lobby_info.phase, insecure_end_snapshot.lobby_info.phase, 
        "Games should end in the same phase");
    
    if secure_end_snapshot.lobby_info.phase == Phase::Finished {
        // Check throne status in both games
        let secure_host_throne = secure_end_snapshot.pawns_map.get(0).map(|(_, p)| p.alive).unwrap_or(false);
        let secure_guest_throne = secure_end_snapshot.pawns_map.get(193).map(|(_, p)| p.alive).unwrap_or(false);
        let insecure_host_throne = insecure_end_snapshot.pawns_map.get(0).map(|(_, p)| p.alive).unwrap_or(false);
        let insecure_guest_throne = insecure_end_snapshot.pawns_map.get(193).map(|(_, p)| p.alive).unwrap_or(false);
        
        // Both games should have the same winner
        assert_eq!(secure_host_throne, insecure_host_throne, "Host throne status should match");
        assert_eq!(secure_guest_throne, insecure_guest_throne, "Guest throne status should match");
        
        std::println!("Both games ended with same result - Host throne alive: {}, Guest throne alive: {}", 
            secure_host_throne, secure_guest_throne);
    }
    
    // Verify all pawns in insecure mode have ranks
    for (pawn_id, (_, pawn)) in insecure_end_snapshot.pawns_map.iter() {
        assert!(!pawn.rank.is_empty(), "Insecure mode pawn {} should have rank", pawn_id);
    }
    
    // Verify matching ranks where applicable
    for (pawn_id, (_, secure_pawn)) in secure_end_snapshot.pawns_map.iter() {
        if let Some((_, insecure_pawn)) = insecure_end_snapshot.pawns_map.get(pawn_id.clone()) {
            // If revealed in security mode, ranks should match
            if secure_pawn.zz_revealed && !secure_pawn.rank.is_empty() && !insecure_pawn.rank.is_empty() {
                assert_eq!(secure_pawn.rank.get_unchecked(0), insecure_pawn.rank.get_unchecked(0), 
                    "Rank mismatch for revealed pawn {}", pawn_id);
            }
        }
    }
    
    std::println!("Security mode comparison test completed successfully. Both games had identical outcomes.");
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
        create_setup_commits_from_game_state(&setup.env, lobby_a, &UserIndex::Host)
    });

    let (guest_setup, guest_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_b, &UserIndex::Guest)
    });
    let (host_root, host_proofs) = get_merkel(&setup.env, &host_setup, &host_hidden_ranks);
    let (guest_root, guest_proofs) = get_merkel(&setup.env, &guest_setup, &guest_hidden_ranks);

    // Apply identical setups to both games
    for lobby_id in [lobby_a, lobby_b] {
        let (host_addr, guest_addr) = if lobby_id == lobby_a { (&host_a, &guest_a) } else { (&host_b, &guest_b) };

        setup.client.commit_setup(host_addr, &CommitSetupReq { lobby_id, rank_commitment_root: host_root.clone() , zz_hidden_ranks: Vec::new(&setup.env),});
        setup.client.commit_setup(guest_addr, &CommitSetupReq { lobby_id, rank_commitment_root: guest_root.clone(), zz_hidden_ranks: Vec::new(&setup.env),});
    }

    // Populate ranks only in Game B
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_b);
        let mut game_state: GameState = setup.env.storage().temporary().get(&game_state_key).expect("Game state should exist");
        for hidden_rank in host_hidden_ranks.iter().chain(guest_hidden_ranks.iter()) {
            for (index, packed_pawn) in game_state.pawns.iter().enumerate() {
                let mut pawn = Contract::unpack_pawn(&setup.env, packed_pawn);
                if pawn.pawn_id == hidden_rank.pawn_id {
                    pawn.rank = Vec::from_array(&setup.env, [hidden_rank.rank]);
                    let updated_packed = Contract::pack_pawn(pawn);
                    game_state.pawns.set(index as u32, updated_packed);
                    break;
                }
            }
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

        let host_move = generate_valid_move_req(&setup.env, &loop_start_snapshot.pawns_map, &loop_start_snapshot.lobby_parameters, &UserIndex::Host, &host_hidden_ranks, salt_host);
        let guest_move = generate_valid_move_req(&setup.env, &loop_start_snapshot.pawns_map, &loop_start_snapshot.lobby_parameters, &UserIndex::Guest, &guest_hidden_ranks, salt_guest);

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

// endregion
// endregion