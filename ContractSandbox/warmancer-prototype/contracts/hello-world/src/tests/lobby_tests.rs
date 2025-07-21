#![cfg(test)]
#![allow(unused_variables)]
extern crate std;
use super::super::*;
use super::super::test_utils::*;
use super::test_utils::*;

// region make_lobby tests

#[test]
fn test_make_lobby_success() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let lobby_id = 1u32;
    
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq {
        lobby_id,
        parameters: params,
    });
    
    // Verify lobby state and user association
    setup.verify_lobby_info(lobby_id, &host, Phase::Lobby);
    setup.verify_user_lobby(&host, lobby_id);
    
    // Verify lobby is waiting for guest
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.subphase, Subphase::Guest, "Waiting for guest to join");
    }
}

#[test]
fn test_lobby_id_collision() {
    let setup = TestSetup::new();
    let lobby_parameters = create_test_lobby_parameters(&setup.env);

    // Create first lobby
    let host_1 = setup.generate_address();
    setup.client.make_lobby(&host_1, &MakeLobbyReq {
        lobby_id: 1,
        parameters: lobby_parameters.clone(),
    });

    // Attempt to create lobby with same ID should fail
    let host_2 = setup.generate_address();
    let result = setup.client.try_make_lobby(&host_2, &MakeLobbyReq {
        lobby_id: 1,
        parameters: lobby_parameters,
    });
    assert!(result.is_err(), "Duplicate lobby ID should fail");
    assert_eq!(result.unwrap_err().unwrap(), Error::AlreadyExists);
}

// endregion

// region leave_lobby tests

#[test]
fn test_leave_lobby_error_conditions() {
    let setup = TestSetup::new();

    // User with no lobby cannot leave
    let user = setup.generate_address();
    let result = setup.client.try_leave_lobby(&user);
    assert_eq!(result.unwrap_err().unwrap(), Error::NotFound, "No lobby to leave");

    // User who already left cannot leave again
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&user, &MakeLobbyReq {
        lobby_id: 1,
        parameters: params,
    });
    setup.client.leave_lobby(&user);
    
    let result = setup.client.try_leave_lobby(&user);
    assert_eq!(result.unwrap_err().unwrap(), Error::NotFound, "Already left lobby");
}


// endregion

// region join_lobby tests

#[test]
fn test_join_lobby_access_control() {
    let setup = TestSetup::new();
    let params = create_test_lobby_parameters(&setup.env);
    let host = setup.generate_address();
    let guest_1 = setup.generate_address();
    let guest_2 = setup.generate_address();
    
    // Create lobby
    setup.client.make_lobby(&host, &MakeLobbyReq {
        lobby_id: 1,
        parameters: params.clone(),
    });

    // Host cannot join own lobby
    let result = setup.client.try_join_lobby(&host, &JoinLobbyReq { lobby_id: 1 });
    assert_eq!(result.unwrap_err().unwrap(), Error::Unauthorized, "Host self-join blocked");

    // First guest joins successfully
    setup.client.join_lobby(&guest_1, &JoinLobbyReq { lobby_id: 1 });

    // Second guest cannot join full lobby
    let result = setup.client.try_join_lobby(&guest_2, &JoinLobbyReq { lobby_id: 1 });
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotJoinable, "Full lobby blocked");
    
    // Guest in active game cannot join another lobby
    setup.client.make_lobby(&guest_2, &MakeLobbyReq {
        lobby_id: 2,
        parameters: params,
    });
    
    let result = setup.client.try_join_lobby(&guest_1, &JoinLobbyReq { lobby_id: 2 });
    assert_eq!(result.unwrap_err().unwrap(), Error::Unauthorized, "Multi-lobby blocked");
}

// endregion

// region: additional lobby tests

#[test]
fn test_host_leaves_lobby_becomes_aborted() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;

    // Host creates lobby
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &req);

    // Host leaves before guest joins
    setup.client.leave_lobby(&host_address);

    // Verify lobby phase changed to Aborted
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.phase, Phase::Aborted);
        assert_eq!(snapshot.subphase, Subphase::None);
    }

    // New guest cannot join this lobby
    let join_req = JoinLobbyReq { lobby_id };
    let result = setup.client.try_join_lobby(&guest_address, &join_req);
    assert!(result.is_err(), "Should fail: lobby is aborted");
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotJoinable);
}

