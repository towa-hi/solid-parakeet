# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.2] - 2024-11-06

### Fixed

- Fixed masking not working for edge detection
- Fixed potential UnassignedReferenceExceptions when outline/fill material was not assigned
- Fixed package samples missing scripts and materials

### Changed

- Changed edge detection default background to clear instead of white

## [1.2.1] - 2024-11-03

### Added

- Added custom property drawer for rendering layer mask in Unity 2022

### Fixed

- Fixed Wide Outline not working with render scales different from 1
- Fixed error when using compatibility check in a project using a 2D renderer

## [1.2.0] - 2024-10-25

### Added

- Added alpha cutout support for Wide Outline and Soft Outline
- Added support for WebGL (except for Soft Outline)
- Added support for iOS
- Added the SetActive method for enabling/disabling outlines through code

### Fixed

- Fixed typos

## [1.1.1] - 2024-10-12

### Fixed

- Fixed a compilation error on older version of Unity 2022.3

## [1.1.0] - 2024-10-07

### Added

- Added support for Unity 2022.3
- Added support for Unity 6 with compatibility mode enabled
- Added (experimental) support for the DOTS Hybrid Renderer
- Added new compatibility check window to see if Linework will work with your project
- Added option to create outline settings directly from within the renderer feature UI

### Fixed

- Fixed various memory leaks and unnecessary memory allocations

### Removed

- Removed unused code
- Removed old 'About and Support' window

## [1.0.0] - 2024-08-25

### Added

- Added the Fast Outline effect for rendering simple outlines using vertex extrusion
- Added the Soft Outline effect for rendering soft and glowy outlines
- Added the Wide Outline effect for rendering consistent and smooth outlines
- Added the Edge Detection effect for rendering a full-screen outline effect that applies to the whole scene
- Added the Surface Fill effect for rendering screen-space fill effects and patterns
for rendering screen-space fill effects and patterns