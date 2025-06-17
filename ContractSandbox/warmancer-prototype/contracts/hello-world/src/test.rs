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
    
    // Simulate expiration by having the host leave the lobby (which aborts it)
    setup.client.leave_lobby(&host_address);
    
    // Now try to create another lobby - should succeed since current lobby is aborted/expired
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
    
    // Simulate expiration by having the guest leave their lobby (which aborts it)
    setup.client.leave_lobby(&guest_address);
    
    // Guest should now be able to join lobby 1 since their current lobby is aborted
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

//#region commit_setup and prove_setup tests

#[test]
fn test_commit_setup_success() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby to get to setup phase
    setup.create_and_join_lobby(&host_address, &guest_address, lobby_id);
    
    // Generate realistic setup data for host
    let (commit_req, _prove_req, _proofs) = create_realistic_setup_data(&setup.env, lobby_id, 0);
    
    // Host commits setup
    setup.client.commit_setup(&host_address, &commit_req);
    
    // Verify the commit was stored and instruction updated
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        let host_state = game_state.user_states.get(0).unwrap();
        assert_eq!(host_state.setup_hash, commit_req.setup_hash);
        assert_eq!(host_state.instruction, Instruction::WaitingOppSetupCommit);
        
        let guest_state = game_state.user_states.get(1).unwrap();
        assert_eq!(guest_state.instruction, Instruction::RequestingSetupCommit);
    });
}

#[test]
fn test_commit_setup_both_players_committed() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby
    setup.create_and_join_lobby(&host_address, &guest_address, lobby_id);
    
    // Generate setup data for both players
    let (host_commit_req, _host_prove_req, _host_proofs) = create_realistic_setup_data(&setup.env, lobby_id, 0);
    let (guest_commit_req, _guest_prove_req, _guest_proofs) = create_realistic_setup_data(&setup.env, lobby_id, 1);
    
    // Host commits first
    setup.client.commit_setup(&host_address, &host_commit_req);
    
    // Guest commits second - should trigger transition to RequestingSetupProof for both
    setup.client.commit_setup(&guest_address, &guest_commit_req);
    
    // Verify both players transitioned to RequestingSetupProof
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        let host_state = game_state.user_states.get(0).unwrap();
        assert_eq!(host_state.instruction, Instruction::RequestingSetupProof);
        
        let guest_state = game_state.user_states.get(1).unwrap();
        assert_eq!(guest_state.instruction, Instruction::RequestingSetupProof);
    });
}

#[test]
fn test_commit_setup_lobby_not_found() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    
    let (commit_req, _prove_req, _proofs) = create_realistic_setup_data(&setup.env, 999u32, 0);
    
    let result = setup.client.try_commit_setup(&host_address, &commit_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotFound);
}

#[test]
fn test_commit_setup_before_game_starts() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create lobby but don't join (stays in WaitingForPlayers, not Setup phase)
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    let (commit_req, _prove_req, _proofs) = create_realistic_setup_data(&setup.env, lobby_id, 0);
    
    let result = setup.client.try_commit_setup(&host_address, &commit_req);
    let error = result.unwrap_err().unwrap();
    // Should fail because either the game state doesn't exist yet or the lobby isn't in progress
    assert!(matches!(error, Error::GameStateNotFound | Error::GameNotInProgress), 
        "Expected GameStateNotFound or GameNotInProgress, got {:?}", error);
}

#[test]
fn test_commit_setup_user_not_in_lobby() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let outsider_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby
    setup.create_and_join_lobby(&host_address, &guest_address, lobby_id);
    
    let (commit_req, _prove_req, _proofs) = create_realistic_setup_data(&setup.env, lobby_id, 0);
    
    // Outsider tries to commit setup
    let result = setup.client.try_commit_setup(&outsider_address, &commit_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::NotInLobby);
}

#[test]
fn test_prove_setup_success() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby
    setup.create_and_join_lobby(&host_address, &guest_address, lobby_id);
    
    // Generate and commit setup for both players
    let (host_commit_req, host_prove_req, _host_proofs) = create_realistic_setup_data(&setup.env, lobby_id, 0);
    let (guest_commit_req, _guest_prove_req, _guest_proofs) = create_realistic_setup_data(&setup.env, lobby_id, 1);
    
    setup.client.commit_setup(&host_address, &host_commit_req);
    setup.client.commit_setup(&guest_address, &guest_commit_req);
    
    // Host proves setup
    setup.client.prove_setup(&host_address, &host_prove_req);
    
    // Verify setup was stored and instruction updated
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        let host_state = game_state.user_states.get(0).unwrap();
        assert_eq!(host_state.setup.len(), 36); // Should have 36 pieces
        assert_eq!(host_state.instruction, Instruction::WaitingOppSetupProof);
        
        // Phase should still be Setup until both players prove
        assert_eq!(game_state.phase, Phase::Setup);
    });
}

