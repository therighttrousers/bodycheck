# Controllers

## What it is

What decides an entity's actions each tick: the player's input turned into movement, and the behaviour of each NPC. A controller acts on a body only through what the body offers.

## How it relates to the rest

It depends on the entities it controls, and on Core's root to report measurements and to read features. It doesn't know where its entity is in a room or how the entity got there; whatever builds a room gives each entity its controller.

## What it is not for

New capabilities of a body, such as a motor: those belong to the entity. Reading the keyboard or a gamepad: a controller is given its input. Choosing which controller an entity gets.