#[test]
fn test_sequential_lobby_creation() {
    let setup = TestSetup::new();
    let user_address = setup.generate_address();
    let guest_address = setup.generate_address();

    // User creates lobby A
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let req_a = MakeLobbyReq {
        lobby_id: 1,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&user_address, &req_a);
    setup.verify_user_lobby(&user_address, 1);

    // User creates lobby B (without leaving A)
    let req_b = MakeLobbyReq {
        lobby_id: 2,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&user_address, &req_b);
    
    // Verify user's current_lobby updated to B
    setup.verify_user_lobby(&user_address, 2);

    // Verify lobby A remains in Lobby phase (not Finished)
    {
        let snapshot_a = extract_phase_snapshot(&setup.env, &setup.contract_id, 1);
        assert_eq!(snapshot_a.phase, Phase::Lobby);
        assert_eq!(snapshot_a.subphase, Subphase::Guest);
    }

    // Guest tries to join A - should succeed
    let join_req_a = JoinLobbyReq { lobby_id: 1 };
    setup.client.join_lobby(&guest_address, &join_req_a);
    
    // Verify game started
    {
        let snapshot_a = extract_phase_snapshot(&setup.env, &setup.contract_id, 1);
        assert_eq!(snapshot_a.phase, Phase::SetupCommit);
        assert_eq!(snapshot_a.subphase, Subphase::Both);
    }
}

#[test]
fn test_lobby_id_reuse_after_finished() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;

    // Create and start game
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address, &req);
    
    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);

    // Game is now in SetupCommit phase
    // Host leaves which finishes the game (guest wins)
    setup.client.leave_lobby(&host_address);

    // Verify game is finished (game was in progress when host left)
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.phase, Phase::Finished);
        assert_eq!(snapshot.subphase, Subphase::Guest); // Guest wins
    }

    // New host creates lobby with same ID
    let new_host = setup.generate_address();
    let new_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    
    // This should fail because the lobby still exists in storage
    let result = setup.client.try_make_lobby(&new_host, &new_req);
    assert!(result.is_err(), "Should fail: lobby ID still exists");
    assert_eq!(result.unwrap_err().unwrap(), Error::AlreadyExists);
}

#[test]
fn test_concurrent_lobby_participation_restrictions() {
    let setup = TestSetup::new();
    let user_a = setup.generate_address();
    let user_b = setup.generate_address();

    // User A hosts lobby 1
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let req_1 = MakeLobbyReq {
        lobby_id: 1,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&user_a, &req_1);

    // User B hosts lobby 2
    let req_2 = MakeLobbyReq {
        lobby_id: 2,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&user_b, &req_2);

    // User A tries to join lobby 2 as guest while hosting lobby 1
    let join_req = JoinLobbyReq { lobby_id: 2 };
    let result = setup.client.try_join_lobby(&user_a, &join_req);
    assert!(result.is_err(), "Should fail: user still in active lobby");
    assert_eq!(result.unwrap_err().unwrap(), Error::Unauthorized);
}

#[test]
fn test_join_triggers_game_initialization() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 1u32;

    // Create and join lobby
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq {
        lobby_id,
        parameters: params,
    });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });

    // Verify game initialization occurred
    {
        let snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.lobby_info.phase, Phase::SetupCommit, "Game started");
        assert_eq!(snapshot.lobby_info.subphase, Subphase::Both, "Both players active");
        assert!(snapshot.game_state.pawns.len() > 0, "Pawns initialized");
    }
    
    // Verify user associations
    setup.verify_user_lobby(&host, lobby_id);
    setup.verify_user_lobby(&guest, lobby_id);
}

#[test]
fn test_leave_during_setup_phase() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 10u32;
    
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    
    // Now in SetupCommit phase
    setup.client.leave_lobby(&guest);
    
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.phase, Phase::Finished);
    assert_eq!(snapshot.subphase, Subphase::Host, "Host should win when guest leaves during setup");
}

