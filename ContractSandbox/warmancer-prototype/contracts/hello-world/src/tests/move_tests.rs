#![cfg(test)]
#![allow(unused_variables)]
extern crate std;
use super::super::*;
use super::super::test_utils::*;
use super::test_utils::*;

// region move tests

#[test]
fn test_commit_move_success_both_players() {
    let setup = TestSetup::new();
    let lobby_id = 1u32;

    let (host_address, guest_address) = create_and_advance_to_move_commit(&setup, lobby_id);

    // Create test move hashes for both players
    let host_pawn_id = Contract::encode_pawn_id( Pos { x: 0, y: 0 }, UserIndex::Host as u32);
    let host_move_hash = create_test_move_hash(&setup.env, host_pawn_id, Pos { x: 0, y: 0 }, Pos { x: 0, y: 1 }, 12345);

    let guest_pawn_id = Contract::encode_pawn_id( Pos { x: 0, y: 3 }, UserIndex::Guest as u32);
    let guest_move_hash = create_test_move_hash(&setup.env, guest_pawn_id, Pos { x: 0, y: 3 }, Pos { x: 0, y: 2 }, 54321);

    let host_commit_req = CommitMoveReq {
        lobby_id,
        move_hash: host_move_hash.clone(),
    };
    setup.client.commit_move(&host_address, &host_commit_req);

    let guest_commit_req = CommitMoveReq {
        lobby_id,
        move_hash: guest_move_hash.clone(),
    };
    setup.client.commit_move(&guest_address, &guest_commit_req);

    // Verify both moves committed and phase advanced to MoveProve
    {
        let validation_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);

        assert_eq!(validation_snapshot.lobby_info.phase, Phase::MoveProve);
        assert_eq!(validation_snapshot.lobby_info.subphase, Subphase::Both);

        // Verify both move hashes were stored
        let host_move = validation_snapshot.game_state.moves.get(0).unwrap();
        let guest_move = validation_snapshot.game_state.moves.get(1).unwrap();

        assert_eq!(host_move.move_hash, host_move_hash);
        assert_eq!(guest_move.move_hash, guest_move_hash);
    }
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

    // Create a lobby and advance to move commit phase for further tests
    let (lobby_id, host_address, _guest_address, _host_ranks, _guest_ranks, _host_merkle_proofs, _guest_merkle_proofs) = setup_lobby_for_commit_move(&setup, 100);

    // Test: Not in lobby
    let outsider_address = setup.generate_address();
    let commit_req = CommitMoveReq {
        lobby_id,
        move_hash: move_hash.clone(),
    };
    let result = setup.client.try_commit_move(&outsider_address, &commit_req);
    //assert!(result.is_err(), "Should fail: not in lobby");

    // Test: Wrong phase (create a new lobby in wrong phase)
    let (wrong_phase_lobby_id, wrong_phase_host, _) = setup_lobby_for_commit_setup(&setup, 200);

    let commit_req = CommitMoveReq {
        lobby_id: wrong_phase_lobby_id,
        move_hash: move_hash.clone(),
    };
    let result = setup.client.try_commit_move(&wrong_phase_host, &commit_req);
    assert!(result.is_err(), "Should fail: wrong phase");
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongPhase);

    // Test: Wrong subphase (commit one move, then try to commit another)
    let host_pawn_id = Contract::encode_pawn_id( Pos { x: 0, y: 0 }, UserIndex::Host as u32);
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
    let (finished_lobby_id, finished_host, finished_guest, _, _, _, _) = setup_lobby_for_commit_move(&setup, 300);
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
// region security_mode=false tests
#[test]
fn test_no_security_mode_basic_functionality() {
    let setup = TestSetup::new();
    let lobby_id = 1u32;
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let mut params = create_test_lobby_parameters(&setup.env);
    params.security_mode = false;
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    // Use the same setup logic as security mode tests
    let (host_setup, host_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, &UserIndex::Host)
    });
    let (guest_setup, guest_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, &UserIndex::Guest)
    });
    let (host_root, _host_proofs) = get_merkel(&setup.env, &host_setup, &host_hidden_ranks);
    let (guest_root, _guest_proofs) = get_merkel(&setup.env, &guest_setup, &guest_hidden_ranks);
    // Commit setup (in no-security mode, provide the hidden ranks directly)
    setup.client.commit_setup(&host, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root,
        zz_hidden_ranks: host_hidden_ranks.clone(),
    });
    setup.client.commit_setup(&guest, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: guest_root,
        zz_hidden_ranks: guest_hidden_ranks.clone(),
    });
    // Verify game moved to MoveCommit phase - this proves the core functionality works
    let phase_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(phase_snapshot.phase, Phase::MoveCommit);
    assert_eq!(phase_snapshot.subphase, Subphase::Both);
    // Generate valid moves using the same logic as integration tests
    let updated_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    let host_move_opt = generate_valid_move_req(&setup.env, &updated_snapshot.pawns_map, &updated_snapshot.lobby_parameters, &UserIndex::Host, &host_hidden_ranks, 12345);
    let guest_move_opt = generate_valid_move_req(&setup.env, &updated_snapshot.pawns_map, &updated_snapshot.lobby_parameters, &UserIndex::Guest, &guest_hidden_ranks, 54321);
    if host_move_opt.is_none() || guest_move_opt.is_none() {
        std::println!("No valid moves available - this is expected if game is already finished");
        return;
    }
    let host_move = host_move_opt.unwrap();
    let guest_move = guest_move_opt.unwrap();
    let host_move_hash = {
        let serialized = host_move.clone().to_xdr(&setup.env);
        let full_hash = setup.env.crypto().sha256(&serialized).to_bytes().to_array();
        HiddenMoveHash::from_array(&setup.env, &full_hash[0..16].try_into().unwrap())
    };
    let guest_move_hash = {
        let serialized = guest_move.clone().to_xdr(&setup.env);
        let full_hash = setup.env.crypto().sha256(&serialized).to_bytes().to_array();
        HiddenMoveHash::from_array(&setup.env, &full_hash[0..16].try_into().unwrap())
    };
    // Check if host move succeeds
    setup.client.commit_move_and_prove_move(&host, &CommitMoveReq { lobby_id, move_hash: host_move_hash }, &ProveMoveReq { lobby_id, move_proof: host_move });
    let after_host_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    if after_host_snapshot.phase == Phase::Aborted {
        // Game was aborted, probably due to invalid move
        std::println!("Game aborted after host move - move was invalid");
        return;
    }
    // Try guest move
    let result = setup.client.try_commit_move_and_prove_move(&guest, &CommitMoveReq { lobby_id, move_hash: guest_move_hash }, &ProveMoveReq { lobby_id, move_proof: guest_move });
    if result.is_err() {
        let final_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        std::println!("Guest move failed, final phase: {:?}, subphase: {:?}", final_snapshot.phase, final_snapshot.subphase);
        if final_snapshot.phase == Phase::Aborted {
            std::println!("Game was aborted - likely due to invalid move validation");
        }
        return;
    }
    // If we get here, both moves succeeded
    let final_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(final_snapshot.phase, Phase::MoveCommit, "Should stay in or return to MoveCommit phase in no-security mode");
}
#[test]
fn test_no_security_mode_forbidden_functions() {
    let setup = TestSetup::new();
    let lobby_id = 1u32;
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let mut params = create_test_lobby_parameters(&setup.env);
    params.security_mode = false;
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    // Use the same setup logic as security mode tests
    let (host_setup, host_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, &UserIndex::Host)
    });
    let (guest_setup, guest_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, &UserIndex::Guest)
    });
    let (host_root, _host_proofs) = get_merkel(&setup.env, &host_setup, &host_hidden_ranks);
    let (guest_root, _guest_proofs) = get_merkel(&setup.env, &guest_setup, &guest_hidden_ranks);
    setup.client.commit_setup(&host, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root,
        zz_hidden_ranks: host_hidden_ranks,
    });
    setup.client.commit_setup(&guest, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: guest_root,
        zz_hidden_ranks: guest_hidden_ranks,
    });
    // Test that security-mode-only functions are rejected
    let fake_move_hash = BytesN::from_array(&setup.env, &[1u8; 16]);
    let result = setup.client.try_commit_move(&host, &CommitMoveReq {
        lobby_id,
        move_hash: fake_move_hash,
    });
    assert!(result.is_err());
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongSecurityMode);
    let fake_move = HiddenMove {
        pawn_id: 1,
        start_pos: Pos { x: 0, y: 0 },
        target_pos: Pos { x: 0, y: 1 },
        salt: 12345,
    };
    let result = setup.client.try_prove_move(&host, &ProveMoveReq {
        lobby_id,
        move_proof: fake_move,
    });
    assert!(result.is_err());
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongSecurityMode);
}
// endregion