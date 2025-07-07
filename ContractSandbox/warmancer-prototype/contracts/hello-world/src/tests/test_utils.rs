#![cfg(test)]
extern crate std;
use super::super::*;
use super::super::test_utils::*;
use soroban_sdk::{Env, Address};
use soroban_sdk::testutils::Address as _;

pub struct TestSetup {
    pub env: Env,
    pub contract_id: Address,
    pub client: ContractClient<'static>,
}

impl TestSetup {
    pub fn new() -> Self {
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

    pub fn is_user_conflict_error(error: &Error) -> bool {
        matches!(error,
            Error::GuestAlreadyInLobby |
            Error::HostAlreadyInLobby |
            Error::JoinerIsHost |
            Error::NotInLobby
        )
    }

    pub fn is_lobby_state_error(error: &Error) -> bool {
        matches!(error,
            Error::LobbyNotJoinable |
            Error::WrongPhase |
            Error::LobbyNotFound |
            Error::LobbyHasNoHost
        )
    }

    pub fn is_validation_error(error: &Error) -> bool {
        matches!(error,
            Error::InvalidBoard |
            Error::InvalidArgs |
            Error::LobbyAlreadyExists
        )
    }

    pub fn generate_address(&self) -> Address {
        Address::generate(&self.env)
    }

    pub fn verify_lobby_info(&self, lobby_id: u32, expected_host: &Address, expected_phase: Phase) {
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

    pub fn verify_user_lobby(&self, user_address: &Address, expected_lobby_id: u32) {
        self.env.as_contract(&self.contract_id, || {
            let user_key = DataKey::User(user_address.clone());
            let stored_user: User = self.env.storage()
                .persistent()
                .get(&user_key)
                .expect("User should be stored");

            assert_eq!(stored_user.current_lobby, expected_lobby_id);
        });
    }

    pub fn verify_user_has_no_lobby(&self, user_address: &Address) {
        self.env.as_contract(&self.contract_id, || {
            let user_key = DataKey::User(user_address.clone());
            let stored_user: User = self.env.storage()
                .persistent()
                .get(&user_key)
                .expect("User should be stored");

            assert_eq!(stored_user.current_lobby, 0);
        });
    }

    pub fn verify_game_state_created(&self, lobby_id: u32) {
        self.env.as_contract(&self.contract_id, || {
            let game_state_key = DataKey::GameState(lobby_id);
            let stored_game_state: GameState = self.env.storage()
                .temporary()
                .get(&game_state_key)
                .expect("Game state should be created");

            assert!(!stored_game_state.pawns.is_empty());
        });
    }
}

pub struct Tree {
    pub leaves: Vec<MerkleHash>,
    pub levels: Vec<Vec<MerkleHash>>,
}

impl Tree {
    pub fn generate_proof(&self, env: &Env, leaf_index: u32) -> MerkleProof {
        std::println!("=== generate_proof START ===");
        std::println!("Leaf index: {}", leaf_index);
        std::println!("Tree levels: {}", self.levels.len());
        
        let mut siblings = Vec::new(env);
        let mut current_index = leaf_index;
        
        for level in 0..(self.levels.len() - 1) {
            let level_nodes = &self.levels.get(level).unwrap();
            let sibling_index = if current_index % 2 == 0 {
                current_index + 1
            } else {
                current_index - 1
            };
            
            std::println!("Level {}: current_index={}, sibling_index={}, level_nodes_count={}", 
                         level, current_index, sibling_index, level_nodes.len());
            
            let sibling_hash = level_nodes.get(sibling_index).unwrap();
            std::println!("  Adding sibling: {:?}", sibling_hash.to_array());
            siblings.push_back(sibling_hash);
            
            current_index = current_index / 2;
            std::println!("  Next level index: {}", current_index);
        }
        
        std::println!("Generated proof with {} siblings", siblings.len());
        for (i, sibling) in siblings.iter().enumerate() {
            std::println!("  Sibling {}: {:?}", i, sibling.to_array());
        }
        
        let proof = MerkleProof {
            leaf_index,
            siblings,
        };
        
        std::println!("=== generate_proof END ===");
        proof
    }
    
    pub fn root(&self) -> MerkleHash {
        let root_level = self.levels.get(self.levels.len() as u32 - 1).unwrap();
        root_level.get(0).unwrap()
    }
}

pub fn build_merkle_tree(env: &Env, leaves: Vec<BytesN<16>>) -> (BytesN<16>, Tree) {
    std::println!("=== build_merkle_tree START ===");
    std::println!("Number of leaves: {}", leaves.len());
    
    for (i, leaf) in leaves.iter().enumerate() {
        std::println!("Leaf {}: {:?}", i, leaf.to_array());
    }
    
    if leaves.is_empty() {
        let zero_root = MerkleHash::from_array(env, &[0u8; 16]);
        let tree = Tree {
            leaves: Vec::new(env),
            levels: Vec::from_array(env, [Vec::from_array(env, [zero_root.clone()])]),
        };
        std::println!("Empty tree, returning zero root: {:?}", zero_root.to_array());
        return (zero_root, tree);
    }
    
    let mut padded_leaves = leaves.clone();
    let empty_hash = MerkleHash::from_array(env, &[0u8; 16]);
    
    let mut target_size = 1;
    while target_size < leaves.len() {
        target_size *= 2;
    }
    
    while padded_leaves.len() < target_size {
        padded_leaves.push_back(empty_hash.clone());
    }
    
    std::println!("Padded from {} to {} leaves", leaves.len(), padded_leaves.len());
    
    let mut levels: Vec<Vec<MerkleHash>> = Vec::new(env);
    
    levels.push_back(padded_leaves.clone());
    std::println!("Level 0 (padded leaves): {} nodes", padded_leaves.len());
    
    let mut current_level = 0;
    while levels.get(current_level).unwrap().len() > 1 {
        let current_nodes = levels.get(current_level).unwrap();
        let mut next_level = Vec::new(env);
        
        std::println!("Processing level {}, nodes: {}", current_level, current_nodes.len());
        
        let mut i = 0;
        while i < current_nodes.len() {
            let left = current_nodes.get(i).unwrap();
            let right = current_nodes.get(i + 1).unwrap();
            
            std::println!("  Pair {}: left={:?}, right={:?}", i/2, left.to_array(), right.to_array());
            
            let mut combined_bytes = [0u8; 32];
            combined_bytes[0..16].copy_from_slice(&left.to_array());
            combined_bytes[16..32].copy_from_slice(&right.to_array());
            
            std::println!("  Combined bytes: {:?}", combined_bytes);
            
            let parent_full = env.crypto().sha256(&Bytes::from_array(env, &combined_bytes));
            let parent_bytes = parent_full.to_array();
            let parent_hash = MerkleHash::from_array(env, &parent_bytes[0..16].try_into().unwrap());
            
            std::println!("  Parent hash: {:?}", parent_hash.to_array());
            
            next_level.push_back(parent_hash);
            i += 2;
        }
        
        levels.push_back(next_level);
        current_level += 1;
        std::println!("Level {} complete, {} nodes", current_level, levels.get(current_level).unwrap().len());
    }
    
    let root = levels.get(levels.len() as u32 - 1).unwrap().get(0).unwrap();
    
    std::println!("Final root: {:?}", root.to_array());
    std::println!("Total levels: {}", levels.len());
    
    let tree = Tree {
        leaves: leaves.clone(),
        levels,
    };
    
    std::println!("=== build_merkle_tree END ===");
    (root, tree)
}

pub fn create_test_move_hash(env: &Env, pawn_id: PawnId, start_pos: Pos, target_pos: Pos, salt: u64) -> HiddenMoveHash {
    let move_proof = HiddenMove {
        pawn_id,
        start_pos,
        target_pos,
        salt,
    };
    let serialized = move_proof.to_xdr(env);
    let full_hash = env.crypto().sha256(&serialized).to_bytes().to_array();
    HiddenMoveHash::from_array(env, &full_hash[0..16].try_into().unwrap())
}

pub fn create_and_advance_to_move_commit(setup: &TestSetup, lobby_id: u32) -> (Address, Address) {
    let host_address = setup.generate_address();
    let guest_address = setup.generate_address();

    let lobby_parameters = create_test_lobby_parameters(&setup.env);
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);

    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);

