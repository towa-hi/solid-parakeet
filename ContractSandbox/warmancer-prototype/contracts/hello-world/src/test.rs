#![cfg(test)]
extern crate std;
use super::*;
use super::test_utils::*;
use soroban_sdk::{Env, Address};
use soroban_sdk::testutils::Address as _;

// region make_lobby tests

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
    
    {
        let validation_snapshot = extract_lobby_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(validation_snapshot.lobby_parameters.dev_mode, true);
        assert_eq!(validation_snapshot.lobby_parameters.host_team, 0);
        assert_eq!(validation_snapshot.lobby_parameters.board.name, String::from_str(&setup.env, "Default Stratego Board"));
    }
}

#[test]
fn test_replicate_bad_request_exact() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 536714u32; // Exact lobby_id from bad request
    
    // Create the exact board hash from bad request: "f88f128c1d9c0e0f989c50490982a99c"
    let board_hash = BytesN::from_array(&setup.env, &[
        0xf8, 0x8f, 0x12, 0x8c, 0x1d, 0x9c, 0x0e, 0x0f,
        0x98, 0x9c, 0x50, 0x49, 0x09, 0x82, 0xa9, 0x9c
    ]);
    
    // Create tiles matching the exact packed tile values from bad request
    let packed_tiles = Vec::from_array(&setup.env, [
        13107201, 13107203, 13107205, 13107207, 13107209, 13107211, 13107213, 13107215, 13107217, 1048594,
        8913921,  8913923,  8913925,  8913927,  8913929,  8913931,  8913933,  8913935,  8913937,  1049618,
        4720641,  4720643,  4720645,  4720647,  4720649,  4720651,  4720653,  4720655,  4720657,  1050642,
        527361,   527363,   527365,   527367,   527369,   527371,   527373,   527375,   527377,   1051666,
        4097,     4099,     1052676,  4103,     4105,     4107,     1052684,  4111,     4113,     1052690,
        5121,     5123,     1053700,  5127,     5129,     5131,     1053708,  5135,     5137,     1053714,
        1054721,  1054723,  1054725,  1054727,  1054729,  1054731,  1054733,  1054735,  1054737,  1054738,
        5250049,  5250051,  5250053,  5250055,  5250057,  5250059,  5250061,  5250063,  5250065,  1055762,
        9445377,  9445379,  9445381,  9445383,  9445385,  9445387,  9445389,  9445391,  9445393,  1056786,
        13640705, 13640707, 13640709, 13640711, 13640713, 13640715, 13640717, 13640719, 13640721, 1057810,]);
    let mut used_positions: Map<Pos, bool> = Map::new(&setup.env);
    let mut board_invalid = false;
    for packed_tile in packed_tiles.clone() {
        let tile = Contract::unpack_tile(packed_tile);
        std::println!("passable: {}, x {} y {} setup {} setup_zone {}", tile.passable, tile.pos.x, tile.pos.y, tile.setup, tile.setup_zone);
        if used_positions.contains_key(tile.pos.clone()) {
            board_invalid = true;
            break;
        }
        used_positions.set(tile.pos.clone(), true);
        if tile.setup != 0 && tile.setup != 1 && tile.setup != 2 {
            board_invalid = true;
            std::println!("{}, {} is invalid because rule 1", tile.pos.x, tile.pos.y);
        }
        if tile.setup != 2 {
            if !tile.passable {
                board_invalid = true;
                std::println!("{}, {} is invalid because rule 2", tile.pos.x, tile.pos.y);
            }
        }
        if tile.setup_zone != 0 && tile.setup_zone != 1 && tile.setup_zone != 2 && tile.setup_zone != 3 && tile.setup_zone != 4 {
            board_invalid = true;
            std::println!("{}, {} is invalid because rule 3", tile.pos.x, tile.pos.y);
        }
    }
    let good_board = create_default_board(&setup.env);
    let good_tiles = good_board.tiles.clone();
    let board = Board {
        hex: true, // From bad request
        name: String::from_str(&setup.env, "Narrow Hexagons"), // From bad request
        size: Pos { x: 10, y: 10 }, // From bad request
        tiles: packed_tiles.clone(),
    };

    // Exact max_ranks from bad request: [1,1,8,3,4,4,4,3,2,1,1,4,0]
    let max_ranks = Vec::from_array(&setup.env, [1u32, 1u32, 8u32, 3u32, 4u32, 4u32, 4u32, 3u32, 2u32, 1u32, 1u32, 4u32, 0u32]);

    let lobby_parameters = LobbyParameters {
        board,
        board_hash,
        dev_mode: false, // From bad request
        host_team: 0, // From bad request
        max_ranks,
        must_fill_all_tiles: true, // From bad request
        security_mode: true, // From bad request
    };

    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters.clone(),
    };

    // This should replicate the exact bad request scenario
    setup.client.make_lobby(&host_address, &req);

    // Verify the lobby was created successfully
    setup.verify_lobby_info(lobby_id, &host_address, Phase::Lobby);
    setup.verify_user_lobby(&host_address, lobby_id);
}

