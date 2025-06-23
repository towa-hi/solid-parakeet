# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity-based implementation of "Scrying Stratego" - a variant of Stratego running on the Stellar blockchain with simultaneous turn resolution using commit-reveal logic. The project combines:

- **Unity Game Client** (C#): Main game interface and logic
- **Stellar Smart Contract** (Rust): Blockchain game state management in `ContractSandbox/warmancer-prototype`
- **Build System**: Multi-platform Unity builds with WebGL support

## Architecture

### Unity Project Structure
- `Assets/Scripts/` - Core game logic organized by domain:
  - `Board/` - Game board, tiles, pawns, and piece definitions
  - `UI/` - All user interface components and menus
  - `Stellar/` - Blockchain integration and contract communication
  - `Effects/` - Visual effects and rendering utilities
  - `Controls/` - Input handling and interaction systems
  - `Debug/` - Development and testing utilities

### Smart Contract (Rust)
- Located in `ContractSandbox/warmancer-prototype/`
- Built with Soroban SDK for Stellar blockchain
- Implements commit-reveal game mechanics

## Development Commands

### Unity Development
- Open project in Unity Editor 6000.0.28f1
- Main scene: `Assets/Scenes/Main.unity`
- Board editor scene: `Assets/Scenes/BoardMaker.unity`

### Smart Contract Development
```bash
cd ContractSandbox/warmancer-prototype
cargo test -- --nocapture  # Run contract tests with output
```

### Building
- Unity builds are configured through Unity Editor Build Settings
- WebGL builds output to `Build/` directory
- Contract builds use Rust/Cargo standard build process

## Key Systems

### Game State Management
- `GameManager.cs` - Central game coordinator
- `Rules.cs` - Game rule validation and enforcement
- `FakeServer.cs` - Local testing without blockchain

### Blockchain Integration
- `Contract.cs` - Smart contract interface
- `StellarDotnet.cs` - Stellar network communication
- `WalletManager.cs` - User wallet integration

### Board System
- `BoardDef.cs` - Configurable board layouts stored as ScriptableObjects
- `BoardGrid.cs` - Hex/square tile coordinate system
- `Tile.cs`/`Pawn.cs` - Core game pieces

### Visual Systems
- Universal Render Pipeline (URP) configuration
- Custom shaders for card effects and visual polish
- Sprite-based pawn rendering with animation controllers

## Smart Contract Rules

When working with the Rust contract code:
- Never edit struct definitions in the contract
- Do not import external dependencies (no `std`)
- Only edit `lib.rs` and `test.rs` files
- Use `#region` and `#endregion` for code organization
- "user" and "opponent" refer to method invoker, not guest/host
- Valid moves require: owned pawn, alive pawn, passable tile, not occupied by same team

## Audio System
- `AudioManager.cs` handles all sound effects and music
- Separate folders for edited vs original audio files
- Music includes battle, menu, and title tracks