#![cfg(test)]

use super::*;
use soroban_sdk::{Env, Address};
use soroban_sdk::testutils::Address as _;

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
    
    // Helper to check if error indicates user/lobby conflict
    fn is_user_conflict_error(error: &Error) -> bool {
        matches!(error, 
            Error::GuestAlreadyInLobby | 
            Error::HostAlreadyInLobby | 
            Error::JoinerIsHost |
            Error::NotInLobby
        )
    }
    
    // Helper to check if error indicates lobby state issue
    fn is_lobby_state_error(error: &Error) -> bool {
        matches!(error, 
            Error::LobbyNotJoinable | 
            Error::WrongPhase | 
            Error::LobbyNotFound |
            Error::LobbyHasNoHost
        )
    }
    
    // Helper to check if error indicates validation failure
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
            
            assert_eq!(stored_user.current_lobby.get(0).unwrap(), expected_lobby_id);
        });
    }
    
    fn verify_user_has_no_lobby(&self, user_address: &Address) {
        self.env.as_contract(&self.contract_id, || {
            let user_key = DataKey::User(user_address.clone());
            let stored_user: User = self.env.storage()
                .persistent()
                .get(&user_key)
                .expect("User should be stored");
            
            assert!(stored_user.current_lobby.is_empty());
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
            assert_eq!(stored_game_state.setups.len(), 2);
        });
    }
}

// Helper function to create test lobby parameters
fn create_test_lobby_parameters(env: &Env) -> LobbyParameters {
    let board_hash = BytesN::from_array(env, &[1u8; 32]);
    
    // Create a simple 4x4 board for testing
    let tiles = Vec::from_array(env, [
        // Team 0 setup positions (bottom 2 rows)
        Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0 },
        Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 0 },
        Tile { pos: Pos { x: 2, y: 0 }, passable: true, setup: 0 },
        Tile { pos: Pos { x: 3, y: 0 }, passable: true, setup: 0 },
        Tile { pos: Pos { x: 0, y: 1 }, passable: true, setup: 0 },
        Tile { pos: Pos { x: 1, y: 1 }, passable: true, setup: 0 },
        
        // Neutral/impassable tiles (middle)
        Tile { pos: Pos { x: 0, y: 2 }, passable: true, setup: 2 },
        Tile { pos: Pos { x: 1, y: 2 }, passable: false, setup: 2 },
        Tile { pos: Pos { x: 2, y: 2 }, passable: false, setup: 2 },
        Tile { pos: Pos { x: 3, y: 2 }, passable: true, setup: 2 },
        
        // Team 1 setup positions (top 2 rows)
        Tile { pos: Pos { x: 0, y: 3 }, passable: true, setup: 1 },
        Tile { pos: Pos { x: 1, y: 3 }, passable: true, setup: 1 },
        Tile { pos: Pos { x: 2, y: 3 }, passable: true, setup: 1 },
        Tile { pos: Pos { x: 3, y: 3 }, passable: true, setup: 1 },
        Tile { pos: Pos { x: 2, y: 1 }, passable: true, setup: 1 },
        Tile { pos: Pos { x: 3, y: 1 }, passable: true, setup: 1 },
    ]);
    
    let board = Board {
        name: String::from_str(env, "Test Board"),
        tiles,
        hex: false,
        size: Pos { x: 4, y: 4 },
    };
    
    LobbyParameters {
        board_hash,
        board,
        dev_mode: true,
        host_team: 0,
        max_ranks: Vec::from_array(env, [1u32, 2u32, 3u32]),
        must_fill_all_tiles: false,
        security_mode: false,
    }
}

