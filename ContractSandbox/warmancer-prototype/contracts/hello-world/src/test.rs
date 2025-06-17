#![cfg(test)]

use super::*;
use soroban_sdk::{Env, Address};
use soroban_sdk::testutils::Address as _;

//#region make_lobby tests

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
    
    // Verify lobby info and user were stored correctly
    setup.verify_lobby_info(lobby_id, &host_address, LobbyStatus::WaitingForPlayers);
    setup.verify_user_lobby(&host_address, lobby_id);
    
    // Verify detailed lobby parameters
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_params_key = DataKey::LobbyParameters(lobby_id);
        let stored_params: LobbyParameters = setup.env.storage()
            .temporary()
            .get(&lobby_params_key)
            .expect("Lobby parameters should be stored");
        
        assert_eq!(stored_params.dev_mode, true);
        assert_eq!(stored_params.host_team, 1);
        assert_eq!(stored_params.must_fill_all_tiles, true);
        assert_eq!(stored_params.security_mode, true);
        assert_eq!(stored_params.max_ranks.len(), 13);
    });
}

#[test]
fn test_make_lobby_host_already_in_lobby() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    
    // First, create a lobby successfully
    let lobby_parameters = create_simple_lobby_parameters(&setup.env);
    let first_req = MakeLobbyReq {
        lobby_id: 1u32,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address, &first_req);
    
    // Now try to create another lobby with the same host - should fail
    let second_req = MakeLobbyReq {
        lobby_id: 2u32,
        parameters: lobby_parameters,
    };
    
    let result = setup.client.try_make_lobby(&host_address, &second_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::HostAlreadyInLobby);
}

#[test]
fn test_make_lobby_already_exists() {
    let setup = TestSetup::new();
    let host_address_1 = setup.generate_address();
    let host_address_2 = setup.generate_address();
    
    let lobby_id = 1u32;
    let lobby_parameters = create_simple_lobby_parameters(&setup.env);
    
    // First user creates lobby successfully
    let req_1 = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address_1, &req_1);
    
    // Second user tries to create lobby with same ID - should fail
    let req_2 = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    
    let result = setup.client.try_make_lobby(&host_address_2, &req_2);
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyAlreadyExists);
}

#[test]
fn test_make_lobby_with_different_parameters() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 1u32;
    
    let board_hash = BytesN::from_array(&setup.env, &[2u8; 32]);
    let max_ranks = Vec::from_array(
        &setup.env, 
        [
            MaxRank { max: 1, rank: 10 }, // Flag
            MaxRank { max: 2, rank: 9 },  // Spy
            MaxRank { max: 1, rank: 8 },  // Marshal
        ]
    );
    
    let lobby_parameters = LobbyParameters {
        board_hash,
        dev_mode: false,
        host_team: 1,
        max_ranks,
        must_fill_all_tiles: true,
        security_mode: true,
    };
    
    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    
    setup.client.make_lobby(&host_address, &req);
    
    // Verify specific parameters were stored
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_params_key = DataKey::LobbyParameters(lobby_id);
        let stored_params: LobbyParameters = setup.env.storage()
            .temporary()
            .get(&lobby_params_key)
            .expect("Lobby parameters should be stored");
        
        assert_eq!(stored_params.dev_mode, false);
        assert_eq!(stored_params.host_team, 1);
        assert_eq!(stored_params.must_fill_all_tiles, true);
        assert_eq!(stored_params.security_mode, true);
        assert_eq!(stored_params.max_ranks.len(), 3);
        assert_eq!(stored_params.max_ranks.get(0).unwrap().rank, 10);
        assert_eq!(stored_params.max_ranks.get(1).unwrap().rank, 9);
        assert_eq!(stored_params.max_ranks.get(2).unwrap().rank, 8);
    });
}

