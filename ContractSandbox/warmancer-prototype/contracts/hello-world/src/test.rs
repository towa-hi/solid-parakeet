#![cfg(test)]
extern crate std;
extern crate alloc;

use super::*;
use soroban_sdk::testutils::{Address, Events, Ledger};
use alloc::string::ToString;

#[test]
fn test_generate_uuid() {
    let env = &get_test_env();
    log!(env, "test_generate_uuid() started");
    let test_salt: u32 = 0;
    let contract_id = env.register(Contract, ());
    let uuid: String = env.as_contract(&contract_id, || {
        Contract::generate_uuid(&env, test_salt)
    });
    assert_eq!(uuid.len(), 36);
    let uuid_str = uuid.to_string();
    for c in uuid_str.chars() {
        if c == '-' {
            continue;
        }
        assert!(c.is_ascii_hexdigit(), "UUID should only contain hex characters");
    }
    log!(env, "UUID: ", uuid_str);
    log!(env, "test_generate_uuid() ended");
}

#[test]
fn test_send_invite() {
    let env = &get_test_env();
    log!(env, "test_send_invite() started");
    let contract_id = env.register(Contract, ());
    let client = ContractClient::new(&env, &contract_id);
    let host = soroban_sdk::Address::generate(&env);
    let guest = soroban_sdk::Address::generate(&env);
    let lobby_parameters = LobbyParameters {
        board_def: BoardDef {
            name: String::from_str(&env, "test board"),
            size: Pos { x: 10, y: 10 },
            tiles: Map::new(&env),
            is_hex: false,
            default_max_pawns: Map::new(&env),
        },
        must_fill_all_tiles: false,
        max_pawns: Map::new(&env),
        dev_mode: false,
        security_mode: false,
    };
    let req = SendInviteReq {
        host_address: host.to_string(),
        guest_address: guest.to_string(),
        ledgers_until_expiration: 100,
        parameters: lobby_parameters,
    };

    let result = client.send_invite(&host, &req);
    let events = env.events().all();
    assert!(!events.is_empty());
    // TODO: test events better




}



fn get_test_env() -> Env {
    let env = Env::default();
    env.ledger().set_timestamp(1227060);
    env.ledger().set_sequence_number(1);
    env.mock_all_auths();
    env
}