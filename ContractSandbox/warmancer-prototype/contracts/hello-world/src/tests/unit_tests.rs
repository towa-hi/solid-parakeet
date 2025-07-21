use crate::*;

#[cfg(test)]
mod unit_tests {
    extern crate std;
    use super::*;

    // region get_neighbors tests

    #[test]
    fn test_get_neighbors_square_board_center() {
        // Test a position in the middle of a square board
        let pos = Pos { x: 5, y: 5 };
        let is_hex = false;
        
        let mut neighbors = [Pos { x: -42069, y: -42069 }; 6];
        Contract::get_neighbors(&pos, is_hex, &mut neighbors);
        
        // Expected neighbors for square board: N, E, S, W
        let expected = [
            Pos { x: 5, y: 4 },  // N (y-1)
            Pos { x: 6, y: 5 },  // E (x+1)
            Pos { x: 5, y: 6 },  // S (y+1)
            Pos { x: 4, y: 5 },  // W (x-1)
            Pos { x: -42069, y: -42069 },  // Unused
            Pos { x: -42069, y: -42069 },  // Unused
        ];
        
        std::println!("Testing square board neighbors for position ({}, {})", pos.x, pos.y);
        for i in 0..6 {
            std::println!("Neighbor[{}]: expected ({}, {}), got ({}, {})", 
                     i, expected[i].x, expected[i].y, neighbors[i].x, neighbors[i].y);
            assert_eq!(neighbors[i].x, expected[i].x, "Neighbor {} x mismatch", i);
            assert_eq!(neighbors[i].y, expected[i].y, "Neighbor {} y mismatch", i);
        }
    }

    #[test]
    fn test_get_neighbors_square_board_corners() {
        // Test corner position (0, 0)
        let pos = Pos { x: 0, y: 0 };
        let is_hex = false;
        
        let mut neighbors = [Pos { x: -42069, y: -42069 }; 6];
        Contract::get_neighbors(&pos, is_hex, &mut neighbors);
        
        // At (0,0), we should have: N at (0,-1), E at (1,0), S at (0,1), W at (-1,0)
        assert_eq!(neighbors[0], Pos { x: 0, y: -1 });  // N (out of bounds)
        assert_eq!(neighbors[1], Pos { x: 1, y: 0 });   // E
        assert_eq!(neighbors[2], Pos { x: 0, y: 1 });   // S
        assert_eq!(neighbors[3], Pos { x: -1, y: 0 });  // W (out of bounds)
        assert_eq!(neighbors[4], Pos { x: -42069, y: -42069 });  // Unused
        assert_eq!(neighbors[5], Pos { x: -42069, y: -42069 });  // Unused
    }

    #[test]
    fn test_get_neighbors_hex_even_column() {
        // Test position with even x coordinate
        let pos = Pos { x: 2, y: 2 };
        let is_hex = true;
        
        let mut neighbors = [Pos { x: -42069, y: -42069 }; 6];
        Contract::get_neighbors(&pos, is_hex, &mut neighbors);
        
        // For even columns in hex:
        let expected = [
            Pos { x: 2, y: 3 },   // top
            Pos { x: 1, y: 3 },   // top right
            Pos { x: 1, y: 2 },   // bot right
            Pos { x: 2, y: 1 },   // bot
            Pos { x: 3, y: 2 },   // bot left
            Pos { x: 3, y: 3 },   // top left
        ];
        
        std::println!("Testing hex even column neighbors for position ({}, {})", pos.x, pos.y);
        for i in 0..6 {
            std::println!("Neighbor[{}]: expected ({}, {}), got ({}, {})", 
                     i, expected[i].x, expected[i].y, neighbors[i].x, neighbors[i].y);
            assert_eq!(neighbors[i].x, expected[i].x, "Even column neighbor {} x mismatch", i);
            assert_eq!(neighbors[i].y, expected[i].y, "Even column neighbor {} y mismatch", i);
        }
    }