#[test]
fn test_make_multiple_lobbies_different_hosts() {
    let setup = TestSetup::new();
    let host_addresses = [
        setup.generate_address(),
        setup.generate_address(),
        setup.generate_address(),
    ];
    
    let lobby_parameters = create_simple_lobby_parameters(&setup.env);
    
    // Create three different lobbies with different hosts
    for (i, host_addr) in host_addresses.iter().enumerate() {
        let lobby_id = (i + 1) as u32;
        let req = MakeLobbyReq {
            lobby_id,
            parameters: lobby_parameters.clone(),
        };
        
        setup.client.make_lobby(host_addr, &req);
        setup.verify_lobby_info(lobby_id, host_addr, LobbyStatus::WaitingForPlayers);
    }
}

#[test]
fn test_make_lobby_with_realistic_stratego_ranks() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 1u32;
    
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    
    setup.client.make_lobby(&host_address, &req);
    
    // Verify the realistic max_ranks were stored correctly
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_params_key = DataKey::LobbyParameters(lobby_id);
        let stored_params: LobbyParameters = setup.env.storage()
            .temporary()
            .get(&lobby_params_key)
            .expect("Lobby parameters should be stored");
        
        assert_eq!(stored_params.max_ranks.len(), 13);
        
        // Verify specific pieces
        assert_eq!(stored_params.max_ranks.get(0).unwrap().max, 1);  // Flag
        assert_eq!(stored_params.max_ranks.get(0).unwrap().rank, 0);
        
        assert_eq!(stored_params.max_ranks.get(1).unwrap().max, 1);  // Spy
        assert_eq!(stored_params.max_ranks.get(1).unwrap().rank, 1);
        
        assert_eq!(stored_params.max_ranks.get(2).unwrap().max, 8);  // Scouts
        assert_eq!(stored_params.max_ranks.get(2).unwrap().rank, 2);
        
        assert_eq!(stored_params.max_ranks.get(9).unwrap().max, 1);  // General
        assert_eq!(stored_params.max_ranks.get(9).unwrap().rank, 9);
        
        assert_eq!(stored_params.max_ranks.get(10).unwrap().max, 1); // Marshal
        assert_eq!(stored_params.max_ranks.get(10).unwrap().rank, 10);
        
        assert_eq!(stored_params.max_ranks.get(11).unwrap().max, 4); // Bombs
        assert_eq!(stored_params.max_ranks.get(11).unwrap().rank, 11);
        
        assert_eq!(stored_params.max_ranks.get(12).unwrap().max, 0); // Empty/unused
        assert_eq!(stored_params.max_ranks.get(12).unwrap().rank, 99);
        
        // Calculate total pieces (excluding empty rank 99)
        let mut total_pieces = 0u32;
        for i in 0..12 {  // Exclude the last one (rank 99)
            total_pieces += stored_params.max_ranks.get(i).unwrap().max;
        }
        assert_eq!(total_pieces, 36); // Total pieces from the provided configuration
    });
}

#[test]
fn test_make_lobby_with_expired_current_lobby() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    
    // Create a lobby first
    let lobby_parameters = create_simple_lobby_parameters(&setup.env);
    let first_req = MakeLobbyReq {
        lobby_id: 1u32,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address, &first_req);
    
    // Manually expire/delete the lobby from storage to simulate expiration
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(1u32);
        setup.env.storage().temporary().remove(&lobby_info_key);
    });
    
    // Now try to create another lobby - should succeed since current lobby is expired
    let second_req = MakeLobbyReq {
        lobby_id: 2u32,
        parameters: lobby_parameters,
    };
    
    // This should succeed now
    setup.client.make_lobby(&host_address, &second_req);
    
    // Verify the new lobby was created and user updated
    setup.verify_lobby_info(2u32, &host_address, LobbyStatus::WaitingForPlayers);
    setup.verify_user_lobby(&host_address, 2u32);
}

//#endregion

//#region leave_lobby tests

#[test]
fn test_leave_lobby_success_host_leaves() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create a lobby
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &req);
    
    // Host leaves the lobby
    setup.client.leave_lobby(&host_address);
    
    // Verify user's current_lobby was reset and lobby was aborted with cleared addresses
    setup.verify_user_lobby(&host_address, 0);
    setup.verify_lobby_aborted_and_addresses_cleared(lobby_id, true, true);
}

