use crate::*;
#[cfg(test)]
mod unit_tests {
    extern crate std;
    use super::*;
    // region get_neighbors tests
    #[test]
    fn test_get_neighbors_square_and_hex_grids() {
        let test_cases = [
            (Pos { x: 5, y: 5 }, false, [
                Pos { x: 5, y: 4 },
                Pos { x: 6, y: 5 },
                Pos { x: 5, y: 6 },
                Pos { x: 4, y: 5 },
                Pos { x: -42069, y: -42069 },
                Pos { x: -42069, y: -42069 },
            ]),
            (Pos { x: 0, y: 0 }, false, [
                Pos { x: 0, y: -1 },
                Pos { x: 1, y: 0 },
                Pos { x: 0, y: 1 },
                Pos { x: -1, y: 0 },
                Pos { x: -42069, y: -42069 },
                Pos { x: -42069, y: -42069 },
            ]),
            (Pos { x: 2, y: 2 }, true, [
                Pos { x: 2, y: 3 },
                Pos { x: 1, y: 3 },
                Pos { x: 1, y: 2 },
                Pos { x: 2, y: 1 },
                Pos { x: 3, y: 2 },
                Pos { x: 3, y: 3 },
            ]),
            (Pos { x: 3, y: 3 }, true, [
                Pos { x: 3, y: 4 },
                Pos { x: 2, y: 3 },
                Pos { x: 2, y: 2 },
                Pos { x: 3, y: 2 },
                Pos { x: 4, y: 2 },
                Pos { x: 4, y: 3 },
            ]),
            (Pos { x: 0, y: 0 }, true, [
                Pos { x: 0, y: 1 },
                Pos { x: -1, y: 1 },
                Pos { x: -1, y: 0 },
                Pos { x: 0, y: -1 },
                Pos { x: 1, y: 0 },
                Pos { x: 1, y: 1 },
            ]),
            (Pos { x: 1, y: 1 }, true, [
                Pos { x: 1, y: 2 },
                Pos { x: 0, y: 1 },
                Pos { x: 0, y: 0 },
                Pos { x: 1, y: 0 },
                Pos { x: 2, y: 0 },
                Pos { x: 2, y: 1 },
            ]),
        ];
        for (pos, is_hex, expected) in test_cases.iter() {
            let mut neighbors = [Pos { x: -42069, y: -42069 }; 6];
            Contract::get_neighbors(pos, *is_hex, &mut neighbors);
            for i in 0..6 {
                assert_eq!(neighbors[i].x, expected[i].x, "Position ({},{}) is_hex={} neighbor {} x mismatch", pos.x, pos.y, is_hex, i);
                assert_eq!(neighbors[i].y, expected[i].y, "Position ({},{}) is_hex={} neighbor {} y mismatch", pos.x, pos.y, is_hex, i);
            }
        }
    }
    // endregion
    // region validate_board tests
    fn create_baseline_valid_params(env: &Env) -> LobbyParameters {
        let tiles = Vec::from_array(env, [
            Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 1 },
            Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 1, setup_zone: 1 },
            Tile { pos: Pos { x: 2, y: 0 }, passable: true, setup: 2, setup_zone: 1 },
            Tile { pos: Pos { x: 0, y: 1 }, passable: true, setup: 2, setup_zone: 1 },
            Tile { pos: Pos { x: 1, y: 1 }, passable: true, setup: 2, setup_zone: 1 },
            Tile { pos: Pos { x: 2, y: 1 }, passable: true, setup: 2, setup_zone: 1 },
        ]);
        let mut packed_tiles = Vec::new(env);
        for tile in tiles.iter() {
            packed_tiles.push_back(crate::test_utils::pack_tile(&tile));
        }
        LobbyParameters {
            blitz_interval: 0,
            blitz_max_simultaneous_moves: 1,
            board: Board {
                hex: false,
                name: String::from_str(env, "Baseline"),
                size: Pos { x: 3, y: 2 },
                tiles: packed_tiles,
            },
            board_hash: BytesN::from_array(env, &[1u8; 16]),
            dev_mode: true,
            host_team: 0,
            max_ranks: Vec::from_array(env, [1u32, 0u32, 0u32, 0u32, 0u32, 0u32, 0u32, 0u32, 0u32, 0u32, 0u32, 0u32, 0u32]),
            must_fill_all_tiles: false,
            security_mode: true,
        }
    }
    #[test]
    fn test_validate_board_all_conditions() {
        let env = Env::default();
        let baseline = create_baseline_valid_params(&env);
        assert!(Contract::validate_parameters(&env, &baseline));
        let square_board = crate::test_utils::create_test_lobby_parameters(&env);
        assert!(Contract::validate_parameters(&env, &square_board));
        let hex_board = crate::test_utils::create_user_board_parameters(&env);
        assert!(Contract::validate_parameters(&env, &hex_board));
        let mut params = create_baseline_valid_params(&env);
        params.board.size = Pos { x: 2, y: 2 };
        assert!(!Contract::validate_parameters(&env, &params));
        let mut params = create_baseline_valid_params(&env);
        let mut tiles = Vec::new(&env);
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 1 }));
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 1, setup_zone: 1 }));
        params.board.tiles = tiles;
        params.board.size = Pos { x: 1, y: 2 };
        assert!(!Contract::validate_parameters(&env, &params));
        let mut params = create_baseline_valid_params(&env);
        let mut tiles = Vec::new(&env);
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 1 }));
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 1, setup_zone: 1 }));
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 2, y: 0 }, passable: true, setup: 3, setup_zone: 1 }));
        params.board.tiles = tiles;
        params.board.size = Pos { x: 3, y: 1 };
        assert!(!Contract::validate_parameters(&env, &params));
        let mut params = create_baseline_valid_params(&env);
        let mut tiles = Vec::new(&env);
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 1 }));
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 1, setup_zone: 1 }));
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 2, y: 0 }, passable: true, setup: 2, setup_zone: 5 }));
        params.board.tiles = tiles;
        params.board.size = Pos { x: 3, y: 1 };
        assert!(!Contract::validate_parameters(&env, &params));
        let mut params = create_baseline_valid_params(&env);
        let mut tiles = Vec::new(&env);
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 0, y: 0 }, passable: false, setup: 0, setup_zone: 1 }));
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 1, setup_zone: 1 }));
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 2, y: 0 }, passable: true, setup: 2, setup_zone: 1 }));
        params.board.tiles = tiles;
        params.board.size = Pos { x: 3, y: 1 };
        assert!(!Contract::validate_parameters(&env, &params));
        let mut params = create_baseline_valid_params(&env);
        let mut tiles = Vec::new(&env);
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 0, y: 0 }, passable: false, setup: 2, setup_zone: 1 }));
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 1, y: 0 }, passable: false, setup: 2, setup_zone: 1 }));
        params.board.tiles = tiles;
        params.board.size = Pos { x: 2, y: 1 };
        assert!(!Contract::validate_parameters(&env, &params));
        let mut params = create_baseline_valid_params(&env);
        let mut tiles = Vec::new(&env);
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 1, setup_zone: 1 }));
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 2, setup_zone: 1 }));
        params.board.tiles = tiles;
        params.board.size = Pos { x: 2, y: 1 };
        assert!(!Contract::validate_parameters(&env, &params));
        let mut params = create_baseline_valid_params(&env);
        let mut tiles = Vec::new(&env);
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 1 }));
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 2, setup_zone: 1 }));
        params.board.tiles = tiles;
        params.board.size = Pos { x: 2, y: 1 };
        assert!(!Contract::validate_parameters(&env, &params));
        let mut params = create_baseline_valid_params(&env);
        let board_size = 17;
        let mut tiles = Vec::new(&env);
        for y in 0..board_size {
            for x in 0..board_size {
                let setup = if x == 0 && y == 0 { 0 } else if x == 1 && y == 0 { 1 } else { 2 };
                tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x, y }, passable: true, setup, setup_zone: 1 }));
            }
        }
        params.board.tiles = tiles;
        params.board.size = Pos { x: board_size, y: board_size };
        assert!(!Contract::validate_parameters(&env, &params));
        let mut params = create_baseline_valid_params(&env);
        let mut tiles = Vec::new(&env);
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 1 }));
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 1, setup_zone: 1 }));
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 2, y: 0 }, passable: false, setup: 2, setup_zone: 1 }));
        tiles.push_back(crate::test_utils::pack_tile(&Tile { pos: Pos { x: 3, y: 0 }, passable: true, setup: 2, setup_zone: 1 }));
        params.board.tiles = tiles;
        params.board.size = Pos { x: 4, y: 1 };
        assert!(!Contract::validate_parameters(&env, &params));
    }
    // endregion
    // region verify_merkle_proof tests
    #[test]
    fn test_verify_merkle_proof_single_and_multiple() {
        let env = Env::default();
        let hidden_ranks = Vec::from_array(&env, [
            HiddenRank { pawn_id: 1, rank: 5, salt: 100 },
            HiddenRank { pawn_id: 2, rank: 7, salt: 200 },
            HiddenRank { pawn_id: 3, rank: 3, salt: 300 },
            HiddenRank { pawn_id: 4, rank: 10, salt: 400 },
        ]);
        let mut rank_hashes = Vec::new(&env);
        for hidden_rank in hidden_ranks.iter() {
            let serialized = hidden_rank.clone().to_xdr(&env);
            let full_hash = env.crypto().sha256(&serialized).to_bytes().to_array();
            let rank_hash = HiddenRankHash::from_array(&env, &full_hash[0..16].try_into().unwrap());
            rank_hashes.push_back(rank_hash);
        }
        let (root, tree) = crate::test_utils::build_merkle_tree(&env, rank_hashes.clone());
        for (i, (hidden_rank, expected_hash)) in hidden_ranks.iter().zip(rank_hashes.iter()).enumerate() {
            let proof = tree.generate_proof(&env, i as u32);
            let serialized = hidden_rank.clone().to_xdr(&env);
            let full_hash = env.crypto().sha256(&serialized).to_bytes().to_array();
            let calculated_hash = HiddenRankHash::from_array(&env, &full_hash[0..16].try_into().unwrap());
            assert_eq!(calculated_hash, expected_hash);
            let is_valid = Contract::verify_merkle_proof(&env, &calculated_hash, &proof, &root);
            assert!(is_valid);
        }
        let hidden_rank = HiddenRank { pawn_id: 100, rank: 5, salt: 1234 };
        let hidden_rank2 = HiddenRank{ pawn_id: 101, rank: 7, salt: 1234 };
        let serialized = hidden_rank.clone().to_xdr(&env);
        let full_hash = env.crypto().sha256(&serialized).to_bytes().to_array();
        let rank_hash = HiddenRankHash::from_array(&env, &full_hash[0..16].try_into().unwrap());
        let serialized2 = hidden_rank2.clone().to_xdr(&env);
        let full_hash2 = env.crypto().sha256(&serialized2).to_bytes().to_array();
        let rank_hash2 = HiddenRankHash::from_array(&env, &full_hash2[0..16].try_into().unwrap());
        let leaves = Vec::from_array(&env, [rank_hash.clone(), rank_hash2.clone()]);
        let (root, tree) = crate::test_utils::build_merkle_tree(&env, leaves);
        let proof = tree.generate_proof(&env, 0);
        let proof2 = tree.generate_proof(&env, 1);
        let is_valid = Contract::verify_merkle_proof(&env, &rank_hash, &proof, &root);
        assert!(is_valid);
        let is_valid2 = Contract::verify_merkle_proof(&env, &rank_hash2, &proof2, &root);
        assert!(is_valid2);
    }
    // endregion
