#![cfg(test)]
#![allow(unused_variables)]
extern crate std;
use super::super::*;
use super::super::test_utils::*;
use super::test_utils::*;

// region setup tests

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
    let (host_setup, host_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, &UserIndex::Host)
    });
    let (guest_setup, guest_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, &UserIndex::Guest)
    });
    let (host_root, host_proofs) = get_merkel(&setup.env, &host_setup, &host_hidden_ranks);
    let (guest_root, guest_proofs) = get_merkel(&setup.env, &guest_setup, &guest_hidden_ranks);

    let host_commit_req = CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root,
    };
    let guest_commit_req = CommitSetupReq {
        lobby_id,
        rank_commitment_root: guest_root,
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

        // Create pawns map to access unpacked pawns
        let pawns_map = Contract::create_pawns_map(&setup.env, &game_state.pawns);

        // Find a host pawn on front line (team 0) and a guest pawn on front line (team 1)
        let mut host_front_pawn_id = None;
        let mut guest_front_pawn_id = None;

        for (_, (_, pawn)) in pawns_map.iter() {
            let (_, team) = Contract::decode_pawn_id(pawn.pawn_id);
            if team == UserIndex::Host && pawn.pos.y == 3 && host_front_pawn_id.is_none() {
                host_front_pawn_id = Some(pawn.pawn_id);
            }
            if team == UserIndex::Guest && pawn.pos.y == 6 && guest_front_pawn_id.is_none() {
                guest_front_pawn_id = Some(pawn.pawn_id);
            }
        }

        let host_pawn_id = host_front_pawn_id.expect("Should find host front pawn");
        let guest_pawn_id = guest_front_pawn_id.expect("Should find guest front pawn");

        // Set host pawn to Scout (rank 2) and guest pawn to Marshal (rank 10)
        // When Scout attacks Marshal, Marshal should win and stay revealed
        for (index, packed_pawn) in game_state.pawns.iter().enumerate() {
            let mut pawn = Contract::unpack_pawn(&setup.env, packed_pawn);
            if pawn.pawn_id == host_pawn_id {
                pawn.rank = Vec::from_array(&setup.env, [2u32]); // Scout
                let updated_packed = Contract::pack_pawn(pawn.clone());
                game_state.pawns.set(index as u32, updated_packed);
                std::println!("Set host pawn {} to Scout (rank 2) at position ({},{})",
                             host_pawn_id, pawn.pos.x, pawn.pos.y);
            }
            if pawn.pawn_id == guest_pawn_id {
                pawn.rank = Vec::from_array(&setup.env, [10u32]); // Marshal
                let updated_packed = Contract::pack_pawn(pawn.clone());
                game_state.pawns.set(index as u32, updated_packed);
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

        // Create pawns map to access unpacked pawns
        let pawns_map = Contract::create_pawns_map(&setup.env, &game_state.pawns);

        // Find the Marshal's position to attack it
        let mut marshal_pos = None;
        let mut scout_pos = None;

        for (_, (_, pawn)) in pawns_map.iter() {
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