#[test]
fn test_leave_lobby_success_guest_leaves() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby
    setup.create_and_join_lobby(&host_address, &guest_address, lobby_id);
    
    // Guest leaves the lobby
    setup.client.leave_lobby(&guest_address);
    
    // Verify guest's current_lobby was reset and lobby was aborted with guest address cleared
    setup.verify_user_lobby(&guest_address, 0);
    setup.verify_lobby_aborted_and_addresses_cleared(lobby_id, false, true);
    
    // Verify host address is still there
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let stored_lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should be stored");
        assert_eq!(stored_lobby_info.host_address, host_address);
    });
}

#[test]
fn test_leave_lobby_user_not_found() {
    let setup = TestSetup::new();
    let non_existent_user = setup.generate_address();
    
    // Try to leave lobby with user that doesn't exist
    let result = setup.client.try_leave_lobby(&non_existent_user);
    assert_eq!(result.unwrap_err().unwrap(), Error::UserNotFound);
}

#[test]
fn test_leave_lobby_no_current_lobby() {
    let setup = TestSetup::new();
    let user_address = setup.generate_address();
    
    // Create a user by making and immediately leaving a lobby
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let req = MakeLobbyReq {
        lobby_id: 1u32,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&user_address, &req);
    setup.client.leave_lobby(&user_address);
    
    // Try to leave again (user has current_lobby = 0) - should succeed
    setup.client.leave_lobby(&user_address);
    
    // Verify user's current_lobby is still 0
    setup.verify_user_lobby(&user_address, 0);
}

#[test]
fn test_leave_lobby_lobby_already_deleted() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create a lobby
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &req);
    
    // Manually delete the lobby from storage to simulate the lobby being gone
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        setup.env.storage().temporary().remove(&lobby_info_key);
    });
    
    // Try to leave - should succeed and just reset user's current_lobby
    setup.client.leave_lobby(&host_address);
    
    // Verify user's current_lobby was reset to 0
    setup.verify_user_lobby(&host_address, 0);
}

#[test]
fn test_leave_lobby_multiple_users_same_lobby() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby
    setup.create_and_join_lobby(&host_address, &guest_address, lobby_id);
    
    // Host leaves first
    setup.client.leave_lobby(&host_address);
    
    // Verify host is out, lobby is aborted, host address cleared but guest remains
    setup.verify_user_lobby(&host_address, 0);
    setup.verify_lobby_aborted_and_addresses_cleared(lobby_id, true, false);
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let stored_lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should be stored");
        assert_eq!(stored_lobby_info.guest_address, guest_address);
    });
    
    // Guest also leaves
    setup.client.leave_lobby(&guest_address);
    
    // Verify guest is also out and both addresses are cleared
    setup.verify_user_lobby(&guest_address, 0);
    setup.verify_lobby_aborted_and_addresses_cleared(lobby_id, true, true);
}

//#endregion

//#region join_lobby tests (only guests can use join_lobby - hosts cannot rejoin after leaving)

#[test]
fn test_join_lobby_success() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Host creates a lobby
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    // Guest joins the lobby
    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);
    
    // Verify guest joined successfully
    setup.verify_user_lobby(&guest_address, lobby_id);
    
    // Verify lobby status changed to GameInProgress and guest was added
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let stored_lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should be stored");
        
        assert_eq!(stored_lobby_info.guest_address, guest_address);
        assert_eq!(stored_lobby_info.host_address, host_address);
        assert_eq!(stored_lobby_info.status, LobbyStatus::GameInProgress);
        
        // Verify game state was created
        let game_state_key = DataKey::GameState(lobby_id);
        let stored_game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should be created");
        
        assert_eq!(stored_game_state.phase, Phase::Setup);
        assert_eq!(stored_game_state.user_states.len(), 2);
        assert_eq!(stored_game_state.user_states.get(0).unwrap().instruction, Instruction::RequestingSetupCommit);
        assert_eq!(stored_game_state.user_states.get(1).unwrap().instruction, Instruction::RequestingSetupCommit);
    });
}