#[test]
fn test_prove_setup_both_players_transitions_to_movement() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby
    setup.create_and_join_lobby(&host_address, &guest_address, lobby_id);
    
    // Generate and commit setup for both players
    let (host_commit_req, host_prove_req, _host_proofs) = create_realistic_setup_data(&setup.env, lobby_id, 0);
    let (guest_commit_req, guest_prove_req, _guest_proofs) = create_realistic_setup_data(&setup.env, lobby_id, 1);
    
    setup.client.commit_setup(&host_address, &host_commit_req);
    setup.client.commit_setup(&guest_address, &guest_commit_req);
    
    // Both players prove setup
    setup.client.prove_setup(&host_address, &host_prove_req);
    setup.client.prove_setup(&guest_address, &guest_prove_req);
    
    // Verify transition to Movement phase and pawns created
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        // Phase should now be Movement
        assert_eq!(game_state.phase, Phase::Movement);
        
        // Both players should be requesting move commits
        let host_state = game_state.user_states.get(0).unwrap();
        let guest_state = game_state.user_states.get(1).unwrap();
        assert_eq!(host_state.instruction, Instruction::RequestingMoveCommit);
        assert_eq!(guest_state.instruction, Instruction::RequestingMoveCommit);
        
        // Should have 72 pawns total (36 per player)
        assert_eq!(game_state.board_state.pawns.len(), 72);
        
        // All pawns should be alive and not moved
        for pawn in game_state.board_state.pawns.iter() {
            assert_eq!(pawn.alive, true);
            assert_eq!(pawn.moved, false);
            assert_eq!(pawn.revealed_rank, 63); // Hidden rank sentinel
        }
    });
}

#[test]
fn test_prove_setup_hash_mismatch() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby
    setup.create_and_join_lobby(&host_address, &guest_address, lobby_id);
    
    // Generate setup data
    let (host_commit_req, mut host_prove_req, _host_proofs) = create_realistic_setup_data(&setup.env, lobby_id, 0);
    let (guest_commit_req, _guest_prove_req, _guest_proofs) = create_realistic_setup_data(&setup.env, lobby_id, 1);
    
    setup.client.commit_setup(&host_address, &host_commit_req);
    setup.client.commit_setup(&guest_address, &guest_commit_req);
    
    // Tamper with the prove_req to make hash mismatch
    host_prove_req.salt = host_prove_req.salt + 1;
    
    let result = setup.client.try_prove_setup(&host_address, &host_prove_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::SetupHashFail);
}

#[test]
fn test_prove_setup_wrong_instruction() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby but don't commit first
    setup.create_and_join_lobby(&host_address, &guest_address, lobby_id);
    
    let (_host_commit_req, host_prove_req, _host_proofs) = create_realistic_setup_data(&setup.env, lobby_id, 0);
    
    // Try to prove without committing first (wrong instruction)
    let result = setup.client.try_prove_setup(&host_address, &host_prove_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongInstruction);
}

#[test]
fn test_commit_setup_already_committed() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby
    setup.create_and_join_lobby(&host_address, &guest_address, lobby_id);
    
    let (host_commit_req, _host_prove_req, _host_proofs) = create_realistic_setup_data(&setup.env, lobby_id, 0);
    
    // Commit once successfully
    setup.client.commit_setup(&host_address, &host_commit_req);
    
    // Try to commit again - should fail with wrong instruction
    let result = setup.client.try_commit_setup(&host_address, &host_commit_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongInstruction);
}

#[test]
fn test_prove_setup_pawn_ids_decoded_correctly() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby
    setup.create_and_join_lobby(&host_address, &guest_address, lobby_id);
    
    // Generate and commit setup for both players
    let (host_commit_req, host_prove_req, _host_proofs) = create_realistic_setup_data(&setup.env, lobby_id, 0);
    let (guest_commit_req, guest_prove_req, _guest_proofs) = create_realistic_setup_data(&setup.env, lobby_id, 1);
    
    setup.client.commit_setup(&host_address, &host_commit_req);
    setup.client.commit_setup(&guest_address, &guest_commit_req);
    
    // Both players prove setup
    setup.client.prove_setup(&host_address, &host_prove_req);
    setup.client.prove_setup(&guest_address, &guest_prove_req);
    
    // Verify pawn positions were decoded correctly from pawn IDs
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        // Check that pawn positions match what we expect from the setup data
        let mut host_pawns = 0u32;
        let mut guest_pawns = 0u32;
        
        for pawn in game_state.board_state.pawns.iter() {
            let (_pos, team) = Contract::decode_pawn_id(&pawn.pawn_id);
            if team == 0 {
                host_pawns += 1;
                // Host pawns should be in bottom rows (y <= 3)
                assert!(pawn.pos.y <= 3, "Host pawn at incorrect position: {:?}", pawn.pos);
            } else {
                guest_pawns += 1;
                // Guest pawns should be in top rows (y >= 6)
                assert!(pawn.pos.y >= 6, "Guest pawn at incorrect position: {:?}", pawn.pos);
            }
        }
        
        assert_eq!(host_pawns, 36);
        assert_eq!(guest_pawns, 36);
    });
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