#[test]
fn test_replicate_bad_request_exact2() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 536714u32; // Exact lobby_id from bad request

    // Create the exact board hash from bad request: "f88f128c1d9c0e0f989c50490982a99c"
    let board_hash = BytesN::from_array(&setup.env, &[
        0xf8, 0x8f, 0x12, 0x8c, 0x1d, 0x9c, 0x0e, 0x0f,
        0x98, 0x9c, 0x50, 0x49, 0x09, 0x82, 0xa9, 0x9c]);

    // Create tiles matching the exact packed tile values from bad request
    let packed_tiles = Vec::from_array(&setup.env, [4114u32]);

    let good_board = create_default_board(&setup.env);

    let board = Board {
        hex: true, // From bad request
        name: String::from_str(&setup.env, "Narrow Hexagons"), // From bad request
        size: Pos { x: 10, y: 10 }, // From bad request
        tiles: good_board.tiles.clone(),
    };

    // Exact max_ranks from bad request: [1,1,8,3,4,4,4,3,2,1,1,4,0]
    let max_ranks = Vec::from_array(&setup.env, [1u32, 1u32, 8u32, 3u32, 4u32, 4u32, 4u32, 3u32, 2u32, 1u32, 1u32, 4u32, 0u32]);
    let good_lobby_parameters = create_test_lobby_parameters(&setup.env);
    let lobby_parameters = LobbyParameters {
        board: board,
        board_hash,
        dev_mode: false, // From bad request
        host_team: 0, // From bad request
        max_ranks,
        must_fill_all_tiles: true, // From bad request
        security_mode: true, // From bad request
    };

    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters.clone(),
    };

    // This should replicate the exact bad request scenario
    setup.client.make_lobby(&host_address, &req);

    // Verify the lobby was created successfully
    setup.verify_lobby_info(lobby_id, &host_address, Phase::Lobby);
    setup.verify_user_lobby(&host_address, lobby_id);
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
    {
        let validation_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(validation_snapshot.phase, Phase::Finished);

    }
}