    let (host_setup, host_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, 0)
    });
    let (guest_setup, guest_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, 1)
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

pub fn setup_lobby_for_commit_setup(setup: &TestSetup, lobby_id: u32) -> (u32, Address, Address) {
    let lobby_parameters = create_full_stratego_board_parameters(&setup.env);

    let host_address = setup.generate_address();
    let make_req = MakeLobbyReq {
        lobby_id,
        parameters: lobby_parameters,
    };
    setup.client.make_lobby(&host_address, &make_req);

    let guest_address = setup.generate_address();
    let join_req = JoinLobbyReq { lobby_id };
    setup.client.join_lobby(&guest_address, &join_req);

    (lobby_id, host_address, guest_address)
}

pub fn setup_lobby_for_commit_move(setup: &TestSetup, lobby_id: u32) -> (u32, Address, Address, Vec<HiddenRank>, Vec<HiddenRank>, Vec<MerkleProof>, Vec<MerkleProof>) {
    let (lobby_id, host_address, guest_address) = setup_lobby_for_commit_setup(setup, lobby_id);

    let (host_ranks, guest_ranks, host_merkle_proofs, guest_merkle_proofs) = advance_through_complete_setup_phase(setup, lobby_id, &host_address, &guest_address);

    (lobby_id, host_address, guest_address, host_ranks, guest_ranks, host_merkle_proofs, guest_merkle_proofs)
}