#[test]
fn test_abandoned_lobby_remains_joinable() {
    let setup = TestSetup::new();
    let original_host = setup.generate_address();
    let new_guest = setup.generate_address();
    let lobby_id = 105u32;
    
    // Host creates lobby then abandons it (makes new lobby)
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&original_host, &MakeLobbyReq {
        lobby_id,
        parameters: params.clone(),
    });
    
    // Host creates another lobby (abandoning the first)
    setup.client.make_lobby(&original_host, &MakeLobbyReq {
        lobby_id: 999,
        parameters: params,
    });
    
    // Verify original lobby still in waiting state
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.phase, Phase::Lobby);
        assert_eq!(snapshot.subphase, Subphase::Guest, "Still waiting for guest");
    }
    
    // New guest joins the abandoned lobby
    setup.client.join_lobby(&new_guest, &JoinLobbyReq { lobby_id });
    
    // Verify lobby becomes active
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.phase, Phase::SetupCommit, "Game started");
        assert_eq!(snapshot.subphase, Subphase::Both);
    }
}

#[test]
fn test_complete_lobby_to_game_lifecycle() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 400u32;
    
    // Phase 1: Lobby creation and waiting
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.phase, Phase::Lobby);
        assert_eq!(snapshot.subphase, Subphase::Guest, "Waiting for guest");
    }
    
    // Phase 2: Game initialization via join
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    
    {
        let snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.lobby_info.phase, Phase::SetupCommit);
        assert_eq!(snapshot.lobby_info.subphase, Subphase::Both, "Both players setup");
        assert!(snapshot.game_state.pawns.len() > 0, "Game state initialized");
    }
    
    // Phase 3: Setup commits
    let host_root = BytesN::from_array(&setup.env, &[1u8; 16]);
    let guest_root = BytesN::from_array(&setup.env, &[2u8; 16]);
    
    setup.client.commit_setup(&host, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root,
    });
    
    setup.client.commit_setup(&guest, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: guest_root,
    });
    
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.phase, Phase::MoveCommit, "Progressed to movement");
        assert_eq!(snapshot.subphase, Subphase::Both);
    }
    
    // Phase 4: Game termination via forfeit
    setup.client.leave_lobby(&guest);
    
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.phase, Phase::Finished, "Game completed");
        assert_eq!(snapshot.subphase, Subphase::Host, "Host wins by forfeit");
    }
    
    // Phase 5: Post-game cleanup
    setup.verify_user_has_no_lobby(&guest);
    setup.client.leave_lobby(&host);
    setup.verify_user_has_no_lobby(&host);
}

#[test]
fn test_leave_after_game_ends_clears_addresses() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    
    // Verify both addresses in lobby initially
    {
        let snapshot = extract_lobby_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert!(snapshot.lobby_info.host_address.contains(&host));
        assert!(snapshot.lobby_info.guest_address.contains(&guest));
    }
    
    // Guest leaves during game, host wins and game becomes Finished
    setup.client.leave_lobby(&guest);
    
    // Verify game ended properly and guest address cleared
    {
        let snapshot = extract_lobby_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.lobby_info.phase, Phase::Finished);
        assert_eq!(snapshot.lobby_info.subphase, Subphase::Host);
        assert!(snapshot.lobby_info.host_address.contains(&host), "Host should remain");
        assert_eq!(snapshot.lobby_info.guest_address.len(), 0, "Guest address cleared");
    }
    
    // Host leaves after game already ended
    setup.client.leave_lobby(&host);
    
    // Verify game state unchanged but host address cleared
    {
        let snapshot = extract_lobby_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.lobby_info.phase, Phase::Finished, "Phase unchanged");
        assert_eq!(snapshot.lobby_info.subphase, Subphase::Host, "Winner unchanged");
        assert_eq!(snapshot.lobby_info.host_address.len(), 0, "Host address cleared");
        assert_eq!(snapshot.lobby_info.guest_address.len(), 0, "Guest address remains cleared");
    }
    
    // Verify user state consistency
    setup.verify_user_has_no_lobby(&guest);
    setup.verify_user_has_no_lobby(&host);
}

// endregion: additional lobby tests