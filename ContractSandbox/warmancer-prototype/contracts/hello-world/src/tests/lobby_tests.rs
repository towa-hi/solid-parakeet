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
        assert_eq!(validation_snapshot.lobby_parameters.board.name, String::from_str(&setup.env, "Default Stratego Board"));
    }
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

#[test]
fn test_make_lobby_with_user_board_configuration() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 378189u32;

    let lobby_parameters = create_user_board_parameters(&setup.env);
    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters.clone(),
    };

    let result = setup.client.try_make_lobby(&host_address, &req);

    match result {
        Ok(_) => {
            setup.verify_lobby_info(lobby_id, &host_address, Phase::Lobby);
            setup.verify_user_lobby(&host_address, lobby_id);
        },
        Err(err) => {
            match err {
                Ok(contract_error) => {
                    assert!(matches!(contract_error, Error::InvalidArgs),
                           "Expected InvalidArgs error, got: {:?}", contract_error);
                },
                Err(_host_error) => {
                    panic!("Contract panicked with UnreachableCodeReached - this is the bug we found!");
                }
            }
        }
    }
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