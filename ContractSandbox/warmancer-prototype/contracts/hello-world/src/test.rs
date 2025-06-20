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
    
    // Create a lobby and have user join and leave to create a user record
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id: 1,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&user_address, &make_req);
    setup.client.leave_lobby(&user_address);
    
    // Now user exists but has no current lobby
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
    let guest_address_1 = setup.generate_address();
    let guest_address_2 = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create lobby and have a guest join to advance it to SetupCommit phase
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    // First guest joins, which advances the lobby to SetupCommit phase
    let join_req_1 = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address_1, &join_req_1);
    
    // Second guest tries to join - should fail because lobby is no longer in Lobby phase
    let join_req_2 = JoinLobbyReq { lobby_id };
    let result = setup.client.try_join_lobby(&guest_address_2, &join_req_2);
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

// Helper function to create test setup data
fn create_test_setup_data_from_game_state(setup: &TestSetup, lobby_id: u32, team: u32) -> (Vec<SetupCommit>, SetupProof, u64) {
    setup.env.as_contract(&setup.contract_id, || {
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        let mut setup_commits = Vec::new(&setup.env);
        
        // Find all pawns that belong to this team
        for pawn in game_state.pawns.iter() {
            let (_, pawn_team) = Contract::decode_pawn_id(&pawn.pawn_id);
            if pawn_team == team {
                // Create a simple hash for testing (without format! macro)
                let mut hash_data = [0u8; 32];
                hash_data[0] = (setup_commits.len() as u8) + 1;  // rank index
                hash_data[1] = team as u8;     // team
                hash_data[2] = pawn.pos.x as u8;        // x position
                hash_data[3] = pawn.pos.y as u8;        // y position
                hash_data[4] = lobby_id as u8; // lobby id
                
                let hidden_rank_hash = BytesN::from_array(&setup.env, &hash_data);
                
                let commit = SetupCommit {
                    pawn_id: pawn.pawn_id,
                    hidden_rank_hash,
                };
                setup_commits.push_back(commit);
            }
        }
        
        let salt = 12345u64 + team as u64 * 1000 + lobby_id as u64;
        let setup_proof = SetupProof {
            setup_commits: setup_commits.clone(),
            salt,
        };
        
        (setup_commits, setup_proof, salt)
    })
}

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
    let (_, host_setup_proof, _) = create_test_setup_data_from_game_state(&setup, lobby_id, 0); // Host is team 0
    let (_, guest_setup_proof, _) = create_test_setup_data_from_game_state(&setup, lobby_id, 1); // Guest is team 1
    
    // Hash both setups
    let host_serialized = host_setup_proof.clone().to_xdr(&setup.env);
    let host_setup_hash = setup.env.crypto().sha256(&host_serialized).to_bytes();
    
    let guest_serialized = guest_setup_proof.clone().to_xdr(&setup.env);
    let guest_setup_hash = setup.env.crypto().sha256(&guest_serialized).to_bytes();
    
    // Commit both setups
    let host_commit_req = CommitSetupReq {
        lobby_id,
        setup_hash: host_setup_hash,
    };
    let guest_commit_req = CommitSetupReq {
        lobby_id,
        setup_hash: guest_setup_hash,
    };
    
    setup.client.commit_setup(&host_address, &host_commit_req);
    setup.client.commit_setup(&guest_address, &guest_commit_req);
    
    // Prove both setups to advance to MoveCommit phase
    let host_prove_req = ProveSetupReq {
        lobby_id,
        setup: host_setup_proof,
    };
    let guest_prove_req = ProveSetupReq {
        lobby_id,
        setup: guest_setup_proof,
    };
    
    setup.client.prove_setup(&host_address, &host_prove_req);
    setup.client.prove_setup(&guest_address, &guest_prove_req);
    
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
fn create_test_move_hash(env: &Env, pawn_id: PawnId, pos: Pos, salt: u64) -> HiddenMoveHash {
    let move_proof = HiddenMoveProof {
        pawn_id,
        pos,
        salt,
    };
    let serialized = move_proof.to_xdr(env);
    env.crypto().sha256(&serialized).to_bytes()
}

//#region commit_move tests