// region encode_pawn_id/decode_pawn_id tests
#[test]
fn test_encode_decode_pawn_id() {
    let test_cases = [
        (Pos { x: 0, y: 0 }, 0u32),
        (Pos { x: 0, y: 0 }, 1u32),
        (Pos { x: 15, y: 15 }, 0u32),
        (Pos { x: 15, y: 15 }, 1u32),
        (Pos { x: 5, y: 3 }, 0u32),
        (Pos { x: 5, y: 6 }, 1u32),
        (Pos { x: 7, y: 3 }, 1u32),
    ];
    for (pos, user_index) in test_cases.iter() {
        let encoded = Contract::encode_pawn_id(*pos, *user_index);
        let (decoded_pos, decoded_user) = Contract::decode_pawn_id(encoded);
        assert_eq!(decoded_pos.x, pos.x);
        assert_eq!(decoded_pos.y, pos.y);
        assert_eq!(decoded_user as u32, *user_index);
    }
    let pos = Pos { x: 7, y: 3 };
    let user_index = 1u32;
    let encoded = Contract::encode_pawn_id(pos, user_index);
    assert_eq!(encoded & 1, 1);
    assert_eq!((encoded >> 1) & 0xF, 7);
    assert_eq!((encoded >> 5) & 0xF, 3);
}
// endregion
// region pack_pawn/unpack_pawn tests
#[test]
fn test_pack_unpack_pawn() {
    let env = Env::default();
    
    // Test all possible ranks (0-11) with various flag combinations
    let mut test_cases = Vec::new(&env);
    
    // Add basic test cases with different flags
    for rank in 0u32..=11u32 {
        test_cases.push_back(PawnState {
            pawn_id: rank * 10,
            alive: true,
            moved: false,
            moved_scout: false,
            pos: Pos { x: (rank % 16) as i32, y: (rank / 16) as i32 },
            rank: Vec::from_array(&env, [rank]),
            zz_revealed: false,
        });
    }
    
    // Critical test case: rank 9 with zz_revealed=true (the bug we fixed)
    test_cases.push_back(PawnState {
        pawn_id: 305,
        alive: true,
        moved: true,
        moved_scout: false,
        pos: Pos { x: 8, y: 9 },
        rank: Vec::from_array(&env, [9u32]),
        zz_revealed: true,
    });
    
    // Test all ranks with zz_revealed=true
    for rank in 0u32..=11u32 {
        test_cases.push_back(PawnState {
            pawn_id: 400 + rank,
            alive: true,
            moved: true,
            moved_scout: false,
            pos: Pos { x: (rank % 8) as i32, y: (rank % 8) as i32 },
            rank: Vec::from_array(&env, [rank]),
            zz_revealed: true,
        });
    }
    
    // Edge cases
    test_cases.push_back(PawnState {
        pawn_id: 511,  // max pawn_id (9 bits)
        alive: true,
        moved: true,
        moved_scout: true,
        pos: Pos { x: 15, y: 15 },  // max coordinates
        rank: Vec::from_array(&env, [11u32]),  // max rank
        zz_revealed: true,
    });
    
    test_cases.push_back(PawnState {
        pawn_id: 0,
        alive: false,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::new(&env),  // no rank
        zz_revealed: false,
    });
    
    // Test all cases
    for original_pawn in test_cases.iter() {
        let packed = Contract::pack_pawn(original_pawn.clone());
        let unpacked = Contract::unpack_pawn(&env, packed);
        
        // Check complete equality
        assert_eq!(unpacked.pawn_id, original_pawn.pawn_id, 
            "pawn_id mismatch for pawn {}", original_pawn.pawn_id);
        assert_eq!(unpacked.alive, original_pawn.alive,
            "alive mismatch for pawn {}", original_pawn.pawn_id);
        assert_eq!(unpacked.moved, original_pawn.moved,
            "moved mismatch for pawn {}", original_pawn.pawn_id);
        assert_eq!(unpacked.moved_scout, original_pawn.moved_scout,
            "moved_scout mismatch for pawn {}", original_pawn.pawn_id);
        assert_eq!(unpacked.pos.x, original_pawn.pos.x,
            "pos.x mismatch for pawn {}", original_pawn.pawn_id);
        assert_eq!(unpacked.pos.y, original_pawn.pos.y,
            "pos.y mismatch for pawn {}", original_pawn.pawn_id);
        assert_eq!(unpacked.zz_revealed, original_pawn.zz_revealed,
            "zz_revealed mismatch for pawn {}", original_pawn.pawn_id);
        
        // Check rank
        if original_pawn.rank.is_empty() {
            assert!(unpacked.rank.is_empty(),
                "rank should be empty for pawn {}", original_pawn.pawn_id);
        } else {
            assert_eq!(unpacked.rank.len(), original_pawn.rank.len(),
                "rank length mismatch for pawn {}", original_pawn.pawn_id);
            assert_eq!(unpacked.rank.get(0).unwrap(), original_pawn.rank.get(0).unwrap(),
                "rank value mismatch for pawn {}: expected {}, got {}", 
                original_pawn.pawn_id, original_pawn.rank.get(0).unwrap(), unpacked.rank.get(0).unwrap());
        }
    }
    
    // Test bit layout to ensure no overlaps
    let test_pawn = PawnState {
        pawn_id: 0x1FF,  // 9 bits all 1s
        alive: true,
        moved: true,
        moved_scout: true,
        pos: Pos { x: 0xF, y: 0xF },  // 4 bits each all 1s
        rank: Vec::from_array(&env, [0xBu32]),  // 4 bits: 1011 (11)
        zz_revealed: true,
    };
    let packed = Contract::pack_pawn(test_pawn);
    
    // Verify bit positions don't overlap
    assert_eq!(packed & 0x1FF, 0x1FF, "pawn_id bits incorrect");
    assert_eq!((packed >> 9) & 1, 1, "alive bit incorrect");
    assert_eq!((packed >> 10) & 1, 1, "moved bit incorrect");
    assert_eq!((packed >> 11) & 1, 1, "moved_scout bit incorrect");
    assert_eq!((packed >> 12) & 0xF, 0xF, "x coordinate bits incorrect");
    assert_eq!((packed >> 16) & 0xF, 0xF, "y coordinate bits incorrect");
    assert_eq!((packed >> 20) & 0xF, 0xB, "rank bits incorrect");
    assert_eq!((packed >> 24) & 1, 1, "zz_revealed bit incorrect");
    
    // Verify empty rank encoding
    let empty_rank_pawn = PawnState {
        pawn_id: 50,
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 2, y: 3 },
        rank: Vec::new(&env),
        zz_revealed: false,
    };
    let packed = Contract::pack_pawn(empty_rank_pawn.clone());
    let rank_bits = (packed >> 20) & 0xF;
    assert_eq!(rank_bits, 12, "empty rank should encode as 12");
}
// endregion
// region resolve_collision tests
#[test]
fn test_resolve_collision_all_scenarios() {
    let env = Env::default();
    let mut higher_rank = PawnState {
        pawn_id: 1,
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::from_array(&env, [8u32]),
        zz_revealed: false,
    };
    let mut lower_rank = PawnState {
        pawn_id: 2,
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::from_array(&env, [4u32]),
        zz_revealed: false,
    };
    Contract::resolve_collision(&mut higher_rank, &mut lower_rank);
    assert!(higher_rank.alive);
    assert!(!lower_rank.alive);
    let mut pawn_a = PawnState {
        pawn_id: 1,
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::from_array(&env, [5u32]),
        zz_revealed: false,
    };
    let mut pawn_b = PawnState {
        pawn_id: 2,
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::from_array(&env, [5u32]),
        zz_revealed: false,
    };
    Contract::resolve_collision(&mut pawn_a, &mut pawn_b);
    assert!(!pawn_a.alive);
    assert!(!pawn_b.alive);
    let mut assassin = PawnState {
        pawn_id: 1,
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::from_array(&env, [1u32]),
        zz_revealed: false,
    };
    let mut warlord = PawnState {
        pawn_id: 2,
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::from_array(&env, [10u32]),
        zz_revealed: false,
    };
    Contract::resolve_collision(&mut assassin, &mut warlord);
    assert!(assassin.alive);
    assert!(!warlord.alive);
    let mut assassin2 = PawnState {
        pawn_id: 3,
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::from_array(&env, [1u32]),
        zz_revealed: false,
    };
    let mut warlord2 = PawnState {
        pawn_id: 4,
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::from_array(&env, [10u32]),
        zz_revealed: false,
    };
    Contract::resolve_collision(&mut warlord2, &mut assassin2);
    assert!(assassin2.alive);
    assert!(!warlord2.alive);
    let mut seer = PawnState {
        pawn_id: 1,
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::from_array(&env, [3u32]),
        zz_revealed: false,
    };
    let mut trap = PawnState {
        pawn_id: 2,
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::from_array(&env, [11u32]),
        zz_revealed: false,
    };
    Contract::resolve_collision(&mut seer, &mut trap);
    assert!(seer.alive);
    assert!(!trap.alive);
    let mut seer2 = PawnState {
        pawn_id: 3,
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::from_array(&env, [3u32]),
        zz_revealed: false,
    };
    let mut trap2 = PawnState {
        pawn_id: 4,
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::from_array(&env, [11u32]),
        zz_revealed: false,
    };
    Contract::resolve_collision(&mut trap2, &mut seer2);
    assert!(seer2.alive);
    assert!(!trap2.alive);
}
// endregion
// region is_scout_move tests
#[test]
fn test_is_scout_move_distance_detection() {
    let single_step_moves = [
        HiddenMove { pawn_id: 1, salt: 123, start_pos: Pos { x: 5, y: 5 }, target_pos: Pos { x: 6, y: 5 } },
        HiddenMove { pawn_id: 1, salt: 123, start_pos: Pos { x: 5, y: 5 }, target_pos: Pos { x: 5, y: 6 } },
        HiddenMove { pawn_id: 1, salt: 123, start_pos: Pos { x: 5, y: 5 }, target_pos: Pos { x: 4, y: 4 } },
        HiddenMove { pawn_id: 1, salt: 123, start_pos: Pos { x: 5, y: 5 }, target_pos: Pos { x: 5, y: 5 } },
    ];
    for mv in single_step_moves.iter() {
        assert!(!Contract::is_scout_move(mv));
    }
    let multi_step_moves = [
        HiddenMove { pawn_id: 1, salt: 123, start_pos: Pos { x: 5, y: 5 }, target_pos: Pos { x: 7, y: 5 } },
        HiddenMove { pawn_id: 1, salt: 123, start_pos: Pos { x: 5, y: 5 }, target_pos: Pos { x: 5, y: 8 } },
        HiddenMove { pawn_id: 1, salt: 123, start_pos: Pos { x: 5, y: 5 }, target_pos: Pos { x: 3, y: 2 } },
        HiddenMove { pawn_id: 1, salt: 123, start_pos: Pos { x: 0, y: 0 }, target_pos: Pos { x: 9, y: 0 } },
    ];
    for mv in multi_step_moves.iter() {
        assert!(Contract::is_scout_move(mv));
    }
}
// endregion
// region subphase management tests
#[test]
fn test_subphase_transitions_and_validation() {
    assert_eq!(Contract::user_subphase_from_player_index(UserIndex::Host), Subphase::Host);
    assert_eq!(Contract::user_subphase_from_player_index(UserIndex::Guest), Subphase::Guest);
    let result_host = Contract::next_subphase(&Subphase::Both, UserIndex::Host);
    assert_eq!(result_host.unwrap(), Subphase::Guest);
    let result_guest = Contract::next_subphase(&Subphase::Both, UserIndex::Guest);
    assert_eq!(result_guest.unwrap(), Subphase::Host);
    let result_host = Contract::next_subphase(&Subphase::Host, UserIndex::Host);
    assert_eq!(result_host.unwrap(), Subphase::None);
    let result_guest = Contract::next_subphase(&Subphase::Guest, UserIndex::Guest);
    assert_eq!(result_guest.unwrap(), Subphase::None);
    let result = Contract::next_subphase(&Subphase::Host, UserIndex::Guest);
    assert_eq!(result.unwrap_err(), Error::WrongSubphase);
    let result = Contract::next_subphase(&Subphase::Guest, UserIndex::Host);
    assert_eq!(result.unwrap_err(), Error::WrongSubphase);
    let result = Contract::next_subphase(&Subphase::None, UserIndex::Host);
    assert_eq!(result.unwrap_err(), Error::WrongSubphase);
}
// endregion
// region check_game_over tests
fn create_test_game_state(env: &Env, host_flag_alive: bool, guest_flag_alive: bool) -> GameState {
    let mut pawns = Vec::new(env);
    let host_flag_id = Contract::encode_pawn_id(Pos { x: 0, y: 0 }, 0);
    let guest_flag_id = Contract::encode_pawn_id(Pos { x: 0, y: 1 }, 1);
    let host_flag = PawnState {
        pawn_id: host_flag_id,
        alive: host_flag_alive,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 0 },
        rank: Vec::from_array(env, [0u32]),
        zz_revealed: false,
    };
    let guest_flag = PawnState {
        pawn_id: guest_flag_id,
        alive: guest_flag_alive,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 0, y: 1 },
        rank: Vec::from_array(env, [0u32]),
        zz_revealed: false,
    };
    let other_pawn = PawnState {
        pawn_id: Contract::encode_pawn_id(Pos { x: 1, y: 0 }, 0),
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 1, y: 0 },
        rank: Vec::from_array(env, [5u32]),
        zz_revealed: false,
    };
    pawns.push_back(Contract::pack_pawn(host_flag));
    pawns.push_back(Contract::pack_pawn(guest_flag));
    pawns.push_back(Contract::pack_pawn(other_pawn));
    GameState {
        moves: Contract::create_empty_moves(env),
        pawns,
        rank_roots: Vec::new(env),
        turn: 1,
    }
}
#[test]
fn test_check_game_over_all_conditions() {
    let env = Env::default();
    let game_state = create_test_game_state(&env, true, true);
    let lobby_params = create_baseline_valid_params(&env);
    let result = Contract::check_game_over(&env, &game_state, &lobby_params);
    assert_eq!(result, Subphase::Both);
    let game_state = create_test_game_state(&env, true, false);
    let lobby_params = create_baseline_valid_params(&env);
    let result = Contract::check_game_over(&env, &game_state, &lobby_params);
    assert_eq!(result, Subphase::Host);
    let game_state = create_test_game_state(&env, false, true);
    let lobby_params = create_baseline_valid_params(&env);
    let result = Contract::check_game_over(&env, &game_state, &lobby_params);
    assert_eq!(result, Subphase::Guest);
    let game_state = create_test_game_state(&env, false, false);
    let lobby_params = create_baseline_valid_params(&env);
    let result = Contract::check_game_over(&env, &game_state, &lobby_params);
    assert_eq!(result, Subphase::None);
    let mut pawns = Vec::new(&env);
    let other_pawn = PawnState {
        pawn_id: Contract::encode_pawn_id(Pos { x: 1, y: 0 }, 0),
        alive: true,
        moved: false,
        moved_scout: false,
        pos: Pos { x: 1, y: 0 },
        rank: Vec::from_array(&env, [5u32]),
        zz_revealed: false,
    };
    pawns.push_back(Contract::pack_pawn(other_pawn));
    let game_state = GameState {
        moves: Contract::create_empty_moves(&env),
        pawns,
        rank_roots: Vec::new(&env),
        turn: 1,
    };
    let lobby_params = create_baseline_valid_params(&env);
    let result = Contract::check_game_over(&env, &game_state, &lobby_params);
    assert_eq!(result, Subphase::Both);
}
// endregion
}