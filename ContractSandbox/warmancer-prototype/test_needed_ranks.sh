#!/bin/bash
cd /mnt/c/Users/mokou/scryingstratego/ContractSandbox/warmancer-prototype
sshpass -p "touhou" ssh -o StrictHostKeyChecking=no mokou@172.24.48.1 "cd C:/Users/mokou/scryingstratego/ContractSandbox/warmancer-prototype && cargo test test_full_stratego_game -- --nocapture 2>&1" | grep -E "(needed rank:|Found host|Found guest|Pawn ID:|pawn_id=814)" | head -50