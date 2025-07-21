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
    });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.phase, Phase::SetupCommit);
    assert_eq!(snapshot.subphase, Subphase::Guest);
    setup.client.commit_setup(&guest, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: guest_root,
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
    });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.subphase, Subphase::Guest);
    let result = setup.client.try_commit_setup(&host, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root,
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
    });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.subphase, Subphase::Host);
    assert_eq!(snapshot.phase, Phase::SetupCommit);
    let host_root = BytesN::from_array(&setup.env, &[1u8; 16]);
    setup.client.commit_setup(&host, &CommitSetupReq {
        lobby_id,
        rank_commitment_root: host_root,
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
    });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.subphase, Subphase::Guest);
    setup.client.leave_lobby(&guest);
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.phase, Phase::Finished);
    assert_eq!(snapshot.subphase, Subphase::Host);
}
// endregion