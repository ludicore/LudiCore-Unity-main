## [2.1.1] - 2025-01-11

### Fixed

- Fixed LudiCore plugin conflicting with other plugins

## [2.1.0] - 2025-01-10

### Feature

- Can now select any console log to provide as context to AI
- Added support for 2D with /prototype slash command

### Fixed

- Context selection getting cleared when entering/exiting play mode and when scripts are recompiled
- New chat opening when entering/exiting play mode and when scripts are recompiled
- Error messages when using claude-3-5-haiku
- Error messages with some /prototype responses

## [2.0.0] - 2025-01-03

### Added

- Reduced loading times across all AI models
- Can now type '/INSERT_COMMAND' to quickly switch chat modes
- Added support for prefab instantiation via chat commands
- Now storing last used command in between editor reloads

### Feature

- Newly added slash commands replacing old command system
- Chat slash command used for general Unity inquiries, debugging, design process
- Script slash command used to directly create/edit your scripts
- Accept/Deny each inserted script using SEARCH/REPLACE blocks
- Prototype slash command used to quickly create your scene and game objects
- Prototype command now has ability to create simple game UIs

### Fixed

- Placeholder text wasn't displaying proper text always

## [1.2.0] - 2024-12-28

### Added

- Streaming speed for responses faster
- Base Model speed & quality increased
- Chats showcasing in chronological order of most recently used
- Haiku 3-5 available for Indie tier users now

### Feature

- Chat history being stored locally in SQLite DB instead of on LudiCore servers

### Fixed

- Feedback buttons weren't doing anything overly helpful
- Black flash after new chats
- Haiku 3 no longer being offered as an available AI model

## [1.1.3] - 2024-12-25

### Fixed

- Incorrect placeholder text for Unity 6 users

## [1.1.2] - 2024-12-17

### Fixed

- Error handling on context parsers, should see less error logs

## [1.1.1] - 2024-12-15

### Fixed

- Syntax highlighting wasn't really accurate, a little better now

## [1.1.0] - 2024-12-11

### Feature

- Asset database context awareness
- Scene context awareness
- Tree sitter to get general codebase context awareness

### Fixed

- Couldn't delete chats on Windows OS

## [1.0.4] - 2024-12-04

### Fixed

- Refresh occasionally caused chat to stop working on Windows OS

## [1.0.3] - 2024-12-03

### Fixed

- Stop stream button freezing rest of plugin
- Login during auth process would overwrite existing token
- UI Issue with chat history items overflow

### Added

- Save most recently used model between re-compiles

## [1.0.2] - 2024-12-02

### Fixed

- API calling issue on Windows OS

## [1.0.1] - 2024-12-02

### Fixed

- Dependency issues with Microsoft.CodeAnalysis.dll

## [1.0.0] - 2024-12-02

### Added

- First release of LudiCore's AI Assistant beta build