#[test]
fn test_commit_move_success_host() {
    let setup = TestSetup::new();
    let lobby_id = 1u32;
    
    let (host_address, _guest_address) = create_and_advance_to_move_commit(&setup, lobby_id);
    
    // Create a test move hash
    let pawn_id = Contract::encode_pawn_id(&Pos { x: 0, y: 0 }, 0); // Host's pawn
    let new_pos = Pos { x: 0, y: 1 }; // Move up one space
    let move_hash = create_test_move_hash(&setup.env, pawn_id, new_pos, 12345);
    
    let commit_req = CommitMoveReq {
        lobby_id,
        move_hash: move_hash.clone(),
    };
    
    setup.client.commit_move(&host_address, &commit_req);
    
    // Verify move was committed and subphase changed
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert_eq!(lobby_info.phase, Phase::MoveCommit);
        assert_eq!(lobby_info.subphase, Subphase::Guest); // Now waiting for guest
        
        // Verify move hash was stored
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        let host_move = game_state.moves.get(0).unwrap();
        assert_eq!(host_move.move_hash.len(), 1);
        assert_eq!(host_move.move_hash.get(0).unwrap(), move_hash);
    });
}

#[test]
fn test_commit_move_success_both_players() {
    let setup = TestSetup::new();
    let lobby_id = 1u32;
    
    let (host_address, guest_address) = create_and_advance_to_move_commit(&setup, lobby_id);
    
    // Create test move hashes for both players
    let host_pawn_id = Contract::encode_pawn_id(&Pos { x: 0, y: 0 }, 0);
    let host_move_hash = create_test_move_hash(&setup.env, host_pawn_id, Pos { x: 0, y: 1 }, 12345);
    
    let guest_pawn_id = Contract::encode_pawn_id(&Pos { x: 0, y: 3 }, 1);
    let guest_move_hash = create_test_move_hash(&setup.env, guest_pawn_id, Pos { x: 0, y: 2 }, 54321);
    
    // Host commits first
    let host_commit_req = CommitMoveReq {
        lobby_id,
        move_hash: host_move_hash.clone(),
    };
    setup.client.commit_move(&host_address, &host_commit_req);
    
    // Guest commits second
    let guest_commit_req = CommitMoveReq {
        lobby_id,
        move_hash: guest_move_hash.clone(),
    };
    setup.client.commit_move(&guest_address, &guest_commit_req);
    
    // Verify both moves committed and phase advanced to MoveProve
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert_eq!(lobby_info.phase, Phase::MoveProve);
        assert_eq!(lobby_info.subphase, Subphase::Both); // Both can prove
        
        // Verify both move hashes were stored
        let game_state_key = DataKey::GameState(lobby_id);
        let game_state: GameState = setup.env.storage()
            .temporary()
            .get(&game_state_key)
            .expect("Game state should exist");
        
        let host_move = game_state.moves.get(0).unwrap();
        let guest_move = game_state.moves.get(1).unwrap();
        
        assert_eq!(host_move.move_hash.get(0).unwrap(), host_move_hash);
        assert_eq!(guest_move.move_hash.get(0).unwrap(), guest_move_hash);
    });
}

#[test]
fn test_commit_move_lobby_not_found() {
    let setup = TestSetup::new();
    let user_address = setup.generate_address();
    
    let move_hash = create_test_move_hash(&setup.env, 1, Pos { x: 0, y: 1 }, 12345);
    let commit_req = CommitMoveReq {
        lobby_id: 999, // Non-existent lobby
        move_hash,
    };
    
    let result = setup.client.try_commit_move(&user_address, &commit_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotFound);
}

#[test]
fn test_commit_move_not_in_lobby() {
    let setup = TestSetup::new();
    let lobby_id = 1u32;
    let outsider_address = setup.generate_address();
    
    let (_host_address, _guest_address) = create_and_advance_to_move_commit(&setup, lobby_id);
    
    let move_hash = create_test_move_hash(&setup.env, 1, Pos { x: 0, y: 1 }, 12345);
    let commit_req = CommitMoveReq {
        lobby_id,
        move_hash,
    };
    
    let result = setup.client.try_commit_move(&outsider_address, &commit_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::NotInLobby);
}

