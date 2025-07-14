#![cfg(test)]
#![allow(unused_variables)]
extern crate std;
use super::super::*;
use super::test_utils::*;

// region validation tests

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

#[test]
fn test_merkle_proof_verification() {
    let setup = TestSetup::new();
    
    // Create a few test HiddenRank structs
    let hidden_ranks = Vec::from_array(&setup.env, [
        HiddenRank { pawn_id: 1, rank: 5, salt: 100 },
        HiddenRank { pawn_id: 2, rank: 7, salt: 200 },
        HiddenRank { pawn_id: 3, rank: 3, salt: 300 },
        HiddenRank { pawn_id: 4, rank: 10, salt: 400 },
    ]);
    
    // Calculate hashes for each HiddenRank
    let mut rank_hashes = Vec::new(&setup.env);
    for hidden_rank in hidden_ranks.iter() {
        let serialized = hidden_rank.clone().to_xdr(&setup.env);
        let full_hash = setup.env.crypto().sha256(&serialized).to_bytes().to_array();
        let rank_hash = HiddenRankHash::from_array(&setup.env, &full_hash[0..16].try_into().unwrap());
        rank_hashes.push_back(rank_hash);
    }
    
    // Build merkle tree
    let (root, tree) = super::test_utils::build_merkle_tree(&setup.env, rank_hashes.clone());
    
    // Test each proof
    for (i, (hidden_rank, expected_hash)) in hidden_ranks.iter().zip(rank_hashes.iter()).enumerate() {
        let proof = tree.generate_proof(&setup.env, i as u32);
        
        // Recalculate the hash (as done in validate_rank_proofs)
        let serialized = hidden_rank.clone().to_xdr(&setup.env);
        let full_hash = setup.env.crypto().sha256(&serialized).to_bytes().to_array();
        let calculated_hash = HiddenRankHash::from_array(&setup.env, &full_hash[0..16].try_into().unwrap());
        
        // Verify the hash matches
        assert_eq!(calculated_hash, expected_hash, "Hash mismatch for pawn {}", hidden_rank.pawn_id);
        
        // Verify the merkle proof
        let is_valid = setup.env.as_contract(&setup.contract_id, || {
            Contract::verify_merkle_proof(&setup.env, &calculated_hash, &proof, &root)
        });
        
        assert!(is_valid, "Merkle proof verification failed for pawn {}", hidden_rank.pawn_id);
        std::println!("✓ Merkle proof verified for pawn {} (rank {})", hidden_rank.pawn_id, hidden_rank.rank);
    }
    
    std::println!("All merkle proofs verified successfully!");
}

#[test]
fn test_verify_merkle_proof_direct() {
    let setup = TestSetup::new();
    
    // Create test data
    let hidden_rank = HiddenRank { pawn_id: 100, rank: 5, salt: 1234 };
    let hidden_rank2 = HiddenRank{ pawn_id: 101, rank: 7, salt: 1234 };
    // Calculate hash
    let serialized = hidden_rank.clone().to_xdr(&setup.env);
    let full_hash = setup.env.crypto().sha256(&serialized).to_bytes().to_array();
    let rank_hash = HiddenRankHash::from_array(&setup.env, &full_hash[0..16].try_into().unwrap());
    let serialized2 = hidden_rank2.clone().to_xdr(&setup.env);
    let full_hash2 = setup.env.crypto().sha256(&serialized2).to_bytes().to_array();
    let rank_hash2 = HiddenRankHash::from_array(&setup.env, &full_hash2[0..16].try_into().unwrap());
    // Build merkle tree with this single leaf
    let leaves = Vec::from_array(&setup.env, [rank_hash.clone(), rank_hash2.clone()]);
    let (root, tree) = super::test_utils::build_merkle_tree(&setup.env, leaves);
    
    // Generate proof
    let proof = tree.generate_proof(&setup.env, 0);
    let proof2 = tree.generate_proof(&setup.env, 1);
    // Verify the proof
    let is_valid = setup.env.as_contract(&setup.contract_id, || {
        Contract::verify_merkle_proof(&setup.env, &rank_hash, &proof, &root)
    });
    
    assert!(is_valid, "Merkle proof should be valid");

    let is_valid2 = setup.env.as_contract(&setup.contract_id, || {
        Contract::verify_merkle_proof(&setup.env, &rank_hash2, &proof2, &root)
    });
    assert!(is_valid, "Merkle proof 2 should be valid");
    std::println!("✓ Direct merkle proof verification successful!");
}