#[test]
fn test_join_lobby_not_found() {
    let setup = TestSetup::new();
    let guest_address = setup.generate_address();
    
    // Try to join a lobby that doesn't exist
    let join_req = JoinLobbyReq { lobby_id: 999u32 };
    let result = setup.client.try_join_lobby(&guest_address, &join_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotFound);
}

#[test]
fn test_join_lobby_already_in_progress() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let third_user = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby (moves to GameInProgress)
    setup.create_and_join_lobby(&host_address, &guest_address, lobby_id);
    
    // Third user tries to join a lobby that's already in progress
    let join_req = JoinLobbyReq { lobby_id };
    let result = setup.client.try_join_lobby(&third_user, &join_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::AlreadyInitialized);
}

#[test]
fn test_join_lobby_guest_already_in_lobby() {
    let setup = TestSetup::new();
    let host_address_1 = setup.generate_address();
    let host_address_2 = setup.generate_address();
    let guest_address = setup.generate_address();
    
    let lobby_parameters = create_simple_lobby_parameters(&setup.env);
    
    // Create two lobbies
    let lobby1_req = MakeLobbyReq {
        lobby_id: 1u32,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address_1, &lobby1_req);
    
    let lobby2_req = MakeLobbyReq {
        lobby_id: 2u32,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address_2, &lobby2_req);
    
    // Guest joins first lobby
    let join_req_1 = JoinLobbyReq { lobby_id: 1u32 };
    setup.client.join_lobby(&guest_address, &join_req_1);
    
    // Guest tries to join second lobby while already in first - should fail
    let join_req_2 = JoinLobbyReq { lobby_id: 2u32 };
    let result = setup.client.try_join_lobby(&guest_address, &join_req_2);
    assert_eq!(result.unwrap_err().unwrap(), Error::GuestAlreadyInLobby);
}

#[test]
fn test_join_lobby_no_host() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create a lobby
    let lobby_parameters = create_simple_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    // Manually clear the host address to simulate a lobby with no host
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let mut lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby should exist");
        
        lobby_info.host_address = Contract::empty_address(&setup.env);
        setup.env.storage().temporary().set(&lobby_info_key, &lobby_info);
    });
    
    // Guest tries to join lobby with no host
    let join_req = JoinLobbyReq { lobby_id };
    let result = setup.client.try_join_lobby(&guest_address, &join_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyHasNoHost);
}

#[test]
fn test_join_lobby_already_has_guest() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address_1 = setup.generate_address();
    let guest_address_2 = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create lobby 
    let lobby_parameters = create_simple_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    let join_req_1 = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address_1, &join_req_1);
    
    // Second guest tries to join same lobby - should fail with AlreadyInitialized
    // because the lobby status changed to GameInProgress after first guest joined
    let join_req_2 = JoinLobbyReq { lobby_id };
    let result = setup.client.try_join_lobby(&guest_address_2, &join_req_2);
    assert_eq!(result.unwrap_err().unwrap(), Error::AlreadyInitialized);
}

#[test]
fn test_join_lobby_joiner_is_host() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Host creates a lobby
    let lobby_parameters = create_simple_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    // Host tries to join their own lobby - should fail with GuestAlreadyInLobby
    // because the validation for "user already in lobby" happens before "joiner is host"
    let join_req = JoinLobbyReq { lobby_id };
    let result = setup.client.try_join_lobby(&host_address, &join_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::GuestAlreadyInLobby);
}

#[test]
fn test_join_lobby_joiner_is_host_specific() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Host creates a lobby
    let lobby_parameters = create_simple_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    // Manually clear the host's current_lobby to bypass the "already in lobby" check
    // so we can test the specific "joiner is host" validation
    setup.env.as_contract(&setup.contract_id, || {
        let user_key = DataKey::PackedUser(host_address.clone());
        let mut user: User = setup.env.storage()
            .persistent()
            .get(&user_key)
            .expect("User should exist");
        
        user.current_lobby = 0;
        setup.env.storage().persistent().set(&user_key, &user);
    });
    
    // Now host tries to join their own lobby - should fail with JoinerIsHost
    let join_req = JoinLobbyReq { lobby_id };
    let result = setup.client.try_join_lobby(&host_address, &join_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::JoinerIsHost);
}