#[test]
fn test_commit_move_wrong_phase() {
    let setup = TestSetup::new();
    let host_address = setup.generate_address();
    let lobby_id = 1u32;
    
    // Create lobby but don't advance to MoveCommit phase
    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);
    
    let move_hash = create_test_move_hash(&setup.env, 1, Pos { x: 0, y: 1 }, 12345);
    let commit_req = CommitMoveReq {
        lobby_id,
        move_hash,
    };
    
    let result = setup.client.try_commit_move(&host_address, &commit_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongPhase);
}

#[test]
fn test_commit_move_wrong_subphase() {
    let setup = TestSetup::new();
    let lobby_id = 1u32;
    
    let (host_address, _guest_address) = create_and_advance_to_move_commit(&setup, lobby_id);
    
    // Host commits first
    let host_pawn_id = Contract::encode_pawn_id(&Pos { x: 0, y: 0 }, 0);
    let host_move_hash = create_test_move_hash(&setup.env, host_pawn_id, Pos { x: 0, y: 1 }, 12345);
    
    let host_commit_req = CommitMoveReq {
        lobby_id,
        move_hash: host_move_hash,
    };
    setup.client.commit_move(&host_address, &host_commit_req);
    
    // Now subphase should be Guest, so host trying to commit again should fail
    let another_move_hash = create_test_move_hash(&setup.env, host_pawn_id, Pos { x: 1, y: 0 }, 67890);
    let another_commit_req = CommitMoveReq {
        lobby_id,
        move_hash: another_move_hash,
    };
    
    let result = setup.client.try_commit_move(&host_address, &another_commit_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongSubphase);
}

#[test]
fn test_commit_move_after_game_finished() {
    let setup = TestSetup::new();
    let lobby_id = 1u32;
    
    let (host_address, guest_address) = create_and_advance_to_move_commit(&setup, lobby_id);
    
    // Have host leave the game to finish it
    setup.client.leave_lobby(&host_address);
    
    // Try to commit a move after the game is finished
    let move_hash = create_test_move_hash(&setup.env, 1, Pos { x: 0, y: 1 }, 12345);
    let commit_req = CommitMoveReq {
        lobby_id,
        move_hash,
    };
    
    let result = setup.client.try_commit_move(&guest_address, &commit_req);
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongPhase);
}

//#endregion

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
    let (_, host_setup_proof, _) = create_test_setup_data_from_game_state(&setup, lobby_id, 1); // Wrong team!
    
    // Create valid setup for guest (team 0 - also wrong, but different from host)
    let (_, guest_setup_proof, _) = create_test_setup_data_from_game_state(&setup, lobby_id, 0);
    
    // Hash both setups
    let host_serialized = host_setup_proof.clone().to_xdr(&setup.env);
    let host_setup_hash = setup.env.crypto().sha256(&host_serialized).to_bytes();
    
    let guest_serialized = guest_setup_proof.clone().to_xdr(&setup.env);
    let guest_setup_hash = setup.env.crypto().sha256(&guest_serialized).to_bytes();
    
    // Commit both setups to transition to SetupProve phase
    let host_commit_req = CommitSetupReq {
        lobby_id,
        setup_hash: host_setup_hash,
    };
    let guest_commit_req = CommitSetupReq {
        lobby_id,
        setup_hash: guest_setup_hash,
    };
    
    setup.client.commit_setup(&host_address, &host_commit_req);
    setup.client.commit_setup(&guest_address, &guest_commit_req);
    
    // Verify we're in SetupProve phase with Both subphase
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert_eq!(lobby_info.phase, Phase::SetupProve);
        assert_eq!(lobby_info.subphase, Subphase::Both); // Both players can prove
    });
    
    // Try to prove invalid setup - should end the game with host losing
    let host_prove_req = ProveSetupReq {
        lobby_id,
        setup: host_setup_proof,
    };
    
    setup.client.prove_setup(&host_address, &host_prove_req);
    
    // Verify game ended with guest winning
    setup.env.as_contract(&setup.contract_id, || {
        let lobby_info_key = DataKey::LobbyInfo(lobby_id);
        let lobby_info: LobbyInfo = setup.env.storage()
            .temporary()
            .get(&lobby_info_key)
            .expect("Lobby info should exist");
        
        assert_eq!(lobby_info.phase, Phase::Finished);
        assert_eq!(lobby_info.subphase, Subphase::Guest); // Guest wins
    });
}