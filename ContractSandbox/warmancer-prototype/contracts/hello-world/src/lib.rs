#![no_std]
use soroban_sdk::{contract, contracttype, contracterror, contractimpl, vec, Env, Address, String, Vec};
use soroban_sdk::storage::Persistent;

#[contracttype]
pub enum DataKey {
    UserName(Address),
    Wins(u32),
}

#[contracterror]
#[derive(Copy, Clone, Debug, Eq, PartialEq, PartialOrd, Ord)]
pub enum Error {
    UserNotFound = 1,
    InvalidUsername = 2,
}

#[contract]
pub struct Contract;

#[contractimpl]
impl Contract {
    pub fn hello(env: Env, to: String) -> Vec<String> {
        vec![&env, String::from_str(&env, "Hello"), to]
    }

    pub fn register(env: Env, user: Address, username: String) -> Result<String, Error> {
        user.require_auth();
        if !Self::validate_username(username.clone()) {
            return Err(Error::InvalidUsername);
        }
        let key = DataKey::UserName(user);
        let storage = env.storage().persistent();

        storage.set(&key, &username);

        Ok(username)
    }

    pub fn get_username(env: Env, user: Address) -> Result<String, Error> {
        let key = DataKey::UserName(user);
        let storage = env.storage().persistent();
        storage.get(&key).ok_or(Error::UserNotFound)
    }

    fn validate_username(username: String) -> bool {
        let username_length = username.len(); // This is u32
        if username_length == 0 || username_length > 16 {
            return false;
        }

        let mut buffer = [0u8; 16];
        // Convert username_length to usize for slice indexing
        username.copy_into_slice(&mut buffer[..username_length as usize]);

        // Also need to convert for the validation loop
        for &b in buffer[..username_length as usize].iter() {
            if !(b.is_ascii_alphanumeric() || b == b'_') {
                return false;
            }
        }
        true
    }
}