    #[test]
    fn test_get_neighbors_hex_odd_column() {
        // Test position with odd x coordinate
        let pos = Pos { x: 3, y: 3 };
        let is_hex = true;
        
        let mut neighbors = [Pos { x: -42069, y: -42069 }; 6];
        Contract::get_neighbors(&pos, is_hex, &mut neighbors);
        
        // For odd columns in hex:
        let expected = [
            Pos { x: 3, y: 4 },   // top
            Pos { x: 2, y: 3 },   // top right
            Pos { x: 2, y: 2 },   // bot right
            Pos { x: 3, y: 2 },   // bot
            Pos { x: 4, y: 2 },   // bot left
            Pos { x: 4, y: 3 },   // top left
        ];
        
        std::println!("Testing hex odd column neighbors for position ({}, {})", pos.x, pos.y);
        for i in 0..6 {
            std::println!("Neighbor[{}]: expected ({}, {}), got ({}, {})", 
                     i, expected[i].x, expected[i].y, neighbors[i].x, neighbors[i].y);
            assert_eq!(neighbors[i].x, expected[i].x, "Odd column neighbor {} x mismatch", i);
            assert_eq!(neighbors[i].y, expected[i].y, "Odd column neighbor {} y mismatch", i);
        }
    }

    #[test]
    fn test_get_neighbors_hex_specific_positions() {
        // Test several specific positions to ensure hex logic is correct
        
        // Test (0, 0) - even column
        let mut neighbors = [Pos { x: -42069, y: -42069 }; 6];
        Contract::get_neighbors(&Pos { x: 0, y: 0 }, true, &mut neighbors);
        assert_eq!(neighbors[0], Pos { x: 0, y: 1 });   // top
        assert_eq!(neighbors[1], Pos { x: -1, y: 1 });  // top right (out of bounds)
        assert_eq!(neighbors[2], Pos { x: -1, y: 0 });  // bot right (out of bounds)
        assert_eq!(neighbors[3], Pos { x: 0, y: -1 });  // bot (out of bounds)
        assert_eq!(neighbors[4], Pos { x: 1, y: 0 });   // bot left
        assert_eq!(neighbors[5], Pos { x: 1, y: 1 });   // top left
        
        // Test (1, 1) - odd column
        let mut neighbors = [Pos { x: -42069, y: -42069 }; 6];
        Contract::get_neighbors(&Pos { x: 1, y: 1 }, true, &mut neighbors);
        assert_eq!(neighbors[0], Pos { x: 1, y: 2 });   // top
        assert_eq!(neighbors[1], Pos { x: 0, y: 1 });   // top right
        assert_eq!(neighbors[2], Pos { x: 0, y: 0 });   // bot right
        assert_eq!(neighbors[3], Pos { x: 1, y: 0 });   // bot
        assert_eq!(neighbors[4], Pos { x: 2, y: 0 });   // bot left
        assert_eq!(neighbors[5], Pos { x: 2, y: 1 });   // top left
    }

    // endregion

    // region validate_board tests

    #[test]
    fn test_validate_board_valid_basic_board() {
        let env = Env::default();
        let parameters = crate::test_utils::create_test_lobby_parameters(&env);
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(result, "Default test board should be valid");
    }

    #[test]
    fn test_validate_board_tile_count_mismatch() {
        let env = Env::default();
        let mut parameters = crate::test_utils::create_test_lobby_parameters(&env);
        
        // Create board that claims to be 2x2 (4 tiles) but only has 3 tiles
        parameters.board.size = Pos { x: 2, y: 2 };
        // Keep default tiles which has 100 tiles, so count won't match
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(!result, "Board with tile count mismatch should be invalid");
    }

