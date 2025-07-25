#![cfg(test)]
#![allow(unused_variables)]
extern crate std;
use super::super::*;
use super::super::test_utils::*;
use super::test_utils::*;
// region setup tests
#[test]
fn test_commit_setup_success() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 1u32;
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.phase, Phase::SetupCommit);
    assert_eq!(snapshot.subphase, Subphase::Both);
    let host_root = BytesN::from_array(&setup.env, &[1u8; 16]);
    let guest_root = BytesN::from_array(&setup.env, &[2u8; 16]);
    setup.client.commit_setup(&host, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root,
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.phase, Phase::SetupCommit);
    assert_eq!(snapshot.subphase, Subphase::Guest);
    setup.client.commit_setup(&guest, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: guest_root,
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.phase, Phase::MoveCommit);
    assert_eq!(snapshot.subphase, Subphase::Both);
}
#[test]
fn test_commit_setup_wrong_subphase() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 1u32;
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    let host_root = BytesN::from_array(&setup.env, &[1u8; 16]);
    setup.client.commit_setup(&host, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root.clone(),
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.subphase, Subphase::Guest);
    let result = setup.client.try_commit_setup(&host, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root,
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    assert!(result.is_err());
    assert_eq!(result.unwrap_err().unwrap(), Error::WrongSubphase);
}
#[test]
fn test_setup_guest_commits_first() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 1u32;
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    let guest_root = BytesN::from_array(&setup.env, &[2u8; 16]);
    setup.client.commit_setup(&guest, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: guest_root,
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.subphase, Subphase::Host);
    assert_eq!(snapshot.phase, Phase::SetupCommit);
    let host_root = BytesN::from_array(&setup.env, &[1u8; 16]);
    setup.client.commit_setup(&host, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root,
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.phase, Phase::MoveCommit);
    assert_eq!(snapshot.subphase, Subphase::Both);
}
#[test]
fn test_setup_leave_during_commit() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 1u32;
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    let host_root = BytesN::from_array(&setup.env, &[1u8; 16]);
    setup.client.commit_setup(&host, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root,
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.subphase, Subphase::Guest);
    setup.client.leave_lobby(&guest);
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.phase, Phase::Finished);
    assert_eq!(snapshot.subphase, Subphase::Host);
}
// endregion
// region security_mode=false tests
#[test]
fn test_setup_no_security_mode_success() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 1u32;
    let mut params = create_test_lobby_parameters(&setup.env);
    params.security_mode = false;
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    // Use the same setup logic as security mode tests
    let (host_setup, host_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, &UserIndex::Host)
    });
    let (guest_setup, guest_hidden_ranks) = setup.env.as_contract(&setup.contract_id, || {
        create_setup_commits_from_game_state(&setup.env, lobby_id, &UserIndex::Guest)
    });
    let (host_root, _host_proofs) = get_merkel(&setup.env, &host_setup, &host_hidden_ranks);
    let (guest_root, _guest_proofs) = get_merkel(&setup.env, &guest_setup, &guest_hidden_ranks);
    // Commit setup with ranks (in no-security mode, provide the hidden ranks directly)
    setup.client.commit_setup(&host, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root,
        zz_hidden_ranks: host_hidden_ranks,
    });
    setup.client.commit_setup(&guest, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: guest_root,
        zz_hidden_ranks: guest_hidden_ranks,
    });
    // Verify game moved to MoveCommit phase
    let final_snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(final_snapshot.phase, Phase::MoveCommit);
    assert_eq!(final_snapshot.subphase, Subphase::Both);
}
#[test]
fn test_setup_no_security_mode_missing_ranks() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 1u32;
    let mut params = create_test_lobby_parameters(&setup.env);
    params.security_mode = false;
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    // Try to commit setup without providing hidden ranks (should fail)
    let result = setup.client.try_commit_setup(&host, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: BytesN::from_array(&setup.env, &[1u8; 16]),
        zz_hidden_ranks: Vec::new(&setup.env),
    });
    assert!(result.is_err());
}
// endregion