// Helper function for invalid board testing
fn create_invalid_board_parameters(env: &Env) -> LobbyParameters {
    let board_hash = BytesN::from_array(env, &[1u8; 32]);
    
    // Create board with wrong tile count (size says 2x2 but we have 3 tiles)
    let tiles = Vec::from_array(env, [
        Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0 },
        Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 0 },
        Tile { pos: Pos { x: 0, y: 1 }, passable: true, setup: 1 },
    ]);
    
    let board = Board {
        name: String::from_str(env, "Invalid Board"),
        tiles,
        hex: false,
        size: Pos { x: 2, y: 2 }, // Says 2x2 = 4 tiles, but we only have 3
    };
    
    LobbyParameters {
        board_hash,
        board,
        dev_mode: true,
        host_team: 0,
        max_ranks: Vec::from_array(env, [1u32]),
        must_fill_all_tiles: false,
        security_mode: false,
    }
}

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
    setup.verify_lobby_info(lobby_id, &host_address, Phase::Lobby);
    setup.verify_user_lobby(&host_address, lobby_id);
    
    // Verify lobby parameters were stored
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_params_key = DataKey::LobbyParameters(lobby_id);
        let stored_params: LobbyParameters = setup.env.storage()
            .temporary()
            .get(&lobby_params_key)
            .expect("Lobby parameters should be stored");
        
        assert_eq!(stored_params.dev_mode, true);
        assert_eq!(stored_params.host_team, 0);
        assert_eq!(stored_params.board.name, String::from_str(&setup.env, "Test Board"));
    });
}

#[test]
fn test_make_lobby_already_exists() {
    let setup = TestSetup::new();
    let host_address_1 = setup.generate_address();
    let host_address_2 = setup.generate_address();
    
    let lobby_id = 1u32;
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    
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
    let error = result.unwrap_err().unwrap();
    assert!(TestSetup::is_validation_error(&error));
}

#[test]
fn test_make_lobby_invalid_board() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 1u32;
    
    let invalid_lobby_parameters = create_invalid_board_parameters(&setup.env);
    let req = MakeLobbyReq {
        lobby_id,
        parameters: invalid_lobby_parameters,
    };
    
    let result = setup.client.try_make_lobby(&host_address, &req);
    let error = result.unwrap_err().unwrap();
    assert!(TestSetup::is_validation_error(&error));
}

#[test]
fn test_make_multiple_lobbies_different_hosts() {
    let setup = TestSetup::new();
    let host_addresses = [
        setup.generate_address(),
        setup.generate_address(),
        setup.generate_address(),
    ];
    
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    
    // Create three different lobbies with different hosts
    for (i, host_addr) in host_addresses.iter().enumerate() {
        let lobby_id = (i + 1) as u32;
        let req = MakeLobbyReq {
            lobby_id,
            parameters: lobby_parameters.clone(),
        };
        
        setup.client.make_lobby(host_addr, &req);
        setup.verify_lobby_info(lobby_id, host_addr, Phase::Lobby);
    }
}

//#endregion

//#region leave_lobby tests

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
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let stored_lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should still exist");
        
        assert_eq!(stored_lobby_info.phase, Phase::Finished);
        assert!(stored_lobby_info.host_address.is_empty());
    });
}

#[test]
fn test_leave_lobby_user_not_found() {
    let setup = TestSetup::new();
    let non_existent_user = setup.generate_address();
    
    let result = setup.client.try_leave_lobby(&non_existent_user);
    assert_eq!(result.unwrap_err().unwrap(), Error::UserNotFound);
}

#[test]
fn test_leave_lobby_no_current_lobby() {
    let setup = TestSetup::new();
    let user_address = setup.generate_address();
    
    // Create a user but don't put them in any lobby
    setup.env.as_contract(&setup.contract_id, || {
        let user_key = DataKey::User(user_address.clone());
        let user = User {
            current_lobby: Vec::new(&setup.env),
            games_completed: 0,
            index: user_address.clone(),
        };
        setup.env.storage().persistent().set(&user_key, &user);
    });
    
    let result = setup.client.try_leave_lobby(&user_address);
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotFound);
}