#[test]
fn test_join_lobby_with_expired_current_lobby() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    
    // Create two lobbies
    let lobby_parameters = create_simple_lobby_parameters(&setup.env);
    
    // Host creates lobby 1
    let lobby1_req = MakeLobbyReq {
        lobby_id: 1u32,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address, &lobby1_req);
    
    // Guest creates lobby 2
    let lobby2_req = MakeLobbyReq {
        lobby_id: 2u32,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&guest_address, &lobby2_req);
    
    // Manually expire/delete guest's lobby (lobby 2) to simulate expiration
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(2u32);
        setup.env.storage().temporary().remove(&lobby_info_key);
    });
    
    // Guest should now be able to join lobby 1 even though current_lobby != 0
    let join_req = JoinLobbyReq { lobby_id: 1u32 };
    setup.client.join_lobby(&guest_address, &join_req);
    
    // Verify guest joined successfully
    setup.verify_user_lobby(&guest_address, 1u32);
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(1u32);
        let stored_lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should be stored");
        
        assert_eq!(stored_lobby_info.guest_address, guest_address);
        assert_eq!(stored_lobby_info.status, LobbyStatus::GameInProgress);
    });
}

#[test]
fn test_join_lobby_guest_slot_already_filled() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create lobby and manually set a guest without changing status
    let lobby_parameters = create_simple_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    // Manually set guest address without going through join_lobby to keep status WaitingForPlayers
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let mut lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby should exist");
        
        lobby_info.guest_address = guest_address.clone();
        // Keep status as WaitingForPlayers to test the guest slot check
        setup.env.storage().temporary().set(&lobby_info_key, &lobby_info);
    });
    
    // Another user tries to join - should fail with GuestAlreadyInLobby
    let another_user = setup.generate_address();
    let join_req = JoinLobbyReq { lobby_id };
    let result = setup.client.try_join_lobby(&another_user, &join_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::GuestAlreadyInLobby);
}

#[test]
fn test_join_lobby_host_cannot_rejoin_after_leaving() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Host creates a lobby
    let lobby_parameters = create_simple_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    // Host leaves the lobby (making it aborted and not joinable)
    setup.client.leave_lobby(&host_address);
    
    // Verify lobby is aborted
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let stored_lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should still exist");
        
        assert_eq!(stored_lobby_info.status, LobbyStatus::Aborted);
        assert!(Contract::is_address_empty(&setup.env, &stored_lobby_info.host_address));
    });
    
    // Host tries to rejoin their own lobby - should fail because lobby is no longer WaitingForPlayers
    let join_req = JoinLobbyReq { lobby_id };
    let result = setup.client.try_join_lobby(&host_address, &join_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::AlreadyInitialized);
    
    // Guest also cannot join an aborted lobby
    let result = setup.client.try_join_lobby(&guest_address, &join_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::AlreadyInitialized);
}

//#endregion

//#region helper functions