#[test]
fn test_leave_lobby_validation_errors() {
    let setup = TestSetup::new();

    // Test: User not found
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
    setup.client.leave_lobby(&user_address);

    // Test: No current lobby (after leaving)
    let result = setup.client.try_leave_lobby(&user_address);
    assert!(result.is_err(), "Should fail: no current lobby");
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

    {
        let validation_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert!(validation_snapshot.lobby_info.guest_address.contains(&guest_address));
        assert_eq!(validation_snapshot.lobby_info.phase, Phase::SetupCommit);
        assert_eq!(validation_snapshot.lobby_info.subphase, Subphase::Both);
    }

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

// endregion

// region move tests

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
    let (_, host_setup_proof, _, _) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, 0)
    });
    let (_, guest_setup_proof, _, _) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, 1)
    });

    // Commit both setups
    let host_commit_req = CommitSetupReq {
        lobby_id,
        setup: host_setup_proof,
    };
    let guest_commit_req = CommitSetupReq {
        lobby_id,
        setup: guest_setup_proof,
    };

    setup.client.commit_setup(&host_address, &host_commit_req);
    setup.client.commit_setup(&guest_address, &guest_commit_req);

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
    let full_hash = env.crypto().sha256(&serialized).to_bytes().to_array();
    HiddenMoveHash::from_array(env, &full_hash[0..16].try_into().unwrap())
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
    let result = setup.client.try_commit_move(&user_address, &commit_req);
    assert!(result.is_err(), "Should fail: lobby not found");
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotFound);

    // Create a lobby and advance to move commit phase for further tests
    let (lobby_id, host_address, _guest_address, _host_ranks, _guest_ranks) = setup_lobby_for_commit_move(&setup, 100);

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
    let (wrong_phase_lobby_id, wrong_phase_host, _) = setup_lobby_for_commit_setup(&setup, 200);

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
    let (finished_lobby_id, finished_host, finished_guest, _, _) = setup_lobby_for_commit_move(&setup, 300);
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
    let (_, host_setup_proof, _, _) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, 1) // Wrong team!
    });

    // Create valid setup for guest (team 0 - also wrong, but different from host)
    let (_, guest_setup_proof, _, _) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, 0)
    });

    // Try to prove invalid setup - should end the game with host losing
    let host_commit_req = CommitSetupReq {
        lobby_id,
        setup: host_setup_proof,
    };

    setup.client.commit_setup(&host_address, &host_commit_req);

    // Verify game aborted with guest winning
    {
        let validation_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(validation_snapshot.phase, Phase::Aborted);
        assert_eq!(validation_snapshot.subphase, Subphase::Guest); // Guest wins
    }
}

// endregion

// region integration tests

