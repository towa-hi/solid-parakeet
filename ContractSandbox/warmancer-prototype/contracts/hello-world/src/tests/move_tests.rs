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