#[test]
fn test_board_connectivity_connected() {
    let setup = TestSetup::new();
    let env = &setup.env;
    
    // Create a simple 3x3 connected board
    let tiles = Vec::from_array(env, [
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 0, y: 0 }, setup: 0, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 1, y: 0 }, setup: 0, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 2, y: 0 }, setup: 0, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 0, y: 1 }, setup: 0, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: false, pos: Pos { x: 1, y: 1 }, setup: 2, setup_zone: 0 }), // impassable center
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 2, y: 1 }, setup: 0, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 0, y: 2 }, setup: 1, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 1, y: 2 }, setup: 1, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 2, y: 2 }, setup: 1, setup_zone: 0 }),
    ]);
    
    let board = Board {
        hex: false,
        name: String::from_str(env, "Connected Board"),
        size: Pos { x: 3, y: 3 },
        tiles: tiles,
    };
    let lobby_parameters = LobbyParameters {
        board,
        board_hash: BytesN::from_array(env, &[1u8; 16]),
        dev_mode: false,
        host_team: 0,
        max_ranks: Vec::new(env),
        must_fill_all_tiles: false,
        security_mode: false,
    };
    let is_valid = env.as_contract(&setup.contract_id, || {
        Contract::validate_board(env, &lobby_parameters)
    });
    
    assert!(is_valid, "Connected board should be valid");
    std::println!("✓ Connected board validation passed");
}

#[test]
fn test_board_connectivity_disconnected() {
    let setup = TestSetup::new();
    let env = &setup.env;
    
    // Create a 3x3 board with two disconnected islands
    let tiles = Vec::from_array(env, [
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 0, y: 0 }, setup: 0, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 1, y: 0 }, setup: 0, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: false, pos: Pos { x: 2, y: 0 }, setup: 2, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 0, y: 1 }, setup: 0, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: false, pos: Pos { x: 1, y: 1 }, setup: 2, setup_zone: 0 }), // barrier
        crate::test_utils::pack_tile(&Tile { passable: false, pos: Pos { x: 2, y: 1 }, setup: 2, setup_zone: 0 }), // barrier
        crate::test_utils::pack_tile(&Tile { passable: false, pos: Pos { x: 0, y: 2 }, setup: 2, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 1, y: 2 }, setup: 1, setup_zone: 0 }), // isolated island
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 2, y: 2 }, setup: 1, setup_zone: 0 }), // isolated island
    ]);
    
    let board = Board {
        hex: false,
        name: String::from_str(env, "Disconnected Board"),
        size: Pos { x: 3, y: 3 },
        tiles: tiles,
    };
    let lobby_parameters = LobbyParameters {
        board,
        board_hash: BytesN::from_array(env, &[1u8; 16]),
        dev_mode: false,
        host_team: 0,
        max_ranks: Vec::new(env),
        must_fill_all_tiles: false,
        security_mode: false,
    };
    let is_valid = env.as_contract(&setup.contract_id, || {
        Contract::validate_board(env, &lobby_parameters)
    });
    
    assert!(!is_valid, "Disconnected board should be invalid");
    std::println!("✓ Disconnected board validation correctly failed");
}

#[test]
fn test_board_connectivity_hex() {
    let setup = TestSetup::new();
    let env = &setup.env;
    
    // Create a small hex board to test hex connectivity
    let tiles = Vec::from_array(env, [
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 0, y: 0 }, setup: 0, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 1, y: 0 }, setup: 0, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 0, y: 1 }, setup: 0, setup_zone: 0 }),
        crate::test_utils::pack_tile(&Tile { passable: true, pos: Pos { x: 1, y: 1 }, setup: 1, setup_zone: 0 }),
    ]);
    
    let board = Board {
        hex: true,
        name: String::from_str(env, "Hex Board"),
        size: Pos { x: 2, y: 2 },
        tiles: tiles,
    };
    let lobby_parameters = LobbyParameters {
        board,
        board_hash: BytesN::from_array(env, &[1u8; 16]),
        dev_mode: false,
        host_team: 0,
        max_ranks: Vec::new(env),
        must_fill_all_tiles: false,
        security_mode: false,
    };
    let is_valid = env.as_contract(&setup.contract_id, || {
        Contract::validate_board(env, &lobby_parameters)
    });
    
    assert!(is_valid, "Hex board should be valid");
    std::println!("✓ Hex board connectivity validation passed");
}

// endregion