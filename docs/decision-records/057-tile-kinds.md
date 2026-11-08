# 057: Tile kinds

## Context

Inciting event (2026-11-07): Neil added a second kind of tile, grass, beside the ground. Until now a tile was solid or empty: `TileGrid` held bools, and every non-zero value of LDtk's `SolidGeometry` layer was solid (`009 §1.2`). Every tile was drawn with one mesh, `assets/tile/tile.tscn`, and LDtk drew levels with the Kenney 1-bit tileset.

Closed on 2026-11-07. Outcomes are in the architecture docs of `WorldData`, `Levels`, `Simulation`, Core's root and `game/main/`, in `game/`'s, and in [models.md](../designs/models.md).

## 1. Kinds of tile

- [x] 1.1 A tile has a kind, a `TileIndex`: `Empty`, `Ground` or `Grass`. Its values are the `SolidGeometry` layer's, with `Empty` at 0 and `Count` last, so that code can loop over the kinds. Every kind but `Empty` is solid. A text room's `#` is `Ground`. Decision (Neil): kinds, which `009 §1.2` put off.
- [x] 1.2 Kinds differ in looks only, for now (Neil).
- [ ] 1.3 Whether kinds will differ in play, such as in friction, or as one-way platforms.
- [x] 1.4 The LDtk reader rejects a `SolidGeometry` value that isn't a kind (Neil).

## 2. Drawing tiles

- [x] 2.1 `RoomNode` draws each kind as two multimeshes: its tiles, and a darker layer behind them. The back wall with holes in it, which spread from the solid tiles (`020 §2.1`), is gone until something replaces it (Neil).
- [ ] 2.2 What replaces the back wall.
- [x] 2.3 Each tile, and each tile behind it, is turned about the view axis by a random quarter-turn, which hides repetition in the tile art (Neil).
- [x] 2.4 The random numbers come from a seeded hash, `Rand`, FNV-1a for the seed and SplitMix64 for each number, in place of a sum of sines of the tile's coordinates. The sum was concentrated around 0.5, so `floor(r * 4)` was almost always 1 or 2, and few tiles got the other two rotations (Neil). `Rand` is in Core's root, because the game's `Garble` uses the same hashes.

## 3. Assets

- [x] 3.1 Each kind's model is in its own folder, `assets/tiles/<kind>/`, and the game's asset library maps each kind to its wrapper scene. A model's wrapper scene may have any name; it no longer has to be `scene.tscn` (Neil).
- [x] 3.2 LDtk draws levels with `assets/authoring/ldtk-atlas.png`, one cell per kind, which makes them look somewhat as they do in the game, in place of the Kenney tileset, which is removed. The placeholder tile models are textured with it too. Blender copies it beside each model when exporting, as `<kind>_ldtk-atlas.png`, and the copies don't matter (Neil).
- [x] 3.3 `assets/authoring/` holds what is for making assets and levels, not for the game to load. Besides the atlas, `128-color-palette.png` is a screenshot of the palette card (key 6), scaled down, to draw pixel art from (Neil).
- [x] 3.4 The glTF's node in a wrapper scene may have any name; it no longer has to be `Model` (`006 §1.3`). Nothing needs the name but code that finds nodes in a model by path, such as `AssetLibrary` with the platform's parts, which is kept in step with the model's names. The tiles' wrappers name it after the model (Neil).