pub fn advance_through_complete_setup_phase(setup: &TestSetup, lobby_id: u32, host_address: &Address, guest_address: &Address) -> (Vec<HiddenRank>, Vec<HiddenRank>, Vec<MerkleProof>, Vec<MerkleProof>) {
    let (host_setup, host_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, 0)
    });

    let (guest_setup, guest_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, 1)
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

    (host_hidden_ranks, guest_hidden_ranks, host_proofs, guest_proofs)
}

pub fn validate_move_prove_transition(
    snapshot: &SnapshotFull,
    host_move_req: &ProveMoveReq,
    guest_move_req: &ProveMoveReq,
) -> (Phase, Subphase, Vec<PawnId>, Vec<PawnId>) {
    let host_move = snapshot.game_state.moves.get(0).unwrap();
    let guest_move = snapshot.game_state.moves.get(1).unwrap();
    let host_needed_ranks = host_move.needed_rank_proofs.clone();
    let guest_needed_ranks = guest_move.needed_rank_proofs.clone();

    std::println!("=== POST-MOVEPROVE VALIDATION ===");
    std::println!("Phase: {:?}, Subphase: {:?}", snapshot.lobby_info.phase, snapshot.lobby_info.subphase);
    std::println!("✓ Host move submitted: {} from ({},{}) to ({},{})",
                 host_move_req.move_proof.pawn_id,
                 host_move_req.move_proof.start_pos.x, host_move_req.move_proof.start_pos.y,
                 host_move_req.move_proof.target_pos.x, host_move_req.move_proof.target_pos.y);
    std::println!("✓ Guest move submitted: {} from ({},{}) to ({},{})",
                 guest_move_req.move_proof.pawn_id,
                 guest_move_req.move_proof.start_pos.x, guest_move_req.move_proof.start_pos.y,
                 guest_move_req.move_proof.target_pos.x, guest_move_req.move_proof.target_pos.y);

    assert!(snapshot.game_state.moves.len() >= 2, "Game state should have moves for both players");
    std::println!("✓ Move processing completed successfully");
    std::println!("Host needed rank proofs: {} pawns", host_needed_ranks.len());
    for pawn_id in host_needed_ranks.iter() {
        let (_, team) = Contract::decode_pawn_id(&pawn_id);
        std::println!("  - Pawn {} (team {})", pawn_id, team);
    }
    std::println!("Guest needed rank proofs: {} pawns", guest_needed_ranks.len());
    for pawn_id in guest_needed_ranks.iter() {
        let (_, team) = Contract::decode_pawn_id(&pawn_id);
        std::println!("  - Pawn {} (team {})", pawn_id, team);
    }
    std::println!("=== END VALIDATION ===");

    (snapshot.lobby_info.phase.clone(), snapshot.lobby_info.subphase.clone(), host_needed_ranks, guest_needed_ranks)
}

