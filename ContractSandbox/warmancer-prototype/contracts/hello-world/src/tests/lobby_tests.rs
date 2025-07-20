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
    }
}

#[test]
fn test_make_lobby_validation_errors() {
    let setup = TestSetup::new();
    let lobby_parameters = create_test_lobby_parameters(&setup.env);

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
}

// endregion

// region leave_lobby tests

#[test]
fn test_leave_lobby_validation_errors() {
    let setup = TestSetup::new();

    // Test: User not found
    let non_existent_user = setup.generate_address();
    let result = setup.client.try_leave_lobby(&non_existent_user);
    assert!(result.is_err(), "Should fail: user not found");
    assert_eq!(result.unwrap_err().unwrap(), Error::NotFound);

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
    assert_eq!(result.unwrap_err().unwrap(), Error::NotFound);
}


// endregion

// region join_lobby tests

#[test]
fn test_join_lobby_validation_errors() {
    let setup = TestSetup::new();

    // Create a lobby for further tests
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let host_address = setup.generate_address();
    let req = MakeLobbyReq {
        lobby_id: 1,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address, &req);

    // Test: Host trying to join own lobby (will fail because already in a lobby)
    let join_req = JoinLobbyReq { lobby_id: 1 };
    let result = setup.client.try_join_lobby(&host_address, &join_req);
    assert!(result.is_err(), "Should fail: host trying to join own lobby");
    assert_eq!(result.unwrap_err().unwrap(), Error::Unauthorized);

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
    assert_eq!(result.unwrap_err().unwrap(), Error::Unauthorized);
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
fn test_join_initializes_game_correctly() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;

    // Create lobby
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address, &req);

    // Join lobby
    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);

    // Verify game initialization
    {
        let snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
        
        // Verify phase transition
        assert_eq!(snapshot.lobby_info.phase, Phase::SetupCommit);
        assert_eq!(snapshot.lobby_info.subphase, Subphase::Both);
        
        // Verify game state was created
        assert!(snapshot.game_state.pawns.len() > 0, "Game should have pawns");
    }
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
fn test_abandoned_lobby_handling() {
    let setup = TestSetup::new();
    
    // Create multiple abandoned lobbies
    for i in 100..110 {
        let host = setup.generate_address();
        let params = create_test_lobby_parameters(&setup.env);
        let req = MakeLobbyReq {
            lobby_id: i,
            parameters: params,
        };
        setup.client.make_lobby(&host, &req);
        // Don't join or leave - just abandon
    }
    
    // Create a new active lobby
    let active_host = setup.generate_address();
    let active_guest = setup.generate_address();
    let active_lobby_id = 200u32;
    
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&active_host, &MakeLobbyReq { 
        lobby_id: active_lobby_id, 
        parameters: params 
    });
    setup.client.join_lobby(&active_guest, &JoinLobbyReq { lobby_id: active_lobby_id });
    
    // Verify active lobby works despite abandoned lobbies
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, active_lobby_id);
        assert_eq!(snapshot.phase, Phase::SetupCommit);
        assert_eq!(snapshot.subphase, Subphase::Both);
    }
    
    // Try to join an old abandoned lobby - should succeed!
    let new_guest = setup.generate_address();
    setup.client.join_lobby(&new_guest, &JoinLobbyReq { lobby_id: 105 });
    
    // Verify the abandoned lobby is now active with new guest
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, 105);
        assert_eq!(snapshot.phase, Phase::SetupCommit);
        assert_eq!(snapshot.subphase, Subphase::Both);
    }
}

#[test]
fn test_full_lobby_lifecycle() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 400u32;
    
    // Create lobby
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    
    // Verify lobby phase
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.phase, Phase::Lobby);
        assert_eq!(snapshot.subphase, Subphase::Guest);
    }
    
    // Join lobby
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    
    // Verify setup phase
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.phase, Phase::SetupCommit);
        assert_eq!(snapshot.subphase, Subphase::Both);
    }
    
    // Create simple setup commits for testing
    let host_commits: Vec<SetupCommit> = Vec::new(&setup.env);
    let guest_commits: Vec<SetupCommit> = Vec::new(&setup.env);
    let host_ranks: Vec<HiddenRank> = Vec::new(&setup.env);
    let guest_ranks: Vec<HiddenRank> = Vec::new(&setup.env);
    
    // Use dummy merkle roots for testing
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
    
    // Verify move phase
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.phase, Phase::MoveCommit);
        assert_eq!(snapshot.subphase, Subphase::Both);
    }
    
    // Complete the game by having guest leave (host wins)
    setup.client.leave_lobby(&guest);
    
    // Verify finished
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.phase, Phase::Finished);
        assert_eq!(snapshot.subphase, Subphase::Host, "Host should win");
    }
    
    // Verify guest has no current lobby
    setup.verify_user_has_no_lobby(&guest);
    
    // Host must also leave to clear their lobby
    setup.client.leave_lobby(&host);
    setup.verify_user_has_no_lobby(&host);
}

#[test]
fn test_leave_already_ended_lobby() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    
    // Guest leaves first, making lobby Finished
    setup.client.leave_lobby(&guest);
    
    // Verify game is Finished
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.phase, Phase::Finished);
        assert_eq!(snapshot.subphase, Subphase::Host); // Host wins
    }
    
    // Host leaves after game is already Finished
    setup.client.leave_lobby(&host);
    
    // Verify game state didn't change
    {
        let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.phase, Phase::Finished);
        assert_eq!(snapshot.subphase, Subphase::Host); // Still Host wins
    }
    
    // Verify both users have no current lobby
    setup.verify_user_has_no_lobby(&guest);
    setup.verify_user_has_no_lobby(&host);
    
    // Verify addresses were cleared from lobby
    {
        let snapshot = extract_lobby_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.lobby_info.host_address.len(), 0, "Host address should be cleared");
        assert_eq!(snapshot.lobby_info.guest_address.len(), 0, "Guest address should be cleared");
    }
}


#[test]
fn test_address_cleared_on_leave() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    
    // Verify both addresses are in lobby
    {
        let snapshot = extract_lobby_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert!(snapshot.lobby_info.host_address.contains(&host));
        assert!(snapshot.lobby_info.guest_address.contains(&guest));
    }
    
    // Guest leaves first
    setup.client.leave_lobby(&guest);
    
    // Verify guest address is cleared but host remains
    {
        let snapshot = extract_lobby_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert!(snapshot.lobby_info.host_address.contains(&host), "Host should still be in lobby");
        assert_eq!(snapshot.lobby_info.guest_address.len(), 0, "Guest address should be cleared");
    }
    
    // Host leaves
    setup.client.leave_lobby(&host);
    
    // Verify both addresses are now cleared
    {
        let snapshot = extract_lobby_snapshot(&setup.env, &setup.contract_id, lobby_id);
        assert_eq!(snapshot.lobby_info.host_address.len(), 0, "Host address should be cleared");
        assert_eq!(snapshot.lobby_info.guest_address.len(), 0, "Guest address should be cleared");
    }
}

// endregion: additional lobby tests