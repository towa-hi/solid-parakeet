#![cfg(test)]
extern crate std;
extern crate alloc;

use super::*;
use soroban_sdk::testutils::{Ledger};
use alloc::string::ToString;

#[test]
fn test_generate_uuid() {
    let env = &get_test_env();
    log!(env, "test_generate_uuid() started");

    let test_invoker = String::from_str(&env, "test_invoker");
    let test_salt: u32 = 0;
    let uuid: String = Contract::generate_uuid(&env, test_invoker.clone(), test_salt);
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

fn get_test_env() -> Env {
    let env = Env::default();
    env.ledger().set_timestamp(1227060);
    env.ledger().set_sequence_number(1);
    env
}