    #[test]
    fn test_validate_board_duplicate_positions() {
        let env = Env::default();
        let board_hash = BytesN::from_array(&env, &[1u8; 16]);
        
        // Create board with duplicate positions
        let tiles = Vec::from_array(&env, [
            Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 1 },
            Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 1, setup_zone: 1 }, // Duplicate position
        ]);
        let mut packed_tiles = Vec::new(&env);
        for tile in tiles.iter() {
            packed_tiles.push_back(crate::test_utils::pack_tile(&tile));
        }
        
        let board = Board {
            hex: false,
            name: String::from_str(&env, "Duplicate Position Board"),
            size: Pos { x: 1, y: 2 },
            tiles: packed_tiles,
        };
        
        let parameters = LobbyParameters {
            board,
            board_hash,
            dev_mode: true,
            host_team: 0,
            max_ranks: Vec::from_array(&env, [1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 0u32]),
            must_fill_all_tiles: false,
            security_mode: false,
        };
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(!result, "Board with duplicate positions should be invalid");
    }

    #[test]
    fn test_validate_board_invalid_setup_values() {
        let env = Env::default();
        let board_hash = BytesN::from_array(&env, &[1u8; 16]);
        
        // Create board with invalid setup value (3)
        let tiles = Vec::from_array(&env, [
            Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 1 },
            Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 3, setup_zone: 1 }, // Invalid setup
        ]);
        let mut packed_tiles = Vec::new(&env);
        for tile in tiles.iter() {
            packed_tiles.push_back(crate::test_utils::pack_tile(&tile));
        }
        
        let board = Board {
            hex: false,
            name: String::from_str(&env, "Invalid Setup Board"),
            size: Pos { x: 2, y: 1 },
            tiles: packed_tiles,
        };
        
        let parameters = LobbyParameters {
            board,
            board_hash,
            dev_mode: true,
            host_team: 0,
            max_ranks: Vec::from_array(&env, [1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 0u32]),
            must_fill_all_tiles: false,
            security_mode: false,
        };
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(!result, "Board with invalid setup values should be invalid");
    }

    #[test]
    fn test_validate_board_setup_tile_not_passable() {
        let env = Env::default();
        let board_hash = BytesN::from_array(&env, &[1u8; 16]);
        
        // Create board where setup tile is not passable
        let tiles = Vec::from_array(&env, [
            Tile { pos: Pos { x: 0, y: 0 }, passable: false, setup: 0, setup_zone: 1 }, // Setup tile not passable
            Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 1, setup_zone: 1 },
        ]);
        let mut packed_tiles = Vec::new(&env);
        for tile in tiles.iter() {
            packed_tiles.push_back(crate::test_utils::pack_tile(&tile));
        }
        
        let board = Board {
            hex: false,
            name: String::from_str(&env, "Non-passable Setup Board"),
            size: Pos { x: 2, y: 1 },
            tiles: packed_tiles,
        };
        
        let parameters = LobbyParameters {
            board,
            board_hash,
            dev_mode: true,
            host_team: 0,
            max_ranks: Vec::from_array(&env, [1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 0u32]),
            must_fill_all_tiles: false,
            security_mode: false,
        };
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(!result, "Board with non-passable setup tiles should be invalid");
    }

    #[test]
    fn test_validate_board_invalid_setup_zone() {
        let env = Env::default();
        let board_hash = BytesN::from_array(&env, &[1u8; 16]);
        
        // Create board with invalid setup_zone value (5)
        let tiles = Vec::from_array(&env, [
            Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 5 }, // Invalid setup_zone
            Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 1, setup_zone: 1 },
        ]);
        let mut packed_tiles = Vec::new(&env);
        for tile in tiles.iter() {
            packed_tiles.push_back(crate::test_utils::pack_tile(&tile));
        }
        
        let board = Board {
            hex: false,
            name: String::from_str(&env, "Invalid Setup Zone Board"),
            size: Pos { x: 2, y: 1 },
            tiles: packed_tiles,
        };
        
        let parameters = LobbyParameters {
            board,
            board_hash,
            dev_mode: true,
            host_team: 0,
            max_ranks: Vec::from_array(&env, [1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 0u32]),
            must_fill_all_tiles: false,
            security_mode: false,
        };
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(!result, "Board with invalid setup_zone values should be invalid");
    }

    #[test]
    fn test_validate_board_no_passable_tiles() {
        let env = Env::default();
        let board_hash = BytesN::from_array(&env, &[1u8; 16]);
        
        // Create board with no passable tiles
        let tiles = Vec::from_array(&env, [
            Tile { pos: Pos { x: 0, y: 0 }, passable: false, setup: 2, setup_zone: 1 },
            Tile { pos: Pos { x: 1, y: 0 }, passable: false, setup: 2, setup_zone: 1 },
        ]);
        let mut packed_tiles = Vec::new(&env);
        for tile in tiles.iter() {
            packed_tiles.push_back(crate::test_utils::pack_tile(&tile));
        }
        
        let board = Board {
            hex: false,
            name: String::from_str(&env, "No Passable Tiles Board"),
            size: Pos { x: 2, y: 1 },
            tiles: packed_tiles,
        };
        
        let parameters = LobbyParameters {
            board,
            board_hash,
            dev_mode: true,
            host_team: 0,
            max_ranks: Vec::from_array(&env, [1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 0u32]),
            must_fill_all_tiles: false,
            security_mode: false,
        };
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(!result, "Board with no passable tiles should be invalid");
    }

    #[test]
    fn test_validate_board_no_host_setup() {
        let env = Env::default();
        let board_hash = BytesN::from_array(&env, &[1u8; 16]);
        
        // Create board with no host setup tiles (setup = 0)
        let tiles = Vec::from_array(&env, [
            Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 1, setup_zone: 1 },
            Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 2, setup_zone: 1 },
        ]);
        let mut packed_tiles = Vec::new(&env);
        for tile in tiles.iter() {
            packed_tiles.push_back(crate::test_utils::pack_tile(&tile));
        }
        
        let board = Board {
            hex: false,
            name: String::from_str(&env, "No Host Setup Board"),
            size: Pos { x: 2, y: 1 },
            tiles: packed_tiles,
        };
        
        let parameters = LobbyParameters {
            board,
            board_hash,
            dev_mode: true,
            host_team: 0,
            max_ranks: Vec::from_array(&env, [1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 0u32]),
            must_fill_all_tiles: false,
            security_mode: false,
        };
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(!result, "Board with no host setup tiles should be invalid");
    }

    #[test]
    fn test_validate_board_no_guest_setup() {
        let env = Env::default();
        let board_hash = BytesN::from_array(&env, &[1u8; 16]);
        
        // Create board with no guest setup tiles (setup = 1)
        let tiles = Vec::from_array(&env, [
            Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 1 },
            Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 2, setup_zone: 1 },
        ]);
        let mut packed_tiles = Vec::new(&env);
        for tile in tiles.iter() {
            packed_tiles.push_back(crate::test_utils::pack_tile(&tile));
        }
        
        let board = Board {
            hex: false,
            name: String::from_str(&env, "No Guest Setup Board"),
            size: Pos { x: 2, y: 1 },
            tiles: packed_tiles,
        };
        
        let parameters = LobbyParameters {
            board,
            board_hash,
            dev_mode: true,
            host_team: 0,
            max_ranks: Vec::from_array(&env, [1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 0u32]),
            must_fill_all_tiles: false,
            security_mode: false,
        };
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(!result, "Board with no guest setup tiles should be invalid");
    }

    #[test]
    fn test_validate_board_disconnected_passable_tiles() {
        let env = Env::default();
        let board_hash = BytesN::from_array(&env, &[1u8; 16]);
        
        // Create board with disconnected passable areas
        let tiles = Vec::from_array(&env, [
            // First connected area
            Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 1 },
            Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 1, setup_zone: 1 },
            // Gap
            Tile { pos: Pos { x: 2, y: 0 }, passable: false, setup: 2, setup_zone: 1 },
            // Second disconnected area
            Tile { pos: Pos { x: 3, y: 0 }, passable: true, setup: 2, setup_zone: 1 },
        ]);
        let mut packed_tiles = Vec::new(&env);
        for tile in tiles.iter() {
            packed_tiles.push_back(crate::test_utils::pack_tile(&tile));
        }
        
        let board = Board {
            hex: false,
            name: String::from_str(&env, "Disconnected Board"),
            size: Pos { x: 4, y: 1 },
            tiles: packed_tiles,
        };
        
        let parameters = LobbyParameters {
            board,
            board_hash,
            dev_mode: true,
            host_team: 0,
            max_ranks: Vec::from_array(&env, [1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 0u32]),
            must_fill_all_tiles: false,
            security_mode: false,
        };
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(!result, "Board with disconnected passable areas should be invalid");
    }

    #[test]
    fn test_validate_board_oversized_board() {
        let env = Env::default();
        let board_hash = BytesN::from_array(&env, &[1u8; 16]);
        
        // Create board that exceeds MAX_BOARD_SIZE (256)
        let board_size = 17; // 17x17 = 289 > 256
        let mut tiles = Vec::new(&env);
        
        for y in 0..board_size {
            for x in 0..board_size {
                let setup = if y < 4 { 0 } else if y >= board_size - 4 { 1 } else { 2 };
                tiles.push_back(Tile {
                    pos: Pos { x, y },
                    passable: true,
                    setup,
                    setup_zone: 1,
                });
            }
        }
        
        let mut packed_tiles = Vec::new(&env);
        for tile in tiles.iter() {
            packed_tiles.push_back(crate::test_utils::pack_tile(&tile));
        }
        
        let board = Board {
            hex: false,
            name: String::from_str(&env, "Oversized Board"),
            size: Pos { x: board_size, y: board_size },
            tiles: packed_tiles,
        };
        
        let parameters = LobbyParameters {
            board,
            board_hash,
            dev_mode: true,
            host_team: 0,
            max_ranks: Vec::from_array(&env, [1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 0u32]),
            must_fill_all_tiles: false,
            security_mode: false,
        };
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(!result, "Oversized board should be invalid");
    }

    #[test]
    fn test_validate_board_valid_hex_board() {
        let env = Env::default();
        let parameters = crate::test_utils::create_user_board_parameters(&env);
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(result, "Valid hex board should pass validation");
    }

    #[test]
    fn test_validate_board_connected_complex_layout() {
        let env = Env::default();
        let board_hash = BytesN::from_array(&env, &[1u8; 16]);
        
        // Create a more complex but connected layout
        let tiles = Vec::from_array(&env, [
            // Bottom row - host setup
            Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 1 },
            Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 0, setup_zone: 1 },
            Tile { pos: Pos { x: 2, y: 0 }, passable: true, setup: 0, setup_zone: 1 },
            // Middle row - neutral with gaps
            Tile { pos: Pos { x: 0, y: 1 }, passable: true, setup: 2, setup_zone: 1 },
            Tile { pos: Pos { x: 1, y: 1 }, passable: false, setup: 2, setup_zone: 1 }, // Lake
            Tile { pos: Pos { x: 2, y: 1 }, passable: true, setup: 2, setup_zone: 1 },
            // Top row - guest setup
            Tile { pos: Pos { x: 0, y: 2 }, passable: true, setup: 1, setup_zone: 1 },
            Tile { pos: Pos { x: 1, y: 2 }, passable: true, setup: 1, setup_zone: 1 },
            Tile { pos: Pos { x: 2, y: 2 }, passable: true, setup: 1, setup_zone: 1 },
        ]);
        let mut packed_tiles = Vec::new(&env);
        for tile in tiles.iter() {
            packed_tiles.push_back(crate::test_utils::pack_tile(&tile));
        }
        
        let board = Board {
            hex: false,
            name: String::from_str(&env, "Complex Connected Board"),
            size: Pos { x: 3, y: 3 },
            tiles: packed_tiles,
        };
        
        let parameters = LobbyParameters {
            board,
            board_hash,
            dev_mode: true,
            host_team: 0,
            max_ranks: Vec::from_array(&env, [1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 0u32]),
            must_fill_all_tiles: false,
            security_mode: false,
        };
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(result, "Complex but connected board should be valid");
    }

    #[test]
    fn test_validate_board_minimal_valid_board() {
        let env = Env::default();
        let board_hash = BytesN::from_array(&env, &[1u8; 16]);
        
        // Minimal valid board: 2 tiles, one for each team
        let tiles = Vec::from_array(&env, [
            Tile { pos: Pos { x: 0, y: 0 }, passable: true, setup: 0, setup_zone: 1 },
            Tile { pos: Pos { x: 1, y: 0 }, passable: true, setup: 1, setup_zone: 1 },
        ]);
        let mut packed_tiles = Vec::new(&env);
        for tile in tiles.iter() {
            packed_tiles.push_back(crate::test_utils::pack_tile(&tile));
        }
        
        let board = Board {
            hex: false,
            name: String::from_str(&env, "Minimal Valid Board"),
            size: Pos { x: 2, y: 1 },
            tiles: packed_tiles,
        };
        
        let parameters = LobbyParameters {
            board,
            board_hash,
            dev_mode: true,
            host_team: 0,
            max_ranks: Vec::from_array(&env, [1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 1u32, 0u32]),
            must_fill_all_tiles: false,
            security_mode: false,
        };
        
        let result = Contract::validate_board(&env, &parameters);
        assert!(result, "Minimal valid board should pass validation");
    }

    // endregion
}