pub fn validate_rank_prove_transition(
    snapshot: &SnapshotFull,
    host_rank_req: Option<&ProveRankReq>,
    guest_rank_req: Option<&ProveRankReq>,
) -> (Phase, Subphase, Vec<PawnId>, Vec<PawnId>) {
    std::println!("=== POST-RANKPROVE VALIDATION ===");
    std::println!("After rank proving: Phase={:?}, Subphase={:?}", snapshot.lobby_info.phase, snapshot.lobby_info.subphase);

    if let Some(host_req) = host_rank_req {
        std::println!("✓ Validating host rank proofs...");
        for hidden_rank in host_req.hidden_ranks.iter() {
            let pawn = snapshot.game_state.pawns.iter()
                .find(|p| p.pawn_id == hidden_rank.pawn_id)
                .expect(&std::format!("Host pawn {} should exist", hidden_rank.pawn_id));

            assert!(!pawn.rank.is_empty(), "Host pawn {} should have rank revealed", hidden_rank.pawn_id);
            assert_eq!(pawn.rank.get(0).unwrap(), hidden_rank.rank, "Host pawn {} rank should match submitted proof", hidden_rank.pawn_id);

            let rank_str = rank_to_string(hidden_rank.rank);
            std::println!("  ✓ Host pawn {} rank validated: {}", hidden_rank.pawn_id, rank_str);
        }
    }

    if let Some(guest_req) = guest_rank_req {
        std::println!("✓ Validating guest rank proofs...");
        for hidden_rank in guest_req.hidden_ranks.iter() {
            let pawn = snapshot.game_state.pawns.iter()
                .find(|p| p.pawn_id == hidden_rank.pawn_id)
                .expect(&std::format!("Guest pawn {} should exist", hidden_rank.pawn_id));

            assert!(!pawn.rank.is_empty(), "Guest pawn {} should have rank revealed", hidden_rank.pawn_id);
            assert_eq!(pawn.rank.get(0).unwrap(), hidden_rank.rank, "Guest pawn {} rank should match submitted proof", hidden_rank.pawn_id);

            let rank_str = rank_to_string(hidden_rank.rank);
            std::println!("  ✓ Guest pawn {} rank validated: {}", hidden_rank.pawn_id, rank_str);
        }
    }

    let host_move = snapshot.game_state.moves.get(0).unwrap();
    let guest_move = snapshot.game_state.moves.get(1).unwrap();
    let remaining_host_proofs = host_move.needed_rank_proofs.clone();
    let remaining_guest_proofs = guest_move.needed_rank_proofs.clone();

    std::println!("Host still needs {} rank proofs", remaining_host_proofs.len());
    std::println!("Guest still needs {} rank proofs", remaining_guest_proofs.len());
    std::println!("=== END VALIDATION ===");

    (snapshot.lobby_info.phase.clone(), snapshot.lobby_info.subphase.clone(), remaining_host_proofs, remaining_guest_proofs)
}