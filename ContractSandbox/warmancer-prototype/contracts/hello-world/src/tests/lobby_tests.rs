#![cfg(test)]
#![allow(unused_variables)]
extern crate std;
use super::super::*;
use super::super::test_utils::*;
use super::test_utils::*;

// region lobby tests
#[test]
fn test_make_lobby_success() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let lobby_id = 1u32;
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq {
        lobby_id,
        parameters: params,
    });
    setup.verify_lobby_info(lobby_id, &host, Phase::Lobby);
    setup.verify_user_lobby(&host, lobby_id);
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.subphase, Subphase::Guest);
}
#[test]
fn test_lobby_id_collision() {
    let setup = TestSetup::new();
    let params = create_test_lobby_parameters(&setup.env);
    let host_1 = setup.generate_address();
    setup.client.make_lobby(&host_1, &MakeLobbyReq {
        lobby_id: 1,
        parameters: params.clone(),
    });
    let host_2 = setup.generate_address();
    let result = setup.client.try_make_lobby(&host_2, &MakeLobbyReq {
        lobby_id: 1,
        parameters: params,
    });
    assert!(result.is_err());
    assert_eq!(result.unwrap_err().unwrap(), Error::AlreadyExists);
}
#[test]
fn test_leave_lobby_errors() {
    let setup = TestSetup::new();
    let user = setup.generate_address();
    let result = setup.client.try_leave_lobby(&user);
    assert_eq!(result.unwrap_err().unwrap(), Error::NotFound);
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&user, &MakeLobbyReq {
        lobby_id: 1,
        parameters: params,
    });
    setup.client.leave_lobby(&user);
    let result = setup.client.try_leave_lobby(&user);
    assert_eq!(result.unwrap_err().unwrap(), Error::NotFound);
}
#[test]
fn test_join_lobby_access_control() {
    let setup = TestSetup::new();
    let params = create_test_lobby_parameters(&setup.env);
    let host = setup.generate_address();
    let guest_1 = setup.generate_address();
    let guest_2 = setup.generate_address();
    setup.client.make_lobby(&host, &MakeLobbyReq {
        lobby_id: 1,
        parameters: params.clone(),
    });
    let result = setup.client.try_join_lobby(&host, &JoinLobbyReq { lobby_id: 1 });
    assert_eq!(result.unwrap_err().unwrap(), Error::Unauthorized);
    setup.client.join_lobby(&guest_1, &JoinLobbyReq { lobby_id: 1 });
    let result = setup.client.try_join_lobby(&guest_2, &JoinLobbyReq { lobby_id: 1 });
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotJoinable);
    setup.client.make_lobby(&guest_2, &MakeLobbyReq {
        lobby_id: 2,
        parameters: params,
    });
    let result = setup.client.try_join_lobby(&guest_1, &JoinLobbyReq { lobby_id: 2 });
    assert_eq!(result.unwrap_err().unwrap(), Error::Unauthorized);
}
#[test]
fn test_host_leaves_aborts_lobby() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 1u32;
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq {
        lobby_id,
        parameters: params,
    });
    setup.client.leave_lobby(&host);
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.phase, Phase::Aborted);
    assert_eq!(snapshot.subphase, Subphase::None);
    let result = setup.client.try_join_lobby(&guest, &JoinLobbyReq { lobby_id });
    assert!(result.is_err());
    assert_eq!(result.unwrap_err().unwrap(), Error::LobbyNotJoinable);
}
#[test]
fn test_sequential_lobby_creation() {
    let setup = TestSetup::new();
    let user = setup.generate_address();
    let guest = setup.generate_address();
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&user, &MakeLobbyReq {
        lobby_id: 1,
        parameters: params.clone(),
    });
    setup.verify_user_lobby(&user, 1);
    setup.client.make_lobby(&user, &MakeLobbyReq {
        lobby_id: 2,
        parameters: params,
    });
    setup.verify_user_lobby(&user, 2);
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, 1);
    assert_eq!(snapshot.phase, Phase::Lobby);
    assert_eq!(snapshot.subphase, Subphase::Guest);
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id: 1 });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, 1);
    assert_eq!(snapshot.phase, Phase::SetupCommit);
    assert_eq!(snapshot.subphase, Subphase::Both);
}
#[test]
fn test_lobby_id_reuse_blocked() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 1u32;
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq {
        lobby_id,
        parameters: params.clone(),
    });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    setup.client.leave_lobby(&host);
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.phase, Phase::Finished);
    assert_eq!(snapshot.subphase, Subphase::Guest);
    let new_host = setup.generate_address();
    let result = setup.client.try_make_lobby(&new_host, &MakeLobbyReq {
        lobby_id,
        parameters: params,
    });
    assert!(result.is_err());
    assert_eq!(result.unwrap_err().unwrap(), Error::AlreadyExists);
}
#[test]
fn test_join_starts_game() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 1u32;
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq {
        lobby_id,
        parameters: params,
    });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    let snapshot = extract_full_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.lobby_info.phase, Phase::SetupCommit);
    assert_eq!(snapshot.lobby_info.subphase, Subphase::Both);
    assert!(snapshot.game_state.pawns.len() > 0);
    setup.verify_user_lobby(&host, lobby_id);
    setup.verify_user_lobby(&guest, lobby_id);
}
#[test]
fn test_leave_during_setup() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 10u32;
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq { lobby_id, parameters: params });
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    setup.client.leave_lobby(&guest);
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.phase, Phase::Finished);
    assert_eq!(snapshot.subphase, Subphase::Host);
}
#[test]
fn test_abandoned_lobby_joinable() {
    let setup = TestSetup::new();
    let host = setup.generate_address();
    let guest = setup.generate_address();
    let lobby_id = 105u32;
    let params = create_test_lobby_parameters(&setup.env);
    setup.client.make_lobby(&host, &MakeLobbyReq {
        lobby_id,
        parameters: params.clone(),
    });
    setup.client.make_lobby(&host, &MakeLobbyReq {
        lobby_id: 999,
        parameters: params,
    });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.phase, Phase::Lobby);
    assert_eq!(snapshot.subphase, Subphase::Guest);
    setup.client.join_lobby(&guest, &JoinLobbyReq { lobby_id });
    let snapshot = extract_phase_snapshot(&setup.env, &setup.contract_id, lobby_id);
    assert_eq!(snapshot.phase, Phase::SetupCommit);
    assert_eq!(snapshot.subphase, Subphase::Both);
}
// endregion