// Helper function to encode pawn ID from position and team (reverse of decode_pawn_id)
fn encode_pawn_id(pos: (i32, i32), team_index: u32) -> u32 {
    let base_id = (pos.0 as u32) * 101 + (pos.1 as u32);
    let is_red = team_index == 0;
    (base_id << 1) | (if is_red { 0u32 } else { 1u32 })
}

// Helper function to create realistic setup data similar to Unity client auto-setup
fn create_realistic_setup_data(env: &Env, lobby_id: u32, team_index: u32) -> (CommitSetupReq, ProveSetupReq, Vec<HiddenRankProof>) {
    // Get the lobby parameters for realistic piece distribution
    let lobby_parameters = create_test_lobby_parameters(env);
    
    // Define realistic setup positions for each team
    // Team 0 (host, typically red) uses bottom rows, Team 1 (guest, typically blue) uses top rows
    let setup_positions = if team_index == 0 {
        // Host team positions (bottom 4 rows of a 10x10 board)
        [
            // Row 0 (back line - flags, bombs, high-value pieces)
            (0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 0), (9, 0),
            // Row 1 (second line - mixed pieces)
            (0, 1), (1, 1), (2, 1), (3, 1), (4, 1), (5, 1), (6, 1), (7, 1), (8, 1), (9, 1),
            // Row 2 (third line - scouts and mobile pieces)
            (0, 2), (1, 2), (2, 2), (3, 2), (4, 2), (5, 2), (6, 2), (7, 2), (8, 2), (9, 2),
            // Row 3 (front line - attacking pieces)
            (0, 3), (1, 3), (2, 3), (3, 3), (4, 3), (5, 3)
        ]
    } else {
        // Guest team positions (top 4 rows of a 10x10 board)
        [
            // Row 9 (back line)
            (0, 9), (1, 9), (2, 9), (3, 9), (4, 9), (5, 9), (6, 9), (7, 9), (8, 9), (9, 9),
            // Row 8 (second line)
            (0, 8), (1, 8), (2, 8), (3, 8), (4, 8), (5, 8), (6, 8), (7, 8), (8, 8), (9, 8),
            // Row 7 (third line)
            (0, 7), (1, 7), (2, 7), (3, 7), (4, 7), (5, 7), (6, 7), (7, 7), (8, 7), (9, 7),
            // Row 6 (front line)
            (0, 6), (1, 6), (2, 6), (3, 6), (4, 6), (5, 6)
        ]
    };
    
    // Create the pieces according to max_ranks
    let mut pawn_commits = Vec::new(env);
    let mut hidden_rank_proofs = Vec::new(env);
    let mut position_index = 0;
    
    // Process each rank type
    for max_rank in lobby_parameters.max_ranks.iter() {
        if max_rank.rank == 99 || max_rank.max == 0 {
            continue; // Skip empty/unused rank
        }
        
        for _ in 0..max_rank.max {
            if position_index >= setup_positions.len() {
                break; // Safety check
            }
            
            let pos = setup_positions.get(position_index).copied().unwrap();
            let pawn_id = encode_pawn_id(pos, team_index);
            
            // Create a realistic salt (non-zero random number)
            let salt = 12345678901234567890u64 + position_index as u64 + max_rank.rank as u64 * 1000;
            
            let hidden_rank = HiddenRank {
                rank: max_rank.rank,
                salt,
            };
            
            // Hash the hidden rank to get the hash
            let serialized = hidden_rank.clone().to_xdr(env);
            let hidden_rank_hash: BytesN<32> = env.crypto().sha256(&serialized).to_bytes();
            
            let pawn_commit = PawnCommit {
                hidden_rank_hash: hidden_rank_hash.clone(),
                pawn_id,
            };
            
            let hidden_rank_proof = HiddenRankProof {
                hidden_rank: hidden_rank.clone(),
                hidden_rank_hash: hidden_rank_hash.clone(),
                pawn_id,
            };
            
            pawn_commits.push_back(pawn_commit);
            hidden_rank_proofs.push_back(hidden_rank_proof);
            position_index += 1;
        }
    }
    
    // Create ProveSetupReq with a realistic salt
    let prove_setup_salt = 987654321u32 + team_index * 1000 + lobby_id;
    let prove_setup_req = ProveSetupReq {
        lobby_id,
        salt: prove_setup_salt,
        setup: pawn_commits,
    };
    
    // Hash the ProveSetupReq to create the CommitSetupReq
    let serialized = prove_setup_req.clone().to_xdr(env);
    let setup_hash: BytesN<32> = env.crypto().sha256(&serialized).to_bytes();
    
    let commit_setup_req = CommitSetupReq {
        lobby_id,
        setup_hash,
    };
    
    (commit_setup_req, prove_setup_req, hidden_rank_proofs)
}

//#endregion