// Test environment setup helper
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
    
    fn generate_address(&self) -> Address {
        Address::generate(&self.env)
    }
    
    fn verify_lobby_info(&self, lobby_id: u32, expected_host: &Address, expected_status: LobbyStatus) {
        self.env.as_contract(&self.contract_id, || {
            let lobby_info_key = DataKey::LobbyInfo(lobby_id);
            let stored_lobby_info: LobbyInfo = self.env.storage()
                .temporary()
                .get(&lobby_info_key)
                .expect("Lobby info should be stored");
            
            assert_eq!(stored_lobby_info.index, lobby_id);
            assert_eq!(stored_lobby_info.host_address, *expected_host);
            assert_eq!(stored_lobby_info.status, expected_status);
        });
    }
    
    fn verify_user_lobby(&self, user_address: &Address, expected_lobby_id: u32) {
        self.env.as_contract(&self.contract_id, || {
            let user_key = DataKey::PackedUser(user_address.clone());
            let stored_user: User = self.env.storage()
                .persistent()
                .get(&user_key)
                .expect("User should be stored");
            
            assert_eq!(stored_user.current_lobby, expected_lobby_id);
        });
    }
    
    fn verify_lobby_aborted_and_addresses_cleared(&self, lobby_id: u32, host_cleared: bool, guest_cleared: bool) {
        self.env.as_contract(&self.contract_id, || {
            let lobby_info_key = DataKey::LobbyInfo(lobby_id);
            let stored_lobby_info: LobbyInfo = self.env.storage()
                .temporary()
                .get(&lobby_info_key)
                .expect("Lobby info should be stored");
            
            assert_eq!(stored_lobby_info.status, LobbyStatus::Aborted);
            
            if host_cleared {
                assert!(Contract::is_address_empty(&self.env, &stored_lobby_info.host_address));
            }
            if guest_cleared {
                assert!(Contract::is_address_empty(&self.env, &stored_lobby_info.guest_address));
            }
        });
    }
    
    fn create_and_join_lobby(&self, host: &Address, guest: &Address, lobby_id: u32) -> LobbyParameters {
        let lobby_parameters = create_test_lobby_parameters(&self.env);
        let make_req = MakeLobbyReq {
            lobby_id,
            parameters: lobby_parameters.clone(),
        };
        self.client.make_lobby(host, &make_req);
        
        let join_req = JoinLobbyReq { lobby_id };
        self.client.join_lobby(guest, &join_req);
        
        lobby_parameters
    }
}

// Create simple lobby parameters for error testing
fn create_simple_lobby_parameters(env: &Env) -> LobbyParameters {
    let board_hash = BytesN::from_array(env, &[1u8; 32]);
    LobbyParameters {
        board_hash,
        dev_mode: true,
        host_team: 0,
        max_ranks: Vec::from_array(env, [MaxRank { max: 10, rank: 1 }]),
        must_fill_all_tiles: false,
        security_mode: false,
    }
}

// Helper function to create test lobby parameters with realistic Stratego piece distribution
fn create_test_lobby_parameters(env: &Env) -> LobbyParameters {
    // Convert hex string to bytes: ef3b532a3e481f29108991ac07bfebd3bb0fc441b2a7b9e47c99c0e6ff8c8f78
    let board_hash = BytesN::from_array(env, &[
        0xef, 0x3b, 0x53, 0x2a, 0x3e, 0x48, 0x1f, 0x29,
        0x10, 0x89, 0x91, 0xac, 0x07, 0xbf, 0xeb, 0xd3,
        0xbb, 0x0f, 0xc4, 0x41, 0xb2, 0xa7, 0xb9, 0xe4,
        0x7c, 0x99, 0xc0, 0xe6, 0xff, 0x8c, 0x8f, 0x78
    ]);
    let max_ranks = Vec::from_array(env, [
        MaxRank { max: 1, rank: 0 },   // Flag
        MaxRank { max: 1, rank: 1 },   // Spy
        MaxRank { max: 8, rank: 2 },   // Scouts
        MaxRank { max: 3, rank: 3 },   // Miners
        MaxRank { max: 4, rank: 4 },   // Sergeants
        MaxRank { max: 4, rank: 5 },   // Lieutenants
        MaxRank { max: 4, rank: 6 },   // Captains
        MaxRank { max: 3, rank: 7 },   // Majors
        MaxRank { max: 2, rank: 8 },   // Colonels
        MaxRank { max: 1, rank: 9 },   // General
        MaxRank { max: 1, rank: 10 },  // Marshal
        MaxRank { max: 4, rank: 11 },  // Bombs
        MaxRank { max: 0, rank: 99 },  // Empty/unused
    ]);
    
    LobbyParameters {
        board_hash,
        dev_mode: true,
        host_team: 1,
        max_ranks,
        must_fill_all_tiles: true,
        security_mode: true,
    }
}

//#endregion