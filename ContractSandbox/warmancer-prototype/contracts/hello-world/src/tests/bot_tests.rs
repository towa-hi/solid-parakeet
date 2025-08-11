#![cfg(test)]
#![allow(unused_variables)]
extern crate std;
use super::super::*;
use super::super::test_utils::*;
use super::test_utils::*;
use soroban_sdk::Env;
use std::collections::HashMap;
// region bot types
pub struct BotParams {
    pub max_candidates: usize,
    pub worlds: usize,
    pub opp_replies: usize,
    pub cvar_alpha: f32,
    pub safety_mix: f32,
    pub enable_depth2: bool,
    pub rng_seed: u64,
    pub w_outrank: f32,
    pub w_trap: f32,
    pub w_press: f32,
    pub w_survive: f32,
    pub w_material: f32,
    pub w_info: f32,
    pub w_term: f32,
}
impl Default for BotParams {
    fn default() -> Self {
        Self {
            max_candidates: 12,
            worlds: 8,
            opp_replies: 6,
            cvar_alpha: 0.2,
            safety_mix: 0.25,
            enable_depth2: true,
            rng_seed: 0,
            w_outrank: 1.0,
            w_trap: 0.5,
            w_press: 0.3,
            w_survive: 0.2,
            w_material: 0.3,
            w_info: 0.1,
            w_term: 10.0,
        }
    }
}
pub struct RankBeliefs {
    pub p: HashMap<PawnId, HashMap<Rank, f32>>,
    pub p_trap: HashMap<PawnId, f32>,
    pub p_throne: HashMap<PawnId, f32>,
    pub remaining_count: HashMap<Rank, u32>,
}
impl RankBeliefs {
    pub fn new() -> Self {
        Self {
            p: HashMap::new(),
            p_trap: HashMap::new(),
            p_throne: HashMap::new(),
            remaining_count: HashMap::new(),
        }
    }
}
pub struct BotMemory {
    pub last_turn: Option<u32>,
    pub last_pos: HashMap<PawnId, Pos>,
    pub stationary_streak: HashMap<PawnId, u32>,
}
impl BotMemory {
    pub fn new() -> Self {
        Self {
            last_turn: None,
            last_pos: HashMap::new(),
            stationary_streak: HashMap::new(),
        }
    }
    pub fn reset(&mut self) {
        self.last_turn = None;
        self.last_pos.clear();
        self.stationary_streak.clear();
    }
    pub fn get_streak(&self, pawn_id: PawnId) -> u32 {
        *self.stationary_streak.get(&pawn_id).unwrap_or(&0)
    }
    pub fn update_from_game_state(&mut self, env: &Env, game_state: &GameState) {
        if let Some(prev_turn) = self.last_turn { if prev_turn == game_state.turn { return; } }
        for packed_pawn in game_state.pawns.iter() {
            let pawn_state = Contract::unpack_pawn(env, packed_pawn);
            let pawn_id: PawnId = pawn_state.pawn_id;
            if pawn_state.alive {
                if let Some(previous_pos) = self.last_pos.get(&pawn_id) {
                    if *previous_pos == pawn_state.pos {
                        let new_streak = self.stationary_streak.get(&pawn_id).copied().unwrap_or(0) + 1;
                        self.stationary_streak.insert(pawn_id, new_streak);
                    } else {
                        self.stationary_streak.insert(pawn_id, 0);
                    }
                } else {
                    self.stationary_streak.insert(pawn_id, 0);
                }
                self.last_pos.insert(pawn_id, pawn_state.pos);
            } else {
                self.last_pos.remove(&pawn_id);
                self.stationary_streak.remove(&pawn_id);
            }
        }
        self.last_turn = Some(game_state.turn);
    }
}
// endregion
// region beliefs
pub fn compute_enemy_rank_beliefs(env: &Env, game_state: &GameState, lobby_parameters: &LobbyParameters, perspective: UserIndex, memory: &BotMemory) -> RankBeliefs {
    let mut beliefs = RankBeliefs::new();
    let opponent_index = if perspective == UserIndex::Host { UserIndex::Guest } else { UserIndex::Host };
    let mut revealed_count_by_rank: HashMap<Rank, u32> = HashMap::new();
    for rank_value in 0u32..=11u32 { revealed_count_by_rank.insert(rank_value, 0u32); }
    let mut enemy_pawn_ids: Vec<PawnId> = Vec::new(env);
    for packed_pawn in game_state.pawns.iter() {
        let pawn_state = Contract::unpack_pawn(env, packed_pawn);
        let (_, owner_index) = Contract::decode_pawn_id(pawn_state.pawn_id);
        if owner_index == opponent_index {
            // Track revealed counts (alive or dead) to shrink the global remaining pool
            if pawn_state.zz_revealed && !pawn_state.rank.is_empty() {
                let revealed_rank = pawn_state.rank.get(0).unwrap();
                let current_count = *revealed_count_by_rank.get(&revealed_rank).unwrap_or(&0u32);
                revealed_count_by_rank.insert(revealed_rank, current_count + 1);
            }
            // Only evaluate beliefs for alive pawns
            if pawn_state.alive {
                enemy_pawn_ids.push_back(pawn_state.pawn_id);
            }
        }
    }
    for (rank_index, max_allowed) in lobby_parameters.max_ranks.iter().enumerate() {
        let rank_value: u32 = rank_index as u32;
        if rank_value <= 11 {
            let used_count = *revealed_count_by_rank.get(&rank_value).unwrap_or(&0u32);
            let remaining = if max_allowed >= used_count { max_allowed - used_count } else { 0u32 };
            beliefs.remaining_count.insert(rank_value, remaining);
        }
    }
    let pawns_map_by_id = Contract::create_pawns_map(env, &game_state.pawns);
    let mut unknown_enemy_ids: std::vec::Vec<PawnId> = std::vec::Vec::new();
    for enemy_id in enemy_pawn_ids.iter() {
        let (_, owner_index) = Contract::decode_pawn_id(enemy_id);
        let (_idx, pawn_state) = pawns_map_by_id.get_unchecked(enemy_id);
        if owner_index == opponent_index {
            // Treat as known only if the rank has been revealed in gameplay; skip dead
            if !pawn_state.alive { continue; }
            if !pawn_state.zz_revealed {
                unknown_enemy_ids.push(enemy_id);
            } else {
                let revealed_rank = pawn_state.rank.get(0).unwrap();
                let mut rank_distribution: HashMap<Rank, f32> = HashMap::new();
                rank_distribution.insert(revealed_rank, 1.0);
                beliefs.p.insert(enemy_id, rank_distribution);
                beliefs.p_trap.insert(enemy_id, if revealed_rank == 11 { 1.0 } else { 0.0 });
                beliefs.p_throne.insert(enemy_id, if revealed_rank == 0 { 1.0 } else { 0.0 });
            }
        }
    }
    for unknown_id in &unknown_enemy_ids {
        let mut rank_distribution: HashMap<Rank, f32> = HashMap::new();
        // use stationary streak as a generic immobility cue (both throne and trap get a small boost)
        // but only if this pawn actually has at least one legal adjacent tile to move into (assuming it can move)
        let stationary_turns = memory.get_streak(*unknown_id);
        // compute a throne-adjacency multiplier: if all adjacent tiles are either board edge
        // or occupied by an enemy pawn that has been stationary, give throne extra weight
        let (_idx, center_pawn) = pawns_map_by_id.get_unchecked(*unknown_id);
        let mut neighbors = [Pos { x: -42069, y: -42069 }; 6];
        Contract::get_neighbors(&center_pawn.pos, lobby_parameters.board.hex, &mut neighbors);
        let mut total_adjacents: i32 = 0;
        let mut satisfied: i32 = 0;
        let mut edge_count: i32 = 0;
        let mut guard_count: i32 = 0; // stationary guards
        let mut moved_guard_count: i32 = 0; // allied occupant that hasn't been stationary
        let mut has_legal_adjacent_move: bool = false;
        for nb in neighbors.iter() {
            if nb.x == -42069 { continue; } // skip sentinel slots
            total_adjacents += 1;
            // board edge check
            let is_edge = nb.x < 0 || nb.x >= lobby_parameters.board.size.x || nb.y < 0 || nb.y >= lobby_parameters.board.size.y;
            if is_edge {
                edge_count += 1; satisfied += 1; continue;
            }
            // check if tile is passable
            let mut is_passable_tile = false;
            for packed_tile in lobby_parameters.board.tiles.iter() {
                let t = Contract::unpack_tile(packed_tile);
                if t.pos.x == nb.x && t.pos.y == nb.y { is_passable_tile = t.passable; break; }
            }
            // occupied-by-ally (enemy from our perspective) check
            let mut has_stationary_guard = false;
            let mut occupied_by_ally = false;
            for (_, (_, p2)) in pawns_map_by_id.iter() {
                if !p2.alive { continue; }
                if p2.pos.x == nb.x && p2.pos.y == nb.y {
                    let (_, owner2) = Contract::decode_pawn_id(p2.pawn_id);
                    if owner2 == opponent_index {
                        occupied_by_ally = true;
                        if memory.get_streak(p2.pawn_id) >= 2 { has_stationary_guard = true; }
                    }
                    break;
                }
            }
            // track if there is at least one legal adjacent move (passable and not ally-occupied)
            if is_passable_tile && !occupied_by_ally { has_legal_adjacent_move = true; }
            if has_stationary_guard { guard_count += 1; satisfied += 1; }
            else if occupied_by_ally { moved_guard_count += 1; satisfied += 1; }
        }
        let immobility_bias: f32 = if stationary_turns >= 2 && has_legal_adjacent_move { 2.0 + (stationary_turns as f32).min(4.0) * 0.5 } else { 1.0 };
        let throne_surround_multiplier: f32 = if total_adjacents > 0 && satisfied == total_adjacents {
            // board edge is slightly more important than stationary guards
            let edge_weight = 0.6f32; let guard_weight = 0.35f32; let moved_guard_weight = 0.2f32;
            let mix = edge_weight * (edge_count as f32)
                + guard_weight * (guard_count as f32)
                + moved_guard_weight * (moved_guard_count as f32);
            1.0 + 0.25 + 0.05 * mix
        } else { 1.0 };
        // build unnormalized weights
        let mut unnormalized_sum: f32 = 0.0;
        for rank_value in 0u32..=11u32 {
            let remaining_for_rank = *beliefs.remaining_count.get(&rank_value).unwrap_or(&0u32);
            if remaining_for_rank > 0 {
                let mut weight: f32 = 1.0;
                // hard constraint: if this pawn has moved (contract flags), it cannot be throne or trap
                let (_i2, pawn_flags) = pawns_map_by_id.get_unchecked(*unknown_id);
                let has_moved_flag = pawn_flags.moved || pawn_flags.moved_scout;
                if has_moved_flag && (rank_value == 0 || rank_value == 11) {
                    weight = 0.0;
                } else {
                    // generic immobility prior: boosts throne and trap a bit if the pawn has stayed still
                    if stationary_turns >= 2 && (rank_value == 0 || rank_value == 11) { weight *= immobility_bias; }
                    // adjacency-based boost specifically for the throne
                    if rank_value == 0 { weight *= throne_surround_multiplier; }
                }
                rank_distribution.insert(rank_value, weight);
                unnormalized_sum += weight;
            }
        }
        if unnormalized_sum == 0.0 { for rank_value in 0u32..=11u32 { rank_distribution.insert(rank_value, 1.0 / 12.0); } }
        else { for rank_value in 0u32..=11u32 { if let Some(p) = rank_distribution.get_mut(&rank_value) { *p = *p / unnormalized_sum; } } }
        beliefs.p.insert(*unknown_id, rank_distribution);
    }
    let mut total_mass_by_rank: HashMap<Rank, f32> = HashMap::new();
    for rank_value in 0u32..=11u32 { total_mass_by_rank.insert(rank_value, 0.0); }
    for unknown_id in &unknown_enemy_ids {
        if let Some(dist) = beliefs.p.get(unknown_id) {
            for (rank_value, prob) in dist { let s = *total_mass_by_rank.get(rank_value).unwrap_or(&0.0) + *prob; total_mass_by_rank.insert(*rank_value, s); }
        }
    }
    for rank_value in 0u32..=11u32 {
        let target_total = *beliefs.remaining_count.get(&rank_value).unwrap_or(&0u32) as f32;
        let current_total = *total_mass_by_rank.get(&rank_value).unwrap_or(&0.0);
        let scale = if current_total > 0.0 { target_total / current_total } else { 1.0 };
        if scale != 1.0 { for unknown_id in &unknown_enemy_ids { if let Some(dist) = beliefs.p.get_mut(unknown_id) { if let Some(p) = dist.get_mut(&rank_value) { *p = *p * scale; } } } }
    }
    for unknown_id in &unknown_enemy_ids {
        if let Some(dist) = beliefs.p.get_mut(unknown_id) {
            let mut z: f32 = 0.0;
            for (_, pr) in dist.iter() { z += *pr; }
            if z > 0.0 { for (_, pr) in dist.iter_mut() { *pr = *pr / z; } }
        }
    }
    if *beliefs.remaining_count.get(&0u32).unwrap_or(&0u32) == 1u32 {
        let mut throne_candidates: std::vec::Vec<(PawnId, f32)> = std::vec::Vec::new();
        for unknown_id in &unknown_enemy_ids { if let Some(dist) = beliefs.p.get(unknown_id) { let p0 = *dist.get(&0u32).unwrap_or(&0.0); throne_candidates.push((*unknown_id, p0)); } }
        if throne_candidates.len() == 1 { let only_id = throne_candidates[0].0; if let Some(dist) = beliefs.p.get_mut(&only_id) { for rank_value in 0u32..=11u32 { let v = if rank_value == 0 { 1.0 } else { 0.0 }; dist.insert(rank_value, v); } } }
    }
    for (pawn_id, dist) in beliefs.p.iter() {
        let p_trap = *dist.get(&11u32).unwrap_or(&0.0);
        let p_throne = *dist.get(&0u32).unwrap_or(&0.0);
        beliefs.p_trap.insert(*pawn_id, p_trap);
        beliefs.p_throne.insert(*pawn_id, p_throne);
    }
    beliefs
}
// endregion
// region tests
#[test]
fn test_bot_tests_file_compiles() {
    let setup = TestSetup::new();
    assert!(true);
}
#[test]
fn test_belief_tracking_host_perspective() {
    // create insecure lobby
    let setup = TestSetup::new();
    let lobby_id = 7001u32;
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let mut params = create_test_lobby_parameters(&setup.env);
    params.security_mode = false; // insecure mode so we can commit+prove in one call
    params.blitz_interval = 0; // regular single-move turns
    setup.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params.clone() }, "bot host");
    setup.join_lobby(&guest, &JoinLobbyReq { lobby_id }, "bot guest");
    // commit setups (in insecure mode, need to pass ranks)
    let (host_setup, host_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, &UserIndex::Host)
    });
    let (guest_setup, guest_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, &UserIndex::Guest)
    });
    let (host_root, host_merkle) = get_merkel(&setup.env, &host_setup, &host_ranks);
    let (guest_root, guest_merkle) = get_merkel(&setup.env, &guest_setup, &guest_ranks);
    setup.commit_setup(&host, &CommitSetupReq { lobby_id, rank_commitment_root: host_root, zz_hidden_ranks: host_ranks.clone() }, "bot host");
    setup.commit_setup(&guest, &CommitSetupReq { lobby_id, rank_commitment_root: guest_root, zz_hidden_ranks: guest_ranks.clone() }, "bot guest");
    // persistent memory for belief model
    let mut memory = BotMemory::new();
    for move_number in 1..=30u32 {
        // snapshot at start of turn
        let snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
        // update memory from current state
        memory.update_from_game_state(&setup.env, &snapshot.game_state);
        // compute beliefs from host perspective and print alongside actual guest ranks
        let beliefs = compute_enemy_rank_beliefs(&setup.env, &snapshot.game_state, &snapshot.lobby_parameters, UserIndex::Host, &memory);
        // iterate enemy pawns and print their belief vs truth (if known via insecure ranks)
        let pawns_map = &snapshot.pawns_map;
        std::println!("=== TURN {}: Host perspective enemy rank beliefs ===", move_number);
        for (_, (_, pawn)) in pawns_map.iter() {
            let (_, owner) = Contract::decode_pawn_id(pawn.pawn_id);
            if owner != UserIndex::Guest { continue; }
            if !pawn.alive { continue; }
            // actual rank in insecure mode is stored in pawn.rank at setup time
            let actual_rank = if pawn.rank.is_empty() { 999 } else { pawn.rank.get(0).unwrap() };
            let dist = beliefs.p.get(&pawn.pawn_id);
            if let Some(d) = dist {
                let p_throne = *d.get(&0u32).unwrap_or(&0.0);
                let p_trap = *d.get(&11u32).unwrap_or(&0.0);
                let p_top = {
                    let mut best_r: u32 = 0; let mut best_p: f32 = -1.0;
                    for r in 0u32..=11u32 { if let Some(pr) = d.get(&r) { if *pr > best_p { best_p = *pr; best_r = r; } } }
                    (best_r, best_p)
                };
                std::println!(
                    "guest pawn {} at ({},{}): actual={}, p(throne)={:.3}, p(trap)={:.3}, argmax=({}, {:.3}), streak={}",
                    pawn.pawn_id, pawn.pos.x, pawn.pos.y, actual_rank, p_throne, p_trap, p_top.0, p_top.1, memory.get_streak(pawn.pawn_id)
                );
            }
        }
        // generate simple legal moves for both sides using existing helper
        let host_prove = {
            let snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
            let mv = generate_valid_move_req(&setup.env, &snapshot.pawns_map, &snapshot.lobby_parameters, &UserIndex::Host, &host_ranks, 1000 + move_number as u64);
            let move_proofs = if let Some(single) = mv { Vec::from_array(&setup.env, [single]) } else { Vec::new(&setup.env) };
            ProveMoveReq { lobby_id, move_proofs }
        };
        let guest_prove = {
            let snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
            let mv = generate_valid_move_req(&setup.env, &snapshot.pawns_map, &snapshot.lobby_parameters, &UserIndex::Guest, &guest_ranks, 2000 + move_number as u64);
            let move_proofs = if let Some(single) = mv { Vec::from_array(&setup.env, [single]) } else { Vec::new(&setup.env) };
            ProveMoveReq { lobby_id, move_proofs }
        };
        // build hashes
        let host_commit = {
            let mut hashes = Vec::new(&setup.env);
            for mv in host_prove.move_proofs.iter() {
                let ser = mv.clone().to_xdr(&setup.env);
                let full = setup.env.crypto().sha256(&ser).to_bytes().to_array();
                hashes.push_back(HiddenMoveHash::from_array(&setup.env, &full[0..16].try_into().unwrap()));
            }
            CommitMoveReq { lobby_id, move_hashes: hashes }
        };
        let guest_commit = {
            let mut hashes = Vec::new(&setup.env);
            for mv in guest_prove.move_proofs.iter() {
                let ser = mv.clone().to_xdr(&setup.env);
                let full = setup.env.crypto().sha256(&ser).to_bytes().to_array();
                hashes.push_back(HiddenMoveHash::from_array(&setup.env, &full[0..16].try_into().unwrap()));
            }
            CommitMoveReq { lobby_id, move_hashes: hashes }
        };
        // execute insecure batch pattern like integration tests (commit+prove for both)
        crate::tests::integration_tests::execute_insecure_batch_pattern(
            &setup, &host, &guest, &host_commit, &guest_commit, &host_prove, &guest_prove, ">bot host", ">bot guest",
        );
        // stop if game finished early
        let end_snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
        if end_snapshot.lobby_info.phase == Phase::Finished { break; }
    }
}
// endregion

