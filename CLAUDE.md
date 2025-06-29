# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## The Golden Rule  
When unsure about implementation details, ALWAYS ask the user.  

## Development Guidelines
- Add specially formatted comments throughout the codebase, where appropriate, for yourself as inline knowledge that can be easily `grep`ped for.
- Use `AIDEV-NOTE:`, `AIDEV-TODO:`, or `AIDEV-QUESTION:` (all-caps prefix) for comments aimed at AI and developers.  
- Keep them concise (≤ 120 chars).  
- **Important:** Before scanning files, always first try to **locate existing anchors** `AIDEV-*` in relevant subdirectories.  
- **Update relevant anchors** when modifying associated code.  
- **Do not remove `AIDEV-NOTE`s** without explicit human instruction.  

## IDE Diagnostics Guidelines
- **Only fix errors** (severity level "Error") from IDE diagnostics
- **Do not fix warnings** (severity level "Warning") unless explicitly requested by the user
- **Do not fix info/hint level** diagnostics unless explicitly requested
- Focus on functionality over style warnings


## C# Style Guidelines
- Follow existing conventions
- Types: UpperCamelCase
- Interfaces: IUpperCamelCase
- Methods: UpperCamelCase
- Properties: UpperCamelCase
- Local Variables: lowerCamelCase
- Parameters: parameters that set values are sometimes prefixed with n
- Static fields: lowerCamelCase
- Explicit type over var
- Trailing commas
- Object creation using new()
- For Unity GameObjects and MonoBehaviours, use `!gameObject` instead of `gameObject == null` for null checks

## Strict Guidelines
- Do not edit scriptableobjects or any fields in monobehaviors that are serialized without my permission

## Project Overview

This is a WebGL Unity-based implementation of "Scrying Stratego" - a variant of Stratego running on the Stellar blockchain with simultaneous turn resolution using commit-reveal logic. The project combines:

- **Unity Game Client** (C#): Main game interface and logic
- **Stellar Smart Contract** (Rust): Blockchain game state management in `ContractSandbox/warmancer-prototype`

## Architecture

### Unity Project Structure
- NOTE: When working in Unity, the only files that I will expect you to edit are .cs files inside of Assets/Scripts
- `Assets/Scripts/` - Core game logic organized by domain:
	- `Board/` - Game board, tiles, pawns, and piece definitions
	- `UI/` - All user interface components and menus
	- `Stellar/` - Blockchain integration and contract communication
	- `Effects/` - Visual effects and rendering utilities
	- `Controls/` - Input handling and interaction systems
	- `Debug/` - Development and testing utilities

#### Game State Management
- `GameManager.cs` - Central game coordinator
- `TestBoardManager.cs` - Client logic

#### Blockchain Integration
- `Contract.cs` - Smart contract interface
- `StellarManager.cs` - Game to StellarDotnet layer
- `StellarDotnet.cs` - Stellar network communication
- `WalletManager.cs` - User wallet integration

#### Board System
- `BoardDef.cs` - Configurable board layouts stored as ScriptableObjects
- `BoardGrid.cs` - Hex/square tile coordinate system

#### Visual Systems
- Universal Render Pipeline (URP) configuration
- Custom shaders for card effects and visual polish
- Sprite-based pawn rendering with animation controllers

### Smart Contract (Rust)
- Located in `ContractSandbox/warmancer-prototype/contracts/hello-world/src/`
- Built with Soroban SDK for Stellar blockchain
- Implements commit-reveal game mechanics

#### Smart Contract Rules

When working with the Rust contract code:
- Never edit struct definitions in the contract
- Do not import external dependencies (no `std`)
- Only edit `lib.rs` and `test.rs` files
- Use `#region` and `#endregion` for code organization
- "user" and "opponent" refer to method invoker, not guest/host
- Valid moves require: owned pawn, alive pawn, passable tile, not occupied by same team

## Development Commands

### Unity Development
- Open project in Unity Editor 6000.0.28f1
- Main scene: `Assets/Scenes/Main.unity`
- Board editor scene: `Assets/Scenes/BoardMaker.unity`

### Unity Console Logs via MCP Unity
- **Preferred method**: Direct URI access using `ReadMcpResourceTool`
  - Info logs: `unity://logs/info?offset=0&limit=25&includeStackTrace=false`
  - Error logs: `unity://logs/error?offset=0&limit=25&includeStackTrace=false`
  - Warning logs: `unity://logs/warning?offset=0&limit=25&includeStackTrace=false`
- Alternative: `mcp__mcp-unity__get_console_logs` (may include MCP Unity debug spam)
- Always set `includeStackTrace: false` to save 80-90% of tokens unless debugging

### Smart Contract Development
```bash
cd ContractSandbox/warmancer-prototype
cargo test -- --nocapture  # Run contract tests with output
```

### Building
- Unity builds are configured through Unity Editor Build Settings
- WebGL builds output to `Build/` directory
- Contract builds use Rust/Cargo standard build process. Only the user should build and deploy
