#![cfg(test)]
extern crate std;
extern crate alloc;

use alloc::string::ToString;
use super::*;
use soroban_sdk::testutils::{Address, Ledger, Logs};
use soroban_sdk::testutils::arbitrary::std::println;

#[test]
fn pack_lobby_info() {
    let e = &get_test_env();
    let lobby_info = LobbyInfo{
        index: 123,
        guest_address: soroban_sdk::Address::from_str(e,"GCVQEM7ES6D37BROAMAOBYFJSJEWK6AYEYQ7YHDKPJ57Z3XHG2OVQD56"),
        host_address: soroban_sdk::Address::from_str(e,"GC7UFDAGZJMCKENUQ22PHBT6Y4YM2IGLZUAVKSBVQSONRQJEYX46RUAD"),
        phase: Phase::Uninitialized,
    };
    let guest_xdr = lobby_info.guest_address.clone().to_xdr(e);
    log!(e, "guest xdr", guest_xdr);
    let guest_string : &str = &lobby_info.clone().guest_address.to_string().to_string();
    log!(e, "guest string", guest_string);
    assert_eq!(
        guest_xdr.len(),
        44,
        "guest_address.to_xdr(e).len() was {}, expected 36",
        guest_xdr.len()
    );
    let packed = Contract::pack_lobby_info(e, &lobby_info);
    let unpacked = Contract::unpack_lobby_info(e, &packed);
    assert_eq!(lobby_info, unpacked)
}

fn get_test_env() -> Env {
    let env = Env::default();
    env.ledger().set_timestamp(1227060);
    env.ledger().set_sequence_number(1);
    env.mock_all_auths();
    env
}