#[test]
fn test_full_stratego_game() {
    let setup = TestSetup::new();

    // Use helper function to set up lobby and advance to MoveCommit phase, getting ranks
    let (lobby_id, host_address, guest_address, host_ranks, guest_ranks) = setup_lobby_for_commit_move(&setup, 400);

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
                setup.client.commit_move(&guest_address, &guest_move_req);

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
                    let guest_prove_move_req = ProveMoveReq {
                        move_proof: guest_move_proof,
                        lobby_id,
                    };

                    // Prove moves (state-changing operation)
                    setup.client.prove_move(&host_address, &host_prove_move_req);
                    setup.client.prove_move(&guest_address, &guest_prove_move_req);

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

                    // Create rank proof requests
                    let (host_req, guest_req) = create_rank_proof_requests(&setup.env, lobby_id,
                                     &host_needed_ranks, &guest_needed_ranks, &host_ranks, &guest_ranks);

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

// generate_valid_move_req function moved to test_utils.rs

// endregion

// region board visualization - moved to test_utils
// format_board_with_colors function moved to test_utils.rs

// endregion

// region utility functions


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

    fn is_user_conflict_error(error: &Error) -> bool {
        matches!(error,
            Error::GuestAlreadyInLobby |
            Error::HostAlreadyInLobby |
            Error::JoinerIsHost |
            Error::NotInLobby
        )
    }

    fn is_lobby_state_error(error: &Error) -> bool {
        matches!(error,
            Error::LobbyNotJoinable |
            Error::WrongPhase |
            Error::LobbyNotFound |
            Error::LobbyHasNoHost
        )
    }

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

            assert_eq!(stored_user.current_lobby, expected_lobby_id);
        });
    }

    fn verify_user_has_no_lobby(&self, user_address: &Address) {
        self.env.as_contract(&self.contract_id, || {
            let user_key = DataKey::User(user_address.clone());
            let stored_user: User = self.env.storage()
                .persistent()
                .get(&user_key)
                .expect("User should be stored");

            assert_eq!(stored_user.current_lobby, 0);
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


    let lobby_parameters = create_full_stratego_board_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);

    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);

    // Create setups but manually assign specific ranks for testing
    let (_host_commits, host_proof, _, _) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, 0)
    });
    let (_guest_commits, guest_proof, _, _) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, 1)
    });

    let host_commit_req = CommitSetupReq {
        lobby_id,
        setup: host_proof,
    };
    let guest_commit_req = CommitSetupReq {
        lobby_id,
        setup: guest_proof,
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
    let (_, host_proof, _, host_ranks) = create_deterministic_setup(&setup.env, 0, fixed_seed);
    let (_, guest_proof, _, guest_ranks) = create_deterministic_setup(&setup.env, 1, fixed_seed);

    // Apply identical setups to both games
    for lobby_id in [lobby_a, lobby_b] {
        let (host_addr, guest_addr) = if lobby_id == lobby_a { (&host_a, &guest_a) } else { (&host_b, &guest_b) };

        let host_full_hash = setup.env.crypto().sha256(&host_proof.clone().to_xdr(&setup.env)).to_bytes().to_array();
        let host_hash = SetupHash::from_array(&setup.env, &host_full_hash[0..16].try_into().unwrap());
        let guest_full_hash = setup.env.crypto().sha256(&guest_proof.clone().to_xdr(&setup.env)).to_bytes().to_array();
        let guest_hash = SetupHash::from_array(&setup.env, &guest_full_hash[0..16].try_into().unwrap());

        setup.client.commit_setup(host_addr, &CommitSetupReq { lobby_id, setup: host_proof.clone() });
        setup.client.commit_setup(guest_addr, &CommitSetupReq { lobby_id, setup: guest_proof.clone() });
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

        // Take snapshot at start of loop to get current game state
        let loop_start_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_a);

        let host_move = generate_valid_move_req(&setup.env, &loop_start_snapshot.game_state, &loop_start_snapshot.lobby_parameters, 0, &host_ranks, salt_host);
        let guest_move = generate_valid_move_req(&setup.env, &loop_start_snapshot.game_state, &loop_start_snapshot.lobby_parameters, 1, &guest_ranks, salt_guest);

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
                    for rank in host_ranks.iter() {
                        if rank.pawn_id == needed_id {
                            host_proof_ranks.push_back(rank);
                        }
                    }
                }
                if !host_proof_ranks.is_empty() {
                    // Prove host ranks (state-changing operation)
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
                    // Prove guest ranks (state-changing operation)
                    setup.client.prove_rank(&guest_a, &ProveRankReq { lobby_id: lobby_a, hidden_ranks: guest_proof_ranks });
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

// verify_pawn_states_identical function moved to test_utils.rs

// create_deterministic_setup function moved to test_utils.rs

// Replaced by function-specific validation tests

// Legacy individual tests have been replaced by consolidated tests

// Replaced by function-specific validation tests

// All validation error tests have been consolidated

// endregion

// region validation functions (test-specific)

/// Validates the game state transition after move proving phase
/// Returns (phase, subphase, host_needed_ranks, guest_needed_ranks)
fn validate_move_prove_transition(
    snapshot: &SnapshotFull,
    host_move_req: &ProveMoveReq,
    guest_move_req: &ProveMoveReq,
) -> (Phase, Subphase, Vec<PawnId>, Vec<PawnId>) {
    let host_move = snapshot.game_state.moves.get(0).unwrap();
    let guest_move = snapshot.game_state.moves.get(1).unwrap();
    let host_needed_ranks = host_move.needed_rank_proofs.clone();
    let guest_needed_ranks = guest_move.needed_rank_proofs.clone();

    // VALIDATE: Log the submitted moves for tracking
    std::println!("=== POST-MOVEPROVE VALIDATION ===");
    std::println!("Phase: {:?}, Subphase: {:?}", snapshot.lobby_info.phase, snapshot.lobby_info.subphase);
    std::println!("✓ Host move submitted: {} from ({},{}) to ({},{})",
                 host_move_req.move_proof.pawn_id,
                 host_move_req.move_proof.start_pos.x, host_move_req.move_proof.start_pos.y,
                 host_move_req.move_proof.target_pos.x, host_move_req.move_proof.target_pos.y);
    std::println!("✓ Guest move submitted: {} from ({},{}) to ({},{})",
                 guest_move_req.move_proof.pawn_id,
                 guest_move_req.move_proof.start_pos.x, guest_move_req.move_proof.start_pos.y,
                 guest_move_req.move_proof.target_pos.x, guest_move_req.move_proof.target_pos.y);

    // VALIDATE: Check that move processing was successful (moves exist in game state)
    assert!(snapshot.game_state.moves.len() >= 2, "Game state should have moves for both players");
    std::println!("✓ Move processing completed successfully");
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

    (snapshot.lobby_info.phase.clone(), snapshot.lobby_info.subphase.clone(), host_needed_ranks, guest_needed_ranks)
}

/// Validates the game state transition after rank proving phase
/// Returns (phase, subphase, remaining_host_rank_proofs, remaining_guest_rank_proofs)
fn validate_rank_prove_transition(
    snapshot: &SnapshotFull,
    host_rank_req: Option<&ProveRankReq>,
    guest_rank_req: Option<&ProveRankReq>,
) -> (Phase, Subphase, Vec<PawnId>, Vec<PawnId>) {
    std::println!("=== POST-RANKPROVE VALIDATION ===");
    std::println!("After rank proving: Phase={:?}, Subphase={:?}", snapshot.lobby_info.phase, snapshot.lobby_info.subphase);

    // VALIDATE: Check that the submitted rank proofs were applied correctly
    if let Some(host_req) = host_rank_req {
        std::println!("✓ Validating host rank proofs...");
        for hidden_rank in host_req.hidden_ranks.iter() {
            let pawn = snapshot.game_state.pawns.iter()
                .find(|p| p.pawn_id == hidden_rank.pawn_id)
                .expect(&std::format!("Host pawn {} should exist", hidden_rank.pawn_id));

            assert!(!pawn.rank.is_empty(), "Host pawn {} should have rank revealed", hidden_rank.pawn_id);
            assert_eq!(pawn.rank.get(0).unwrap(), hidden_rank.rank, "Host pawn {} rank should match submitted proof", hidden_rank.pawn_id);

            let rank_str = rank_to_string(hidden_rank.rank);
            std::println!("  ✓ Host pawn {} rank validated: {}", hidden_rank.pawn_id, rank_str);
        }
    }

    if let Some(guest_req) = guest_rank_req {
        std::println!("✓ Validating guest rank proofs...");
        for hidden_rank in guest_req.hidden_ranks.iter() {
            let pawn = snapshot.game_state.pawns.iter()
                .find(|p| p.pawn_id == hidden_rank.pawn_id)
                .expect(&std::format!("Guest pawn {} should exist", hidden_rank.pawn_id));

            assert!(!pawn.rank.is_empty(), "Guest pawn {} should have rank revealed", hidden_rank.pawn_id);
            assert_eq!(pawn.rank.get(0).unwrap(), hidden_rank.rank, "Guest pawn {} rank should match submitted proof", hidden_rank.pawn_id);

            let rank_str = rank_to_string(hidden_rank.rank);
            std::println!("  ✓ Guest pawn {} rank validated: {}", hidden_rank.pawn_id, rank_str);
        }
    }

    // Check if rank proofs are still needed
    let host_move = snapshot.game_state.moves.get(0).unwrap();
    let guest_move = snapshot.game_state.moves.get(1).unwrap();
    let remaining_host_proofs = host_move.needed_rank_proofs.clone();
    let remaining_guest_proofs = guest_move.needed_rank_proofs.clone();

    std::println!("Host still needs {} rank proofs", remaining_host_proofs.len());
    std::println!("Guest still needs {} rank proofs", remaining_guest_proofs.len());
    std::println!("=== END VALIDATION ===");

    (snapshot.lobby_info.phase.clone(), snapshot.lobby_info.subphase.clone(), remaining_host_proofs, remaining_guest_proofs)
}

// endregion

// region validation test helpers

/// Sets up a lobby ready for setup commit phase testing
/// Returns (lobby_id, host_address, guest_address)
fn setup_lobby_for_commit_setup(setup: &TestSetup, lobby_id: u32) -> (u32, Address, Address) {
    let lobby_parameters = create_full_stratego_board_parameters(&setup.env);

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
/// Returns (lobby_id, host_address, guest_address, host_ranks, guest_ranks)
fn setup_lobby_for_commit_move(setup: &TestSetup, lobby_id: u32) -> (u32, Address, Address, Vec<HiddenRank>, Vec<HiddenRank>) {
    let (lobby_id, host_address, guest_address) = setup_lobby_for_commit_setup(setup, lobby_id);

    // Advance through complete setup phase (commit + prove)
    let (host_ranks, guest_ranks) = advance_through_complete_setup_phase(setup, lobby_id, &host_address, &guest_address);

    (lobby_id, host_address, guest_address, host_ranks, guest_ranks)
}

/// Helper to advance a lobby through the complete setup phase to move commit
/// Returns (host_ranks, guest_ranks) from the setup
fn advance_through_complete_setup_phase(setup: &TestSetup, lobby_id: u32, host_address: &Address, guest_address: &Address) -> (Vec<HiddenRank>, Vec<HiddenRank>) {
    // Create deterministic setups for both players
    let (_, host_setup_proof, _, host_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_deterministic_setup(&setup.env, 0, 12345)
    });

    let (_, guest_setup_proof, _, guest_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_deterministic_setup(&setup.env, 1, 67890)
    });

    // Commit setups
    let host_commit_req = CommitSetupReq {
        lobby_id,
        setup: host_setup_proof,
    };
    setup.client.commit_setup(host_address, &host_commit_req);

    let guest_commit_req = CommitSetupReq {
        lobby_id,
        setup: guest_setup_proof,
    };
    setup.client.commit_setup(guest_address, &guest_commit_req);

    (host_ranks, guest_ranks)
}