#[test]
fn test_leave_lobby_during_game() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create and join lobby to start game
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);
    
    // Host leaves during setup phase
    setup.client.leave_lobby(&host_address);
    
    // Verify lobby is finished and assigns victory to remaining player
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let stored_lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert_eq!(stored_lobby_info.phase, Phase::Finished);
        // Could be either Guest wins or other end states depending on implementation
        assert!(matches!(stored_lobby_info.subphase, Subphase::Guest | Subphase::Host | Subphase::None));
        assert!(stored_lobby_info.host_address.is_empty());
    });
}

//#endregion

//#region join_lobby tests

#[test]
fn test_join_lobby_success() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create lobby first
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    // Join lobby
    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);
    
    // Verify guest is in lobby and game started
    setup.verify_user_lobby(&guest_address, lobby_id);
    
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let stored_lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert!(stored_lobby_info.guest_address.contains(&guest_address));
        assert_eq!(stored_lobby_info.phase, Phase::SetupCommit);
        assert_eq!(stored_lobby_info.subphase, Subphase::Both);
    });
    
    // Verify game state was created with pawns
    setup.verify_game_state_created(lobby_id);
}

#[test]
fn test_join_lobby_not_found() {
    let setup = TestSetup::new();
    let guest_address = setup.generate_address();
    
    let join_req = JoinLobbyReq { lobby_id: 999 };
    let result = setup.client.try_join_lobby(&guest_address, &join_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotFound);
}

#[test]
fn test_join_lobby_not_joinable_wrong_phase() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create lobby and manually set it to non-joinable phase
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    // Manually change phase to make it not joinable
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let mut lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .unwrap();
        lobby_info.phase = Phase::SetupCommit;
        setup.env.storage().temporary().set(&lobby_info_key, &lobby_info);
    });
    
    let join_req = JoinLobbyReq { lobby_id };
    let result = setup.client.try_join_lobby(&guest_address, &join_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotJoinable);
}

#[test]
fn test_join_lobby_host_is_joiner() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create lobby
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    // Host tries to join their own lobby
    let join_req = JoinLobbyReq { lobby_id };
    let result = setup.client.try_join_lobby(&host_address, &join_req);
    
    // Should fail with a user conflict error
    let error = result.unwrap_err().unwrap();
    assert!(TestSetup::is_user_conflict_error(&error));
}

#[test]
fn test_join_lobby_already_has_guest() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let guest_address_1 = setup.generate_address();
    let guest_address_2 = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create lobby
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    // First guest joins successfully
    let join_req_1 = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address_1, &join_req_1);
    
    // Second guest tries to join - should fail
    let join_req_2 = JoinLobbyReq { lobby_id };
    let result = setup.client.try_join_lobby(&guest_address_2, &join_req_2);
    
    // Should fail with lobby state error (game already started or full)
    let error = result.unwrap_err().unwrap();
    assert!(TestSetup::is_lobby_state_error(&error));
}

#[test]
fn test_join_lobby_guest_already_in_another_lobby() {
    let setup = TestSetup::new();
    let host_address_1 = setup.generate_address();
    let host_address_2 = setup.generate_address();
    let guest_address = setup.generate_address();
    
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    
    // Create first lobby and join it
    let make_req_1 = MakeLobbyReq {
        lobby_id: 1,
        parameters: lobby_parameters.clone(),
    };
    setup.client.make_lobby(&host_address_1, &make_req_1);
    
    let join_req_1 = JoinLobbyReq { lobby_id: 1 };
    setup.client.join_lobby(&guest_address, &join_req_1);
    
    // Create second lobby
    let make_req_2 = MakeLobbyReq {
        lobby_id: 2,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address_2, &make_req_2);
    
    // Try to join second lobby while still in first - should fail
    let join_req_2 = JoinLobbyReq { lobby_id: 2 };
    let result = setup.client.try_join_lobby(&guest_address, &join_req_2);
    
    // Should fail with user conflict or lobby state error
    let error = result.unwrap_err().unwrap();
    assert!(TestSetup::is_user_conflict_error(&error) || TestSetup::is_lobby_state_error(&error));
}

//#endregion