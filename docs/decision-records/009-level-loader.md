# 009: The first level loader

## Context

Inciting event (2026-10-16): work on issue #10, the level loader, began. `000 §1.2` had decided to read the LDtk JSON at runtime, in Core, into a `Room`, and to build the 3D scene from the `Room` as the game already does for its text rooms. No loader existed yet.

What was there:
- `game/levels/main.ldtk`, a GridVania world of 16-pixel tiles, with levels of 320x240 pixels (20x15 tiles) saved in files of their own. It has one level, `Level_0`, in `main/Level_0.ldtkl`.
- Its layers: `SolidGeometry`, an IntGrid with `Ground` (1) and `BrickPlat` (2), auto-tiled from the Kenney 1-bit tileset for the editor's sake; and `Characters`, with the entities `PlayerStart`, `RobotBasic`, `PlatformHorizontal` (fields `Width` and `HorizontalDirection`) and, added by Neil on 2026-10-16, `Block`.
- `Room`, built only from text, and `TileGrid`, a grid of bools.

Goal: play `Level_0` in the game. Anti-goal: anything that a seamless world will need but one level doesn't. Scope is to be kept small (Neil).

Closed on 2026-10-17. Outcomes are in [architecture.md](../designs/architecture.md) and [rendering.md](../designs/rendering.md), and so is the unresolved item.

## 1. Scope

- [x] 1.1 One level, one room, or the whole world stitched into one map? Decision (Neil): each LDtk level becomes one `Room`. Stitching levels into one map is a separate piece of work, later.
- [x] 1.2 Tile kinds. Should `BrickPlat` differ from `Ground`, e.g. as a one-way platform, which would make `TileGrid` hold kinds instead of bools? Decision (Neil): not yet. Every non-zero IntGrid value is solid.
- [x] 1.3 Auto-layers. Decision (Neil): ignore `autoLayerTiles` and everything else LDtk writes for auto-layering. The 3D look of tiles is for later (#7).
- [x] 1.4 Entity fields. Decision (Neil): ignore `PlatformHorizontal`'s `Width` and `HorizontalDirection`. They were exploration. They, or something like them, will mean something later.
- [x] 1.5 Blocks. Neil added a `Block` entity to the LDtk project, and one block to `Level_0`.

## 2. Rooms

- [x] 2.1 How a `Room` is built. Decision (Neil): a `Room` holds a tile grid and its entities' start points, and the text format and LDtk are two sources of one kind of room.

## 3. Reading the JSON

- [x] 3.1 Neil's preference (2026-10-16): he strongly prefers well-typed and validated data ingestion. Recorded as a project convention in [architecture.md](../designs/architecture.md#conventions).
- [x] 3.2 How. Claude's proposal, built for Neil to review (Neil has not parsed JSON in C# before, and will learn it by review). Decision (Neil, 2026-10-17): as built.
  - `System.Text.Json`, which is part of .NET, into small records for only the parts the loader reads, rather than LDtk's generated classes for the whole schema. Unread properties are skipped, so LDtk can add properties without breaking the loader.
  - Its source generator, so that the parsing code is written at compile time, from the records, with no reflection.
  - Every property the loader reads is `required`, so a missing one is an error. Nullable annotations are respected, so a null where the record has no `?` is an error.
  - Strings with a fixed set of values are enums, so an unknown one is an error: a layer's type, a layer's name, and an entity's name. A new layer or entity in LDtk stops the level loading until the code knows it.
  - After parsing, the loader checks what types can't say: the JSON version, grid sizes, the length of the IntGrid's values, that each layer appears once.
  - Every failure is an `InvalidDataException` naming the file, and for a JSON error, the path in it.
- [x] 3.3 Where the checks live. Neil asked whether records can validate themselves, as Pydantic's can, so that every use of them validates.
  - Possible: System.Text.Json calls `OnDeserialized` on a record that implements `IJsonOnDeserialized`, also with the source generator. A constructor that System.Text.Json calls can check too. DataAnnotations attributes such as `[Range]` aren't run.
  - Tried on 2026-10-17 and reverted: a `JsonException` thrown from `OnDeserialized` doesn't get the JSON path that System.Text.Json's own errors carry.
  - Decision (Neil): keep every check after parsing, in the reader. He wants JSON paths in errors, and would rather not have checks split between the records and the reader.

## 4. Files and the game

- [x] 4.1 How the file is read. Godot's `FileAccess` on `res://`, because exported builds keep files in the `.pck`, where `System.IO` can't see them. Exports will need `*.ldtk` and `*.ldtkl` in their filter of non-resource files, when there are exports.
- [x] 4.2 Integration tests with real levels. Decision (Neil): don't link `game/levels` into the test project yet.
- [x] 4.3 Where LDtk levels go in `Main`. Decision (Neil): all three of Claude's options. The levels come first in the list that `,` and `.` cycle through, so `Level_0` is the default room, and the room-name argument on the command line names a level as well as a text room.
- [x] 4.4 Who writes it. Neil had a long day of coding. Claude writes it and Neil reviews.

## 5. Scale

- [x] 5.1 Tile size. Neil had pictured a tile's face as 16x16 pixels, which is why LDtk's grid is 16 and its levels are 20x15: 320x240 pixels. He waited to see a 20x15 room in the game before settling it. Decision (Neil, 2026-10-17): settled; the player is the right size. `World`, a new class of constants of the game world, holds `TilePixels`, which the reader checks every layer's grid against.
  - Other 16s in the JSON don't matter to the loader: the editor's defaults for new layers and entities, the tileset's grid (auto-layers are ignored), and an entity's box, from which only the middle of its base is taken. GridVania's 320x240 world grid will matter when levels are stitched together.
- [~] 5.2 The camera. A 20x15 room is a promise the camera must keep, most of the time: it should be placed so that a tile is 16 pixels, rather than fit the room as it does now. Exceptions: zooming out for cinematics, or to show the player something important that is hard to see at the usual distance. Tentative (Neil): contextual zoom may turn out better than a fixed one after trying both.