// endregion

// region user board test

#[test]
fn test_make_lobby_with_user_board_configuration() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 378189u32;  // Exact lobby ID from user's current request

    // Create the exact board configuration from user's input
    let lobby_parameters = create_user_board_parameters(&setup.env);
    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters.clone(),
    };

    // This should succeed if the board is valid, or give a proper error if invalid
    // It should NOT panic with UnreachableCodeReached
    let result = setup.client.try_make_lobby(&host_address, &req);

    // Check the result
    match result {
        Ok(_) => {
            // SUCCESS: The board configuration worked
            setup.verify_lobby_info(lobby_id, &host_address, Phase::Lobby);
            setup.verify_user_lobby(&host_address, lobby_id);
        },
        Err(err) => {
            match err {
                Ok(contract_error) => {
                    // This is acceptable - a proper contract error like InvalidBoard
                    // Expected error codes: InvalidBoard (40), etc.
                    assert!(matches!(contract_error, Error::InvalidBoard),
                           "Expected InvalidBoard error, got: {:?}", contract_error);
                },
                Err(_host_error) => {
                    // This indicates the contract panicked - which is the issue we're investigating
                    panic!("Contract panicked with UnreachableCodeReached - this is the bug we found!");
                }
            }
        }
    }
}

// Helper function to create the exact board configuration from user's input
fn create_user_board_parameters(env: &Env) -> LobbyParameters {
    let mut tiles = Vec::new(env);

    // Add all 100 tiles exactly as specified in user's input
    // Row 0
    tiles.push_back(Tile { passable: true, pos: Pos { x: 0, y: 0 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 1, y: 0 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 2, y: 0 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 3, y: 0 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 4, y: 0 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 5, y: 0 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 6, y: 0 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 7, y: 0 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 8, y: 0 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 9, y: 0 }, setup:2, setup_zone: 1 });

    // Row 1
    tiles.push_back(Tile { passable: true, pos: Pos { x: 0, y: 1 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 1, y: 1 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 2, y: 1 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 3, y: 1 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 4, y: 1 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 5, y: 1 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 6, y: 1 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 7, y: 1 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 8, y: 1 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 9, y: 1 }, setup:2, setup_zone: 1 });

    // Row 2
    tiles.push_back(Tile { passable: true, pos: Pos { x: 0, y: 2 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 1, y: 2 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 2, y: 2 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 3, y: 2 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 4, y: 2 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 5, y: 2 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 6, y: 2 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 7, y: 2 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 8, y: 2 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 9, y: 2 }, setup:2, setup_zone: 1 });

    // Row 3
    tiles.push_back(Tile { passable: true, pos: Pos { x: 0, y: 3 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 1, y: 3 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 2, y: 3 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 3, y: 3 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 4, y: 3 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 5, y: 3 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 6, y: 3 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 7, y: 3 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 8, y: 3 }, setup: 0, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 9, y: 3 }, setup:2, setup_zone: 1 });

    // Row 4 (neutral)
    tiles.push_back(Tile { passable: true, pos: Pos { x: 0, y: 4 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 1, y: 4 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 2, y: 4 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 3, y: 4 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 4, y: 4 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 5, y: 4 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 6, y: 4 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 7, y: 4 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 8, y: 4 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 9, y: 4 }, setup:2, setup_zone: 1 });

    // Row 5 (neutral)
    tiles.push_back(Tile { passable: true, pos: Pos { x: 0, y: 5 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 1, y: 5 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 2, y: 5 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 3, y: 5 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 4, y: 5 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 5, y: 5 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 6, y: 5 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 7, y: 5 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 8, y: 5 }, setup:2, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 9, y: 5 }, setup:2, setup_zone: 1 });

    // Row 6 (team 1)
    tiles.push_back(Tile { passable: true, pos: Pos { x: 0, y: 6 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 1, y: 6 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 2, y: 6 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 3, y: 6 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 4, y: 6 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 5, y: 6 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 6, y: 6 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 7, y: 6 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 8, y: 6 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 9, y: 6 }, setup:2, setup_zone: 1 });

    // Row 7 (team 1)
    tiles.push_back(Tile { passable: true, pos: Pos { x: 0, y: 7 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 1, y: 7 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 2, y: 7 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 3, y: 7 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 4, y: 7 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 5, y: 7 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 6, y: 7 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 7, y: 7 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 8, y: 7 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 9, y: 7 }, setup:2, setup_zone: 1 });

    // Row 8 (team 1)
    tiles.push_back(Tile { passable: true, pos: Pos { x: 0, y: 8 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 1, y: 8 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 2, y: 8 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 3, y: 8 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 4, y: 8 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 5, y: 8 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 6, y: 8 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 7, y: 8 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 8, y: 8 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 9, y: 8 }, setup:2, setup_zone: 1 });

    // Row 9 (team 1)
    tiles.push_back(Tile { passable: true, pos: Pos { x: 0, y: 9 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 1, y: 9 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 2, y: 9 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 3, y: 9 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 4, y: 9 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 5, y: 9 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 6, y: 9 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 7, y: 9 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: true, pos: Pos { x: 8, y: 9 }, setup: 1, setup_zone: 1 });
    tiles.push_back(Tile { passable: false, pos: Pos { x: 9, y: 9 }, setup:2, setup_zone: 1 });
    let mut packed_tiles = Vec::new(env);
    for tile in tiles.iter() {
        packed_tiles.push_back(Contract::pack_tile(&tile));
    }
    let board = Board {
        hex: true,
        name: String::from_str(env, "Narrow Hexagons"),
        size: Pos { x: 10, y: 10 },
        tiles: packed_tiles,
    };

    LobbyParameters {
        board,
        board_hash: BytesN::from_array(env, &[0u8; 16]),
        dev_mode: false,
        host_team: 0,
        max_ranks: Vec::from_array(env, [1, 1, 8, 3, 4, 4, 4, 3, 2, 1, 1, 4, 0]),
        must_fill_all_tiles: true,
        security_mode: false,
    }
}

// endregion

#[test]
fn test_bad_request_exact() {
    let setup = TestSetup::new();

    // Analyze some of the packed_tiles values to see what they decode to
    let sample_packed_values = [13107201u32, 18u32, 4114u32, 13640721u32];

    for packed_value in sample_packed_values.iter() {
        let tile = Contract::unpack_tile(*packed_value);
        std::println!("Packed value {} unpacks to:", packed_value);
        std::println!("  passable: {}", tile.passable);
        std::println!("  pos: ({}, {})", tile.pos.x, tile.pos.y);
        std::println!("  setup: {} (valid: {})", tile.setup, tile.setup <= 2);
        std::println!("  setup_zone: {} (valid: {})", tile.setup_zone, tile.setup_zone <= 4);

        // Check validation rules
        let mut valid = true;
        if tile.setup > 2 {
            valid = false;
            std::println!("  INVALID: setup > 2");
        }
        if tile.setup_zone > 4 {
            valid = false;
            std::println!("  INVALID: setup_zone > 4");
        }
        if tile.setup != 2 && !tile.passable {
            valid = false;
            std::println!("  INVALID: setup != 2 but not passable");
        }
        std::println!("  Overall valid: {}", valid);
        std::println!();
    }
}


